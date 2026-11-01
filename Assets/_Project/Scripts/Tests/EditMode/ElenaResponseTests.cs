using Nexo.AI.Elena;
using NUnit.Framework;

namespace Nexo.Tests
{
    [TestFixture]
    public sealed class ElenaResponseTests
    {
        [Test]
        public void ValidPayload_AcceptsAndNormalizesCorrectly()
        {
            var response = new ElenaResponse
            {
                dialogue = "Alex, todo despejado hacia las escaleras.",
                emotion = "CALM",
                ui_action = "HIGHLIGHT_SAFE_ROUTE",
                target_id = "",
                trust_level = 150 // debe clampearse a 100
            };

            bool isValid = response.IsValid(out string error);

            Assert.IsTrue(isValid, $"La respuesta válida fue rechazada con error: {error}");
            Assert.AreEqual(100, response.trust_level);
            Assert.AreEqual(ElenaEmotion.Calm, response.ParsedEmotion);
            Assert.AreEqual(ElenaUiAction.HighlightSafeRoute, response.ParsedAction);
        }

        [Test]
        public void EmptyDialogue_RejectsPayload()
        {
            var response = new ElenaResponse
            {
                dialogue = "",
                emotion = "scared",
                ui_action = "none"
            };

            bool isValid = response.IsValid(out string error);

            Assert.IsFalse(isValid);
            Assert.IsNotEmpty(error);
        }

        [Test]
        public void UnknownUiAction_RejectsPayload()
        {
            var response = new ElenaResponse
            {
                dialogue = "Corriendo...",
                emotion = "urgent",
                ui_action = "SUPER_LASER_ATTACK"
            };

            bool isValid = response.IsValid(out string error);

            Assert.IsFalse(isValid);
            Assert.IsTrue(error.Contains("desconocida"));
        }

        [Test]
        public void ElenaJsonTryParse_ExtractsFromMarkdownFencedJson()
        {
            string markdownFenced = "```json\n{\n  \"dialogue\": \"Puerta abierta.\",\n  \"emotion\": \"happy\",\n  \"ui_action\": \"UNLOCK_DOOR\",\n  \"target_id\": \"F12_Door_Stairs\",\n  \"trust_level\": 70\n}\n```";

            bool parsed = ElenaJson.TryParse(markdownFenced, out ElenaResponse response, out string error);

            Assert.IsTrue(parsed, $"Fallo parseando Markdown JSON: {error}");
            Assert.IsNotNull(response);
            Assert.AreEqual("Puerta abierta.", response.dialogue);
            Assert.AreEqual(ElenaEmotion.Happy, response.ParsedEmotion);
            Assert.AreEqual(ElenaUiAction.UnlockDoor, response.ParsedAction);
            Assert.AreEqual("F12_Door_Stairs", response.target_id);
            Assert.AreEqual(70, response.trust_level);
        }

        [TestCase("TOGGLE_HALLWAY_LIGHTS", ElenaUiAction.ToggleHallwayLights)]
        [TestCase("SHOW_HINT", ElenaUiAction.ShowHint)]
        [TestCase("PING_ENEMY_RADAR", ElenaUiAction.PingEnemyRadar)]
        [TestCase("NONE", ElenaUiAction.None)]
        public void PromptActions_AreAllRecognized(string rawAction, ElenaUiAction expected)
        {
            var response = new ElenaResponse { dialogue = "Hola, Alex.", ui_action = rawAction };

            Assert.IsTrue(response.IsValid(out string error), error);
            Assert.AreEqual(expected, response.ParsedAction);
        }

        [Test]
        public void GlitchIntensityAndTarget_AreNormalized()
        {
            var response = new ElenaResponse { dialogue = "Hola.", target_id = "  F12_Door_Stairs ", glitch_intensity = 3.5f };

            Assert.IsTrue(response.IsValid(out _));
            Assert.AreEqual(1f, response.glitch_intensity);
            Assert.AreEqual("F12_Door_Stairs", response.target_id);
        }
    }
}
