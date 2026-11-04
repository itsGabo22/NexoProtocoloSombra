using Nexo.AI.Elena;
using Nexo.Player;
using NUnit.Framework;

namespace Nexo.Tests
{
    /// <summary>Cadena Gemini → respaldo → emergencia y rutas de fallo de la voz [V]. Sin red ni API key.</summary>
    public sealed class ElenaReplyChainTests
    {
        private static ElenaResponse Valid(string text = "Sigue por el pasillo, Alex.") =>
            new ElenaResponse { dialogue = text, emotion = "calm", ui_action = "none", trust_level = 60 };

        [Test]
        public void Pick_ValidRemote_UsesGeminiAndMarksModel()
        {
            ElenaResponse r = ElenaReplyResolver.Pick(Valid(), "gemini-3.5-flash-lite", () => Valid("respaldo"), out bool usedFallback);
            Assert.IsFalse(usedFallback);
            Assert.AreEqual("gemini-3.5-flash-lite", r.source);
            Assert.AreEqual("IA: Gemini", ElenaReplyResolver.MarkerText(r.source));
        }

        [Test]
        public void Pick_NullRemote_UsesFallback()
        {
            ElenaResponse r = ElenaReplyResolver.Pick(null, null, () => Valid("respaldo"), out bool usedFallback);
            Assert.IsTrue(usedFallback);
            Assert.AreEqual("respaldo", r.dialogue);
            Assert.AreEqual(ElenaReplyResolver.SourceFallback, r.source);
            Assert.AreEqual("IA: respaldo", ElenaReplyResolver.MarkerText(r.source));
        }

        [Test]
        public void Pick_InvalidRemote_UsesFallback()
        {
            ElenaResponse r = ElenaReplyResolver.Pick(new ElenaResponse { dialogue = "  " }, "x", () => Valid("respaldo"), out bool usedFallback);
            Assert.IsTrue(usedFallback);
            Assert.AreEqual(ElenaReplyResolver.SourceFallback, r.source);
        }

        [Test]
        public void Pick_NoFallbackProvider_NeverSilent()
        {
            ElenaResponse r = ElenaReplyResolver.Pick(null, null, null, out _);
            Assert.IsNotNull(r);
            Assert.IsFalse(string.IsNullOrWhiteSpace(r.dialogue));
            Assert.AreEqual(ElenaReplyResolver.SourceEmergency, r.source);
        }

        [Test]
        public void Pick_FallbackReturnsNullOrThrows_NeverSilent()
        {
            ElenaResponse a = ElenaReplyResolver.Pick(null, null, () => null, out _);
            ElenaResponse b = ElenaReplyResolver.Pick(null, null, () => throw new System.InvalidOperationException("boom"), out _);
            Assert.IsFalse(string.IsNullOrWhiteSpace(a.dialogue));
            Assert.IsFalse(string.IsNullOrWhiteSpace(b.dialogue));
        }

        [Test]
        public void Marker_EmptyForScriptedLines()
        {
            Assert.AreEqual(string.Empty, ElenaReplyResolver.MarkerText(null));
            Assert.AreEqual(string.Empty, ElenaReplyResolver.MarkerText(""));
        }

        [Test]
        public void ModelOrder_Dialog_PrimaryThenFallbacksWithoutDuplicates()
        {
            var order = GeminiModelOrder.Dialog("gemini-3.8-flash", new[] { "gemini-3.5-flash-lite", " ", "gemini-3.8-flash", "gemini-3.5-flash-lite" });
            CollectionAssert.AreEqual(new[] { "gemini-3.8-flash", "gemini-3.5-flash-lite" }, order);
        }

        [Test]
        public void ModelOrder_Speech_ReliableModelFirst()
        {
            var order = GeminiModelOrder.Speech("gemini-2.5-flash", new[] { "gemini-3.5-flash-lite", "gemini-2.5-flash", "gemini-3.8-flash" });
            CollectionAssert.AreEqual(new[] { "gemini-2.5-flash", "gemini-3.5-flash-lite", "gemini-3.8-flash" }, order);
        }

        [Test]
        public void ModelOrder_InvalidModelName_StillFallsBackToNext()
        {
            // El primer modelo inválido no impide que la reserva figure en la cadena
            var order = GeminiModelOrder.Dialog("modelo-inexistente", new[] { "gemini-3.5-flash-lite" });
            Assert.AreEqual(2, order.Count);
            Assert.IsTrue(GeminiThinking.ShouldTryNextModel(404, false));
            Assert.IsTrue(GeminiThinking.ShouldTryNextModel(503, false));
            Assert.IsTrue(GeminiThinking.ShouldTryNextModel(0, true));
            Assert.IsFalse(GeminiThinking.ShouldTryNextModel(400, false));
        }

        [Test]
        public void Voice_TooShortCapture()
        {
            Assert.AreEqual(VoiceOutcome.TooShort, ElenaVoiceRules.ClassifyCapture(null, 0, 16000));
            Assert.AreEqual(VoiceOutcome.TooShort, ElenaVoiceRules.ClassifyCapture(new float[100], 100, 16000));
        }

        [Test]
        public void Voice_SilentCapture_SkipsNetwork()
        {
            Assert.AreEqual(VoiceOutcome.Silent, ElenaVoiceRules.ClassifyCapture(new float[16000], 16000, 16000));
        }

        [Test]
        public void Voice_AudibleCapture_Ok()
        {
            var samples = new float[16000];
            samples[8000] = 0.4f;
            Assert.AreEqual(VoiceOutcome.Ok, ElenaVoiceRules.ClassifyCapture(samples, samples.Length, 16000));
        }

        [Test]
        public void Voice_TranscriptionFailure_IsNotUnderstood()
        {
            Assert.AreEqual(VoiceOutcome.NotUnderstood, ElenaVoiceRules.ClassifyTranscript(null));
            Assert.AreEqual(VoiceOutcome.NotUnderstood, ElenaVoiceRules.ClassifyTranscript("   "));
            Assert.AreEqual(VoiceOutcome.Ok, ElenaVoiceRules.ClassifyTranscript("abre la puerta"));
        }

        [Test]
        public void Voice_MaxLengthIsEightSeconds()
        {
            Assert.AreEqual(8, ElenaVoiceRules.MaxRecordSeconds);
        }
    }
}
