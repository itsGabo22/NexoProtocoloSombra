using Nexo.AI.Elena;
using Nexo.UI;
using Nexo.UI.Menus;
using NUnit.Framework;

namespace Nexo.Tests
{
    /// <summary>
    /// Textos en español del HUD: emociones de Elena, nombres de puertas, título de la terminal y ayuda de controles.
    /// </summary>
    [TestFixture]
    public sealed class HudLabelsTests
    {
        [TestCase(ElenaEmotion.Calm, "CALMA")]
        [TestCase(ElenaEmotion.Happy, "ALEGRE")]
        [TestCase(ElenaEmotion.Scared, "ASUSTADA")]
        [TestCase(ElenaEmotion.Urgent, "URGENTE")]
        [TestCase(ElenaEmotion.Sad, "TRISTE")]
        [TestCase(ElenaEmotion.Whisper, "SUSURRO")]
        public void EmotionLabel_IsSpanish(ElenaEmotion emotion, string expected)
        {
            Assert.AreEqual(expected, ElenaEmotionLabels.ToSpanish(emotion));
        }

        [Test]
        public void EmotionLabel_CoversEveryEnumMember()
        {
            foreach (ElenaEmotion emotion in System.Enum.GetValues(typeof(ElenaEmotion)))
            {
                string label = ElenaEmotionLabels.ToSpanish(emotion);
                Assert.AreNotEqual(emotion.ToString().ToUpperInvariant(), label, $"{emotion} se muestra en inglés.");
            }
        }

        [TestCase("F12_Door_Stairs", "la puerta de las escaleras")]
        [TestCase("F06_Door_Stairs", "la puerta de las escaleras")]
        [TestCase("F06_Door_Lab", "el laboratorio")]
        [TestCase("F12_Door_Meeting", "la sala de reuniones")]
        [TestCase(" f12_door_stairs ", "la puerta de las escaleras")]
        [TestCase("F99_Door_Unknown", DoorDisplayNames.UnknownDestination)]
        [TestCase("", DoorDisplayNames.UnknownDestination)]
        [TestCase(null, DoorDisplayNames.UnknownDestination)]
        public void DoorDestination_IsSpanish(string doorId, string expected)
        {
            Assert.AreEqual(expected, DoorDisplayNames.Destination(doorId));
        }

        [TestCase("F12_Door_Stairs", "Puerta de las escaleras")]
        [TestCase("F06_Door_Lab", "Puerta del laboratorio")]
        [TestCase("F12_Door_Meeting", "Sala de reuniones")]
        [TestCase("F01_BioScanner", DoorDisplayNames.UnknownLabel)]
        public void DoorLabel_NeverShowsTheTechnicalId(string doorId, string expected)
        {
            string label = DoorDisplayNames.Label(doorId);
            Assert.AreEqual(expected, label);
            StringAssert.DoesNotContain("_", label);
        }

        [TestCase(12, "TERMINAL DE SEGURIDAD · PISO 12")]
        [TestCase(6, "TERMINAL DE SEGURIDAD · PISO 6")]
        public void TerminalTitle_IsSpanishWithFloor(int floor, string expected)
        {
            Assert.AreEqual(expected, FloorLabels.TerminalTitle(floor));
        }

        [Test]
        public void TerminalTitle_FallsBackToFloor12()
        {
            Assert.AreEqual("TERMINAL DE SEGURIDAD · PISO 12", FloorLabels.TerminalTitle(FloorLabels.Resolve(0, 0)));
        }

        [Test]
        public void ControlsHint_IsShownOncePerSession()
        {
            ControlsHint.ResetSession();
            Assert.IsTrue(ControlsHint.TryConsume());
            Assert.IsFalse(ControlsHint.TryConsume(), "Un reinicio de piso no debe repetir la ayuda.");

            ControlsHint.ResetSession();
            Assert.IsTrue(ControlsHint.TryConsume());
            ControlsHint.ResetSession();
        }

        [Test]
        public void ControlsHint_ListsOnlyVerifiedKeys()
        {
            StringAssert.Contains("R recargar", ControlsHint.Text);
            StringAssert.Contains("clic der. apuntar", ControlsHint.Text);
            StringAssert.Contains("F visión térmica", ControlsHint.Text);
            StringAssert.Contains("V hablar con Elena", ControlsHint.Text);
            StringAssert.Contains("C agacharse", ControlsHint.Text);
        }
    }
}
