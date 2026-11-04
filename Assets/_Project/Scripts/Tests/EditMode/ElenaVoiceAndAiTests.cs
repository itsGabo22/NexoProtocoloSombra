using System;
using System.Collections.Generic;
using Nexo.AI.Elena;
using Nexo.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Nexo.Tests
{
    /// <summary>Pase de jugabilidad (partes 3 y 4): voz de Elena, latencia de Gemini, filtro de respuestas y texto escrito.</summary>
    public sealed class ElenaVoiceAndAiTests
    {
        private const string LibraryPath = "Assets/_Project/Resources/Elena/ElenaVoiceLibrary.asset";
        private static readonly string[] FallbackFiles =
        {
            "Assets/_Project/Data/ElenaFallback/fallback_floor12.json",
            "Assets/_Project/Data/ElenaFallback/fallback_floor06.json",
            "Assets/_Project/Data/ElenaFallback/fallback_floor01.json"
        };

        [Serializable] private sealed class R { public string dialogue; }
        [Serializable] private sealed class E { public string key; public R response; }
        [Serializable] private sealed class L { public E[] entries; }

        // ---------- Cobertura de voz ----------

        [Test]
        public void Voice_EveryFallbackAndTwistLineHasAClip()
        {
            var library = AssetDatabase.LoadAssetAtPath<ElenaVoiceLibrary>(LibraryPath);
            Assert.IsNotNull(library, "Falta Resources/Elena/ElenaVoiceLibrary.asset (Nexo › Elena › Generate Voice)");
            var missing = new List<string>();
            foreach (string path in FallbackFiles)
            {
                var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                foreach (E e in JsonUtility.FromJson<L>(asset.text).entries)
                    if (!library.TryGetClip(e.response.dialogue, out _)) missing.Add($"{path}:{e.key}");
            }
            foreach (string twist in new[] { "Perdóname, Alex.", "Necesitaba que llegaras hasta aquí.", "Tú abriste la puerta por los dos." })
                if (!library.TryGetClip(twist, out _)) missing.Add("giro: " + twist);
            Assert.IsEmpty(missing, "Líneas fijas de Elena sin voz:\n" + string.Join("\n", missing));
        }

        [Test]
        public void Voice_LibraryEntriesAllHaveClips()
        {
            var library = AssetDatabase.LoadAssetAtPath<ElenaVoiceLibrary>(LibraryPath);
            Assert.IsNotNull(library);
            Assert.Greater(library.Entries.Count, 40);
            foreach (ElenaVoiceLibrary.Entry e in library.Entries)
            {
                Assert.IsNotNull(e.Clip, e.Text);
                Assert.AreEqual(ElenaVoiceText.Normalize(e.Text), e.Key);
                Assert.Less(e.Clip.length, 15f, "clip demasiado largo: " + e.Text);
            }
            StringAssert.Contains("Gemini", library.Engine);
        }

        // ---------- Audio ----------

        [Test]
        public void Pcm_WavRoundTrip()
        {
            var samples = new float[2400];
            for (int i = 0; i < samples.Length; i++) samples[i] = Mathf.Sin(i * 0.1f) * 0.5f;
            byte[] wav = VoicePcm.EncodeWav(samples, 24000);
            Assert.IsTrue(VoicePcm.TryDecode(wav, "audio/wav", out float[] back, out int rate));
            Assert.AreEqual(24000, rate);
            Assert.AreEqual(samples.Length, back.Length);
            Assert.AreEqual(samples[100], back[100], 1e-3f);
        }

        [Test]
        public void Pcm_RawL16UsesRateFromMime()
        {
            byte[] raw = new byte[4800];
            Assert.IsTrue(VoicePcm.TryDecode(raw, "audio/L16;codec=pcm;rate=16000", out float[] s, out int rate));
            Assert.AreEqual(16000, rate);
            Assert.AreEqual(2400, s.Length);
        }

        private static float[] Tone(int rate, params float[] segments)
        {
            // segmentos alternos voz / silencio (segundos), empezando por silencio
            var list = new List<float>();
            for (int k = 0; k < segments.Length; k++)
            {
                int n = (int)(segments[k] * rate);
                for (int i = 0; i < n; i++) list.Add(k % 2 == 1 ? Mathf.Sin(i * 0.3f) * 0.6f : 0f);
            }
            return list.ToArray();
        }

        [Test]
        public void Pcm_CompressSilenceShortensLongGapsAndTrimsEnds()
        {
            const int rate = 8000;
            float[] audio = Tone(rate, 1.0f, 1.0f, 2.0f, 1.0f, 1.5f);
            float[] result = VoicePcm.CompressSilence(audio, rate);
            float seconds = result.Length / (float)rate;
            Assert.Less(seconds, 2.6f);
            Assert.Greater(seconds, 2.0f);
        }

        [Test]
        public void Pcm_SplitsOnLongestGaps()
        {
            const int rate = 8000;
            float[] audio = Tone(rate, 0.2f, 1.0f, 0.4f, 0.8f, 1.2f, 1.1f, 1.0f, 0.9f, 0.2f);
            List<float[]> parts = VoicePcm.SplitOnLongestGaps(audio, rate, 3);
            Assert.IsNotNull(parts);
            Assert.AreEqual(3, parts.Count);
            // El primer corte cae en el silencio de 1,2 s (no en el de 0,4 s)
            Assert.Greater(parts[0].Length / (float)rate, 2.4f);
            Assert.IsNull(VoicePcm.SplitOnLongestGaps(Tone(rate, 0.1f, 2f, 0.1f), rate, 3), "sin silencios suficientes no se corta");
        }

        // ---------- Estilo de voz ----------

        [Test]
        public void Style_PerEmotionPrefixAndLeakDetection()
        {
            StringAssert.StartsWith("Say urgently", ElenaVoiceStyle.Prompt("URGENT", "¡Corre!"));
            StringAssert.EndsWith(": ¡Corre!", ElenaVoiceStyle.Prompt("URGENT", "¡Corre!"));
            StringAssert.StartsWith("Whisper", ElenaVoiceStyle.Prefix("whisper"));
            StringAssert.StartsWith("Say softly", ElenaVoiceStyle.Prefix(null));
            Assert.IsTrue(ElenaVoiceStyle.TranscriptLeaksStyle("Say softly, like a young girl: hola"));
            Assert.IsTrue(ElenaVoiceStyle.TranscriptLeaksStyle("Di con voz suave de niña pequeña: hola"));
            Assert.IsFalse(ElenaVoiceStyle.TranscriptLeaksStyle("Alex, ¿me oyes? Estoy aquí."));
        }

        [Test]
        public void VoiceText_WordOverlap()
        {
            Assert.AreEqual(1f, ElenaVoiceText.WordOverlap("¡Corre, Alex!", "corre alex"), 1e-4f);
            Assert.Less(ElenaVoiceText.WordOverlap("Tengo el llavero de mi mamá", "Por el pasillo no hay nadie"), 0.5f);
        }

        [Test]
        public void Tts_BodyContainsAudioModalityAndVoice()
        {
            string body = GeminiTtsClient.BuildBody("SAD", "Perdóname, Alex.", "Leda");
            StringAssert.Contains("\"responseModalities\":[\"AUDIO\"]", body);
            StringAssert.Contains("\"voiceName\":\"Leda\"", body);
            StringAssert.Contains("Say sadly", body);
        }

        // ---------- Latencia de Gemini ----------

        [Test]
        public void Thinking_LowLevelForGemini3_BudgetZeroFor25_NoneForTts()
        {
            Assert.AreEqual("\"thinkingConfig\":{\"thinkingLevel\":\"low\"}", GeminiThinking.ConfigFragment("gemini-3.8-flash", "low"));
            Assert.AreEqual("\"thinkingConfig\":{\"thinkingBudget\":0}", GeminiThinking.ConfigFragment("gemini-2.5-flash", "low"));
            Assert.AreEqual(string.Empty, GeminiThinking.ConfigFragment("gemini-3.8-flash-lite-tts", "low"));
            Assert.AreEqual(string.Empty, GeminiThinking.ConfigFragment("gemini-3.8-flash", ""));
        }

        [Test]
        public void Thinking_InjectedIntoGenerationConfig()
        {
            string json = "{\"contents\":[],\"generationConfig\":{\"temperature\":0.7}}";
            string result = GeminiThinking.Inject(json, GeminiThinking.ConfigFragment("gemini-3.8-flash", "low"));
            Assert.AreEqual("{\"contents\":[],\"generationConfig\":{\"thinkingConfig\":{\"thinkingLevel\":\"low\"},\"temperature\":0.7}}", result);
            Assert.AreEqual("{\"generationConfig\":{\"thinkingConfig\":{\"thinkingBudget\":0}}}",
                GeminiThinking.Inject("{\"generationConfig\":{}}", GeminiThinking.ConfigFragment("gemini-2.5-flash", "low")));
        }

        [Test]
        public void Thinking_RetryNextModelOnlyOnTransientErrors()
        {
            Assert.IsTrue(GeminiThinking.ShouldTryNextModel(503, false));
            Assert.IsTrue(GeminiThinking.ShouldTryNextModel(429, false));
            Assert.IsTrue(GeminiThinking.ShouldTryNextModel(404, false));
            Assert.IsTrue(GeminiThinking.ShouldTryNextModel(0, true));
            Assert.IsFalse(GeminiThinking.ShouldTryNextModel(400, false), "petición mal formada: no insistir");
            Assert.IsFalse(GeminiThinking.ShouldTryNextModel(403, false), "clave no válida: respaldo offline directo");
        }

        [Test]
        public void Config_DefaultsUseGemini38()
        {
            var config = ScriptableObject.CreateInstance<ElenaConfig>();
            Assert.AreEqual("gemini-3.8-flash", config.ModelName);
            Assert.AreEqual("gemini-3.8-flash", config.SpeechToTextModel);
            StringAssert.StartsWith("gemini-3.8-flash", config.TtsModel);
            Assert.AreEqual("low", config.ThinkingLevel);
            Assert.LessOrEqual(config.TimeoutSeconds, 5.5f);
            StringAssert.Contains("gemini-3.8-flash:generateContent", config.BuildEndpointUrl());
            UnityEngine.Object.DestroyImmediate(config);
        }

        [Test]
        public void Config_RepoAssetKeepsFallbackOffAndModels()
        {
            var config = AssetDatabase.LoadAssetAtPath<ElenaConfig>("Assets/_Project/Data/ElenaConfig.asset");
            Assert.IsNotNull(config);
            Assert.IsFalse(config.ForceFallback, "En el repo Elena usa Gemini en el Editor");
            Assert.AreEqual("gemini-3.8-flash", config.ModelName);
        }

        // ---------- Filtro de respuestas ----------

        [Test]
        public void Guard_BlocksTwistSpoilersAndOutOfCharacter()
        {
            Assert.IsFalse(ElenaReplyGuard.IsSafe("Alex, estás infectado desde el principio."));
            Assert.IsFalse(ElenaReplyGuard.IsSafe("Tu escáner dice 98,4 %."));
            Assert.IsFalse(ElenaReplyGuard.IsSafe("Las gafas te mienten, Alex."));
            Assert.IsFalse(ElenaReplyGuard.IsSafe("Soy una IA de Google."));
            Assert.IsFalse(ElenaReplyGuard.IsSafe(""));
            Assert.IsTrue(ElenaReplyGuard.IsSafe("Hay un infectado en el pasillo, ve agachado."));
            Assert.IsTrue(ElenaReplyGuard.IsSafe("Mi mamá dijo que las gafas fallan cuando suena la alarma."));
        }

        [Test]
        public void Guard_ClampsLongReplies()
        {
            string longText = string.Join(" ", new string[60]).Replace(" ", "palabra ");
            string clamped = ElenaReplyGuard.Clamp(longText);
            Assert.LessOrEqual(clamped.Length, ElenaReplyGuard.MaxDialogueChars + 3);
            StringAssert.EndsWith("...", clamped);
            Assert.AreEqual("Corto.", ElenaReplyGuard.Clamp("Corto."));
        }

        // ---------- Texto escrito ----------

        [Test]
        public void TypedText_CleansWhitespaceAndLength()
        {
            Assert.AreEqual("hola Elena", ElenaTypedText.Clean("  hola \n\t Elena  "));
            Assert.AreEqual(string.Empty, ElenaTypedText.Clean("   "));
            Assert.AreEqual(ElenaTextInput.MaxChars, ElenaTypedText.Clean(new string('a', 300)).Length);
        }
    }
}
