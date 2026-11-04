using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Nexo.AI.Elena;
using Nexo.Interaction;
using Nexo.Narrative;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Nexo.EditorTools
{
    /// <summary>
    /// Nexo › Elena › Generate Voice: pregenera la voz de TODAS las líneas fijas de Elena con Gemini TTS.
    /// Fuentes: fallback_floor12/06/01.json (incluye las respuestas a [1]-[3]), las líneas «Elena» de las StorySequence de
    /// los 3 pisos, los avisos de FakeVoiceWarning (los dice la Elena real), las líneas del giro (PlotTwistManager, solo se
    /// leen) y la respuesta de emergencia de ElenaAIService. Los mensajes falsos no se voz-ean (llevan su propio audio).
    /// - Modelo/voz de ElenaConfig (TTS), con reservas si el modelo no está disponible; si todo falla, voz de Windows.
    /// - La cuota gratuita de TTS es de ~10 peticiones al día por modelo: las líneas de la misma emoción se piden en lotes
    ///   (hasta 6) y el audio se corta por los silencios más largos. Cada trozo se verifica transcribiéndolo; si no
    ///   coincide con su línea (o el modelo leyó la indicación de estilo), el lote se reparte en lotes menores.
    /// - Reintentos con espera creciente ante 429/503 (una espera de horas = cuota agotada: pasa al siguiente modelo),
    ///   ritmo mínimo entre peticiones y reanudación: lo ya generado con el modelo principal no se vuelve a pedir.
    /// - Guarda WAV en Audio/Elena/Voice, el manifiesto voice_manifest.json y el índice Resources/Elena/ElenaVoiceLibrary.
    /// La clave sale de ElenaSecrets (ignorado en git) y nunca se escribe en el log.
    /// -executeMethod Nexo.EditorTools.NexoElenaVoiceGenerator.GenerateBatch (sale con 0 si la cobertura es completa).
    /// </summary>
    public static class NexoElenaVoiceGenerator
    {
        public const string VoiceFolder = "Assets/_Project/Audio/Elena/Voice";
        public const string LibraryPath = "Assets/_Project/Resources/Elena/ElenaVoiceLibrary.asset";
        private const string ManifestPath = VoiceFolder + "/voice_manifest.json";
        private const string ConfigPath = "Assets/_Project/Data/ElenaConfig.asset";
        private const string FallbackFolder = "Assets/_Project/Data/ElenaFallback";
        private const string VerifyModel = "gemini-3.5-flash-lite";
        private const double MinSecondsBetweenRequests = 6.5;
        private const int MaxAttempts = 3;
        private const double MaxRetryDelaySeconds = 120;
        private const int BatchSize = 3;
        private const int BatchChars = 240;
        /// <summary>La voz de reserva de Windows es adulta: se sube un poco el tono para acercarla a la de una niña.</summary>
        private const float WindowsPitch = 1.16f;

        private static readonly string[] ReserveModels = { "gemini-3.8-flash-tts", "gemini-3.1-flash-tts-preview", "gemini-2.5-flash-preview-tts", "gemini-2.5-pro-preview-tts" };

        private static readonly string[] FloorScenes =
        {
            "Assets/_Project/Scenes/Floors/Floor12_Offices.unity",
            "Assets/_Project/Scenes/Floors/Floor06_Labs.unity",
            "Assets/_Project/Scenes/Floors/Floor01_Lobby.unity"
        };

        /// <summary>Línea hablada por Elena con su emoción y su origen.</summary>
        public sealed class VoiceLine
        {
            public string Text;
            public string Emotion;
            public string Source;
            public string Key => ElenaVoiceText.Normalize(Text);
        }

        [Serializable] private sealed class ManifestEntry { public string key; public string text; public string emotion; public string source; public string file; public string engine; public float seconds; }
        [Serializable] private sealed class Manifest { public List<ManifestEntry> entries = new List<ManifestEntry>(); }

        [Serializable] private sealed class FallbackResponse { public string dialogue; public string emotion; }
        [Serializable] private sealed class FallbackEntry { public int floor; public string key; public FallbackResponse response; }
        [Serializable] private sealed class FallbackLibrary { public FallbackEntry[] entries; }

        private static DateTime s_lastRequest = DateTime.MinValue;

        [MenuItem("Nexo/Elena/Generate Voice (Gemini TTS)")]
        public static void GenerateMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            int missing = Generate(out string report);
            Debug.Log(report);
            EditorUtility.DisplayDialog("Voz de Elena", missing == 0 ? "Todas las líneas tienen voz." : $"Faltan {missing} línea(s). Detalle en la Console.", "OK");
        }

        [MenuItem("Nexo/Checks/Elena Voice Coverage")]
        public static void CoverageMenu()
        {
            int missing = CheckCoverage(out string report);
            Debug.Log(report);
            EditorUtility.DisplayDialog("Cobertura de voz", missing == 0 ? "Todas las líneas fijas de Elena tienen clip." : $"Faltan {missing}. Detalle en la Console.", "OK");
        }

        public static void GenerateBatch()
        {
            int missing;
            try { missing = Generate(out string report); Debug.Log(report); }
            catch (Exception ex) { Debug.LogException(ex); missing = -1; }
            EditorApplication.Exit(missing == 0 ? 0 : 1);
        }

        public static void CoverageBatch()
        {
            int missing = CheckCoverage(out string report);
            Debug.Log(report);
            EditorApplication.Exit(missing == 0 ? 0 : 1);
        }

        // ---------- Recogida de líneas ----------

        public static List<VoiceLine> CollectLines()
        {
            var lines = new List<VoiceLine>();
            foreach (string guid in AssetDatabase.FindAssets("t:TextAsset fallback_floor", new[] { FallbackFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                FallbackLibrary lib = asset != null ? JsonUtility.FromJson<FallbackLibrary>(asset.text) : null;
                if (lib?.entries == null) continue;
                foreach (FallbackEntry e in lib.entries)
                    if (e?.response != null) Add(lines, e.response.dialogue, e.response.emotion, $"{Path.GetFileNameWithoutExtension(path)}:{e.key}");
            }

            Add(lines, "Alex... sigue caminando hacia la esclusa, por favor.", "SCARED", "ElenaAIService:emergencia");

            foreach (string scenePath in FloorScenes)
            {
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                foreach (StorySequence seq in UnityEngine.Object.FindObjectsByType<StorySequence>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID))
                {
                    var so = new SerializedObject(seq);
                    SerializedProperty arr = so.FindProperty("_lines");
                    for (int i = 0; arr != null && i < arr.arraySize; i++)
                    {
                        SerializedProperty line = arr.GetArrayElementAtIndex(i);
                        if (line.FindPropertyRelative("Speaker").enumValueIndex != (int)StorySequence.Speaker.Elena) continue;
                        Add(lines, line.FindPropertyRelative("Text").stringValue, line.FindPropertyRelative("Emotion").stringValue, $"{scene.name}:{seq.name}");
                    }
                }
                foreach (FakeVoiceWarning warning in UnityEngine.Object.FindObjectsByType<FakeVoiceWarning>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID))
                {
                    SerializedProperty arr = new SerializedObject(warning).FindProperty("_warnings");
                    for (int i = 0; arr != null && i < arr.arraySize; i++) Add(lines, arr.GetArrayElementAtIndex(i).stringValue, "URGENT", $"{scene.name}:{warning.name}");
                }
                foreach (PlotTwistManager twist in UnityEngine.Object.FindObjectsByType<PlotTwistManager>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID))
                {
                    SerializedProperty arr = new SerializedObject(twist).FindProperty("_elenaLines");
                    for (int i = 0; arr != null && i < arr.arraySize; i++) Add(lines, arr.GetArrayElementAtIndex(i).stringValue, "SAD", $"{scene.name}:giro");
                }
            }
            return lines;
        }

        private static void Add(List<VoiceLine> lines, string text, string emotion, string source)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            var line = new VoiceLine { Text = text.Trim(), Emotion = string.IsNullOrWhiteSpace(emotion) ? "CALM" : emotion.Trim().ToUpperInvariant(), Source = source };
            if (line.Key.Length == 0 || lines.Any(l => l.Key == line.Key)) return;
            lines.Add(line);
        }

        // ---------- Generación ----------

        public static int Generate(out string report)
        {
            var sb = new StringBuilder("[NexoElenaVoiceGenerator]\n");
            List<VoiceLine> lines = CollectLines();
            Directory.CreateDirectory(VoiceFolder);
            Manifest manifest = LoadManifest();

            var config = AssetDatabase.LoadAssetAtPath<ElenaConfig>(ConfigPath);
            string apiKey = FindApiKey(config);
            string model = config != null ? config.TtsModel : ElenaConfig.DefaultTtsModel;
            string voice = config != null ? config.TtsVoice : ElenaConfig.DefaultTtsVoice;
            string endpoint = config != null ? config.EndpointTemplate : ElenaConfig.DefaultEndpoint;
            var models = new List<string> { model };
            models.AddRange(ReserveModels.Where(m => m != model));
            var deadModels = new HashSet<string>();
            int generated = 0, reused = 0, windows = 0;

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
            var pending = new List<VoiceLine>();
            foreach (VoiceLine line in lines)
            {
                // Se reutiliza todo lo sintetizado con Gemini; solo se rehacen las líneas que faltan o que salieron con la
                // voz de reserva de Windows (al volver la cuota de Gemini, se sustituyen sin gastar peticiones en el resto)
                ManifestEntry existing = manifest.entries.FirstOrDefault(e => e.key == line.Key && e.emotion == line.Emotion);
                bool gemini = existing != null && (existing.engine ?? string.Empty).StartsWith("Gemini");
                if (gemini && File.Exists(Path.Combine(VoiceFolder, existing.file))) { existing.source = line.Source; reused++; }
                else pending.Add(line);
            }

            // Lotes de la misma emoción (cuota gratuita de TTS: ~10 peticiones/día por modelo)
            var queue = new List<List<VoiceLine>>();
            foreach (IGrouping<string, VoiceLine> group in pending.GroupBy(l => l.Emotion))
            {
                var batch = new List<VoiceLine>();
                foreach (VoiceLine line in group)
                {
                    if (batch.Count >= BatchSize || batch.Sum(b => b.Text.Length) + line.Text.Length > BatchChars) { queue.Add(batch); batch = new List<VoiceLine>(); }
                    batch.Add(line);
                }
                if (batch.Count > 0) queue.Add(batch);
            }

            while (queue.Count > 0)
            {
                List<VoiceLine> batch = queue[0];
                queue.RemoveAt(0);
                string emotion = batch[0].Emotion;
                string prompt = batch.Count == 1
                    ? ElenaVoiceStyle.Prompt(emotion, batch[0].Text)
                    : $"{ElenaVoiceStyle.Prefix(emotion)}, pausing for two seconds between lines:\n" + string.Join("\n\n", batch.Select(b => b.Text));

                List<float[]> segments = null;
                string engine = null;
                int rate = 24000;
                bool retrySmaller = false;
                foreach (string m in models.Where(x => !deadModels.Contains(x)).ToList())
                {
                    if (string.IsNullOrEmpty(apiKey)) break;
                    if (!TrySynthesizeText(http, endpoint, m, voice, apiKey, prompt, out float[] audio, out rate, out bool modelDead))
                    {
                        if (modelDead) { deadModels.Add(m); sb.AppendLine($"   modelo agotado o no disponible: {m}"); continue; }
                        break; // fallo transitorio agotado: se reanudará en la próxima ejecución
                    }
                    segments = batch.Count == 1 ? new List<float[]> { audio } : VoicePcm.SplitOnLongestGaps(audio, rate, batch.Count);
                    bool ok = segments != null;
                    for (int s = 0; ok && s < batch.Count; s++)
                    {
                        string transcript = Transcribe(http, endpoint, apiKey, segments[s], rate);
                        if (ElenaVoiceStyle.TranscriptLeaksStyle(transcript)) ok = false;
                        else if (transcript != null && ElenaVoiceText.WordOverlap(batch[s].Text, transcript) < 0.5f) ok = false;
                    }
                    if (!ok)
                    {
                        segments = null;
                        retrySmaller = true;
                        sb.AppendLine($"   lote de {batch.Count} no verificado ({m}); se reparte");
                        break;
                    }
                    engine = $"Gemini TTS {m} · voz {voice}";
                    break;
                }

                if (segments == null && retrySmaller)
                {
                    if (batch.Count > 1)
                    {
                        int half = batch.Count / 2;
                        queue.Insert(0, batch.GetRange(half, batch.Count - half));
                        queue.Insert(0, batch.GetRange(0, half));
                        continue;
                    }
                    // Una sola línea que no pasa la verificación: se pide sin estilo
                    foreach (string m in models.Where(x => !deadModels.Contains(x)))
                    {
                        if (TrySynthesizeText(http, endpoint, m, voice, apiKey, batch[0].Text, out float[] plain, out rate, out _))
                        {
                            segments = new List<float[]> { plain };
                            engine = $"Gemini TTS {m} · voz {voice}";
                            break;
                        }
                    }
                }

                for (int s = 0; s < batch.Count; s++)
                {
                    VoiceLine line = batch[s];
                    float[] samples = segments != null ? segments[s] : null;
                    string lineEngine = engine;
                    int lineRate = rate;
                    if (samples == null && TryWindowsSpeech(line.Text, out samples, out lineRate, out string winVoice))
                    {
                        lineEngine = $"Windows System.Speech · {winVoice}";
                        windows++;
                    }
                    if (samples == null) { sb.AppendLine($"   SIN VOZ: {line.Text}"); continue; }

                    samples = VoicePcm.CompressSilence(samples, lineRate);
                    VoicePcm.Normalize(samples);
                    string file = $"elena_{Slug(line.Source)}_{Hash(line.Key)}.wav";
                    File.WriteAllBytes(Path.Combine(VoiceFolder, file), VoicePcm.EncodeWav(samples, lineRate));
                    manifest.entries.RemoveAll(e => e.key == line.Key);
                    manifest.entries.Add(new ManifestEntry { key = line.Key, text = line.Text, emotion = line.Emotion, source = line.Source, file = file, engine = lineEngine, seconds = samples.Length / (float)lineRate });
                    SaveManifest(manifest); // reanudable aunque se corte a mitad
                    generated++;
                    sb.AppendLine($"   {lineEngine} · {samples.Length / (float)lineRate:0.0} s · [{line.Emotion}] {line.Text}");
                }
            }

            SaveManifest(manifest);
            AssetDatabase.Refresh();
            ConfigureImports(manifest);
            BuildLibrary(manifest, lines);
            int missing = CheckCoverage(out string coverage);
            sb.AppendLine($"Líneas: {lines.Count} · generadas ahora: {generated} · reutilizadas: {reused} · con voz de Windows: {windows}");
            sb.Append(coverage);
            report = sb.ToString();
            return missing;
        }

        private static string FindApiKey(ElenaConfig config)
        {
            if (config != null && config.Secrets != null && config.Secrets.HasApiKey) return config.Secrets.ApiKey.Trim();
            foreach (string guid in AssetDatabase.FindAssets("t:ElenaSecrets"))
            {
                var s = AssetDatabase.LoadAssetAtPath<ElenaSecrets>(AssetDatabase.GUIDToAssetPath(guid));
                if (s != null && s.HasApiKey) return s.ApiKey.Trim();
            }
            return null;
        }

        private static bool TrySynthesizeText(HttpClient http, string endpoint, string model, string voice, string apiKey, string prompt,
                                              out float[] samples, out int rate, out bool modelDead)
        {
            samples = null; rate = 24000; modelDead = false;
            string body = GeminiTtsClient.BuildBodyRaw(prompt, voice);
            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                (int status, string text) = Post(http, string.Format(endpoint, model), apiKey, body);
                if (status == 200)
                {
                    if (GeminiTtsClient.TryParseAudio(text, out byte[] audio, out string mime) && VoicePcm.TryDecode(audio, mime, out samples, out rate)) return true;
                    continue; // respuesta sin audio: se reintenta
                }
                if (status == 404 || status == 403 || status == 400) { modelDead = true; return false; }
                // Sin cuota gratuita para este modelo (límite 0): no tiene sentido esperar
                if (status == 429 && text != null && text.Contains("limit: 0")) { modelDead = true; return false; }
                // 429 / 5xx / red: espera creciente o la que indique el servidor; una espera de horas = cuota diaria agotada
                double? serverDelay = RetryDelay(text);
                if (serverDelay.HasValue && serverDelay.Value > MaxRetryDelaySeconds) { modelDead = true; return false; }
                double wait = serverDelay ?? Math.Min(60, 8 * Math.Pow(2, attempt));
                Debug.Log($"[NexoElenaVoiceGenerator] {model} HTTP {status}; reintento en {wait:0} s");
                Thread.Sleep(TimeSpan.FromSeconds(wait));
            }
            return false;
        }

        private static string Transcribe(HttpClient http, string endpoint, string apiKey, float[] samples, int rate)
        {
            string audio = Convert.ToBase64String(VoicePcm.EncodeWav(samples, rate));
            string body = "{\"contents\":[{\"parts\":[{\"inline_data\":{\"mime_type\":\"audio/wav\",\"data\":\"" + audio +
                          "\"}},{\"text\":\"Transcribe exactamente lo que se dice. Responde solo con el texto.\"}]}],\"generationConfig\":{\"temperature\":0,\"thinkingConfig\":{\"thinkingLevel\":\"low\"}}}";
            (int status, string text) = Post(http, string.Format(endpoint, VerifyModel), apiKey, body);
            if (status != 200) return null;
            Match m = Regex.Match(text, "\"text\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
            return m.Success ? Regex.Unescape(m.Groups[1].Value) : null;
        }

        private static (int, string) Post(HttpClient http, string url, string apiKey, string body)
        {
            double since = (DateTime.UtcNow - s_lastRequest).TotalSeconds;
            if (since < MinSecondsBetweenRequests) Thread.Sleep(TimeSpan.FromSeconds(MinSecondsBetweenRequests - since));
            s_lastRequest = DateTime.UtcNow;
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
                request.Headers.Add("x-goog-api-key", apiKey);
                using HttpResponseMessage response = http.SendAsync(request).GetAwaiter().GetResult();
                string text = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                return ((int)response.StatusCode, text);
            }
            catch (Exception ex)
            {
                return (0, ex.GetType().Name);
            }
        }

        private static double? RetryDelay(string body)
        {
            if (string.IsNullOrEmpty(body)) return null;
            Match m = Regex.Match(body, "\"retryDelay\"\\s*:\\s*\"(\\d+)(?:\\.\\d+)?s\"");
            return m.Success ? double.Parse(m.Groups[1].Value) + 1 : (double?)null;
        }

        /// <summary>Último recurso: la mejor voz española instalada de Windows (System.Speech por PowerShell).</summary>
        private static bool TryWindowsSpeech(string text, out float[] samples, out int rate, out string voiceName)
        {
            samples = null; rate = 22050; voiceName = null;
            if (Application.platform != RuntimePlatform.WindowsEditor) return false;
            string wav = Path.Combine(Path.GetTempPath(), "nexo_elena_sapi.wav");
            string txt = Path.Combine(Path.GetTempPath(), "nexo_elena_sapi.txt");
            File.WriteAllText(txt, text, Encoding.UTF8);
            string script =
                "Add-Type -AssemblyName System.Speech;" +
                "$s = New-Object System.Speech.Synthesis.SpeechSynthesizer;" +
                "$v = $s.GetInstalledVoices() | Where-Object { $_.VoiceInfo.Culture.Name -like 'es-*' } | Sort-Object { if ($_.VoiceInfo.Gender -eq 'Female') {0} else {1} } | Select-Object -First 1;" +
                "if ($v) { $s.SelectVoice($v.VoiceInfo.Name); Write-Output $v.VoiceInfo.Name } else { Write-Output 'default' };" +
                "$s.Rate = 1; $s.SetOutputToWaveFile('" + wav + "');" +
                "$s.Speak([IO.File]::ReadAllText('" + txt + "'));$s.Dispose()";
            try
            {
                var psi = new ProcessStartInfo("powershell.exe", "-NoProfile -NonInteractive -Command \"" + script.Replace("\"", "\\\"") + "\"")
                {
                    UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true
                };
                using Process p = Process.Start(psi);
                voiceName = p.StandardOutput.ReadToEnd().Trim();
                p.WaitForExit(30000);
                if (!File.Exists(wav) || !VoicePcm.TryDecode(File.ReadAllBytes(wav), null, out samples, out rate)) return false;
                samples = VoicePcm.Resample(samples, WindowsPitch);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[NexoElenaVoiceGenerator] Voz de Windows no disponible: {ex.Message}");
                return false;
            }
        }

        // ---------- Índice e importación ----------

        private static void ConfigureImports(Manifest manifest)
        {
            foreach (ManifestEntry e in manifest.entries)
            {
                string path = $"{VoiceFolder}/{e.file}";
                if (!(AssetImporter.GetAtPath(path) is AudioImporter importer)) continue;
                importer.forceToMono = true;
                importer.loadInBackground = false;
                AudioImporterSampleSettings settings = importer.defaultSampleSettings;
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = 0.7f;
                importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();
            }
        }

        private static void BuildLibrary(Manifest manifest, List<VoiceLine> lines)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LibraryPath));
            var library = AssetDatabase.LoadAssetAtPath<ElenaVoiceLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<ElenaVoiceLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }
            var entries = new List<ElenaVoiceLibrary.Entry>();
            foreach (VoiceLine line in lines)
            {
                ManifestEntry m = manifest.entries.FirstOrDefault(e => e.key == line.Key);
                if (m == null) continue;
                entries.Add(new ElenaVoiceLibrary.Entry
                {
                    Key = m.key, Text = m.text, Emotion = m.emotion,
                    Clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{VoiceFolder}/{m.file}")
                });
            }
            string engine = string.Join(" + ", manifest.entries.Select(e => e.engine).Where(e => !string.IsNullOrEmpty(e)).Distinct());
            library.EditorSetEntries(entries, engine);
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
        }

        /// <summary>Cuenta las líneas fijas de Elena sin clip en el índice.</summary>
        public static int CheckCoverage(out string report)
        {
            List<VoiceLine> lines = CollectLines();
            var library = AssetDatabase.LoadAssetAtPath<ElenaVoiceLibrary>(LibraryPath);
            var missing = new List<string>();
            foreach (VoiceLine line in lines)
                if (library == null || !library.TryGetClip(line.Text, out _)) missing.Add($"{line.Source}: {line.Text}");
            report = $"[ElenaVoiceCoverage] {lines.Count - missing.Count}/{lines.Count} líneas fijas de Elena con voz" +
                     (library != null ? $" ({library.Engine})" : " (sin índice)") + "\n" + string.Join("\n", missing.Select(m => "   FALTA " + m));
            return missing.Count;
        }

        private static Manifest LoadManifest()
        {
            if (!File.Exists(ManifestPath)) return new Manifest();
            return JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath)) ?? new Manifest();
        }

        private static void SaveManifest(Manifest manifest) => File.WriteAllText(ManifestPath, JsonUtility.ToJson(manifest, true));

        private static string Slug(string source)
        {
            string s = source.Split(':')[0].ToLowerInvariant();
            if (s.Contains("floor12") || s.Contains("floor12_offices")) return "f12";
            if (s.Contains("floor06") || s.Contains("floor06_labs")) return "f06";
            if (s.Contains("floor01") || s.Contains("floor01_lobby")) return "f01";
            return "misc";
        }

        private static string Hash(string key)
        {
            uint h = 2166136261;
            foreach (char c in key) { h ^= c; h *= 16777619; }
            return h.ToString("x8");
        }
    }
}
