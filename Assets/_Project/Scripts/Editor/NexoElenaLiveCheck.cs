using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Nexo.AI.Elena;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Nexo.EditorTools
{
    /// <summary>
    /// Prueba EN VIVO de la cadena de Elena contra la API de Gemini (menú Nexo/Checks/Elena Live (Gemini) o
    /// -executeMethod Nexo.EditorTools.NexoElenaLiveCheck.RunBatch sin -quit):
    /// 1) configuración del repo (modelo principal + reserva) en los tres pisos, con origen y latencia;
    /// 2) primer modelo con nombre inválido → debe responder la reserva;
    /// 3) todos los modelos inválidos → respaldo JSON offline («IA: respaldo»), nunca silencio.
    /// Usa la clave local de ElenaSecrets (ignorada por Git) y NUNCA la escribe en el log. Copias en memoria de
    /// ElenaConfig: no modifica assets. Resultado en Logs/NexoElenaLiveCheck.txt; en batchmode sale con 0 o 1.
    /// </summary>
    public static class NexoElenaLiveCheck
    {
        private const string ConfigPath = "Assets/_Project/Data/ElenaConfig.asset";
        private const string LogPath = "Logs/NexoElenaLiveCheck.txt";
        private const double WatchdogSeconds = 120d;

        private static readonly StringBuilder Report = new StringBuilder();
        private static double _deadline;
        private static bool _running;

        [MenuItem("Nexo/Checks/Elena Live (Gemini)")]
        public static void RunMenu() => Start();

        /// <summary>Entrada para batchmode (sin -quit): sale con 0 si Gemini respondió y la cadena de respaldo funciona.</summary>
        public static void RunBatch() => Start();

        private static void Start()
        {
            if (_running) return;
            _running = true;
            Report.Clear();
            _deadline = EditorApplication.timeSinceStartup + WatchdogSeconds;
            EditorApplication.update -= Watchdog;
            EditorApplication.update += Watchdog;
            RunAsync();
        }

        private static void Watchdog()
        {
            if (EditorApplication.timeSinceStartup < _deadline) return;
            Line("FALLO: tiempo global agotado (la cadena se colgó)");
            Finish(1);
        }

        private static async void RunAsync()
        {
            int code = 1;
            try
            {
                code = await RunAllAsync();
            }
            catch (Exception e)
            {
                Line("FALLO: excepción " + e.GetType().Name + ": " + e.Message);
            }
            Finish(code);
        }

        private static async Awaitable<int> RunAllAsync()
        {
            ElenaConfig shipped = AssetDatabase.LoadAssetAtPath<ElenaConfig>(ConfigPath);
            if (shipped == null)
            {
                Line("FALLO: no existe " + ConfigPath);
                return 1;
            }
            bool hasKey = shipped.Secrets != null && shipped.Secrets.HasApiKey;
            Line($"Config del repo: principal={shipped.ModelName}, reservas=[{string.Join(", ", shipped.FallbackModelNames)}], " +
                 $"plazo total={shipped.TimeoutSeconds:0.0} s, por modelo={shipped.PerModelTimeoutSeconds:0.0} s, thinking={shipped.ThinkingLevel}, forceFallback={shipped.ForceFallback}");
            Line("Clave local presente: " + (hasKey ? "sí" : "no") + " (el valor nunca se registra)");
            var fallback = new ElenaFallbackProvider(shipped.FallbackFiles);
            int failures = 0;

            // 1) Configuración del repo en los tres pisos
            var questions = new[] { (12, "¿Hay infectados cerca de la salida?"), (6, "¿Cómo abro la puerta de las escaleras?"), (1, "¿Qué hago en el vestíbulo?") };
            int liveAnswers = 0;
            foreach ((int floor, string text) in questions)
            {
                (ElenaResponse r, double ms) = await AskAsync(shipped, shipped.Secrets != null ? shipped.Secrets.ApiKey : null, fallback, floor, text);
                bool live = r.source != ElenaReplyResolver.SourceFallback && r.source != ElenaReplyResolver.SourceEmergency;
                if (live) liveAnswers++;
                Line($"[repo] Piso {floor}: origen={r.source} ({ElenaReplyResolver.MarkerText(r.source)}), {ms:0} ms, emoción={r.emotion}, acción={r.ui_action} → «{r.dialogue}»");
                if (string.IsNullOrWhiteSpace(r.dialogue)) failures++;
            }
            if (hasKey && liveAnswers == 0)
            {
                Line("FALLO: con clave, ninguna respuesta llegó de Gemini");
                failures++;
            }

            // 2) Primer modelo inválido → responde la reserva
            ElenaConfig badFirst = Copy(shipped, "nexo-modelo-inexistente", shipped.FallbackModelNames);
            (ElenaResponse r2, double ms2) = await AskAsync(badFirst, shipped.Secrets != null ? shipped.Secrets.ApiKey : null, fallback, 12, "¿Sigues ahí, Elena?");
            Line($"[primer modelo inválido] origen={r2.source} ({ElenaReplyResolver.MarkerText(r2.source)}), {ms2:0} ms → «{r2.dialogue}»");
            if (string.IsNullOrWhiteSpace(r2.dialogue) || r2.source == "nexo-modelo-inexistente") failures++;
            UnityEngine.Object.DestroyImmediate(badFirst);

            // 3) Todos inválidos → respaldo offline
            ElenaConfig allBad = Copy(shipped, "nexo-modelo-inexistente", new[] { "nexo-reserva-inexistente" });
            (ElenaResponse r3, double ms3) = await AskAsync(allBad, shipped.Secrets != null ? shipped.Secrets.ApiKey : null, fallback, 6, "¿Hay alguien más?");
            Line($"[todos inválidos] origen={r3.source} ({ElenaReplyResolver.MarkerText(r3.source)}), {ms3:0} ms → «{r3.dialogue}»");
            if (r3.source != ElenaReplyResolver.SourceFallback || string.IsNullOrWhiteSpace(r3.dialogue)) failures++;
            UnityEngine.Object.DestroyImmediate(allBad);

            // 4) Sin clave → respaldo offline al instante
            (ElenaResponse r4, double ms4) = await AskAsync(shipped, null, fallback, 1, "¿Me oyes?");
            Line($"[sin clave] origen={r4.source} ({ElenaReplyResolver.MarkerText(r4.source)}), {ms4:0} ms → «{r4.dialogue}»");
            if (r4.source != ElenaReplyResolver.SourceFallback || ms4 > 1500) failures++;

            Line(failures == 0 ? "RESULTADO: OK" : $"RESULTADO: {failures} fallo(s)");
            return failures == 0 ? 0 : 1;
        }

        /// <summary>Misma cadena que ElenaAIService: Gemini (modelos en orden) → JSON del piso → emergencia.</summary>
        private static async Awaitable<(ElenaResponse, double)> AskAsync(ElenaConfig config, string apiKey, ElenaFallbackProvider fallback, int floor, string text)
        {
            var context = new ElenaRequestContext { floor = floor, player_intent = "GENERIC", player_text = text, room_id = floor == 12 ? "F12_Cubicles" : floor == 6 ? "F06_Labs" : "F01_Lobby" };
            var provider = new GeminiProvider(config, apiKey);
            var watch = Stopwatch.StartNew();
            ElenaResponse remote = null;
            try
            {
                remote = await provider.RequestAsync(context, new List<ChatTurn>());
            }
            catch (Exception e)
            {
                Line("  (excepción del proveedor, se usa el respaldo: " + e.GetType().Name + ")");
            }
            ElenaResponse response = ElenaReplyResolver.Pick(remote, provider.LastModel, () => fallback.Get(floor, "GENERIC"), out _);
            watch.Stop();
            return (response, watch.Elapsed.TotalMilliseconds);
        }

        private static ElenaConfig Copy(ElenaConfig source, string model, string[] fallbacks)
        {
            ElenaConfig copy = UnityEngine.Object.Instantiate(source);
            var so = new SerializedObject(copy);
            so.FindProperty("_modelName").stringValue = model;
            SerializedProperty list = so.FindProperty("_fallbackModelNames");
            list.arraySize = fallbacks.Length;
            for (int i = 0; i < fallbacks.Length; i++) list.GetArrayElementAtIndex(i).stringValue = fallbacks[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            return copy;
        }

        private static void Line(string text)
        {
            Report.AppendLine(text);
            Debug.Log("[NexoElenaLiveCheck] " + text);
        }

        private static void Finish(int code)
        {
            if (!_running) return;
            _running = false;
            EditorApplication.update -= Watchdog;
            Directory.CreateDirectory("Logs");
            File.WriteAllText(LogPath, Report.ToString(), new UTF8Encoding(false));
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }
    }
}
