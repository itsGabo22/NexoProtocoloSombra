using Nexo.UI;
using NUnit.Framework;

namespace Nexo.Tests
{
    /// <summary>
    /// HudTextSanitizer: ningún id interno llega a la pantalla, se respete o no el sustantivo que lo precede.
    /// </summary>
    [TestFixture]
    public sealed class HudTextSanitizerTests
    {
        [TestCase("Acceso denegado: F12_Door_Stairs requiere desbloqueo de Elena.",
                  "Acceso denegado: la puerta de las escaleras requiere desbloqueo de Elena.")]
        [TestCase("Puerta F12_Door_Stairs desbloqueada remotamente.",
                  "Puerta de las escaleras desbloqueada remotamente.")]
        [TestCase("Puerta F06_Door_Lab desbloqueada remotamente.",
                  "Puerta del laboratorio desbloqueada remotamente.")]
        [TestCase("Puerta F12_Door_Meeting desbloqueada remotamente.",
                  "Puerta de la sala de reuniones desbloqueada remotamente.")]
        [TestCase("F12_Door_Stairs: apertura denegada. Presencia hostil activa en el piso.",
                  "La puerta de las escaleras: apertura denegada. Presencia hostil activa en el piso.")]
        [TestCase("F06_Door_Stairs: bloqueo de protocolo activo. Solicita apertura a Elena.",
                  "La puerta de las escaleras: bloqueo de protocolo activo. Solicita apertura a Elena.")]
        public void Humanize_RewritesRealGameMessages(string input, string expected)
        {
            Assert.AreEqual(expected, HudTextSanitizer.Humanize(input));
        }

        [TestCase("Ve hacia F01_BioScanner.", "Ve hacia el escáner maestro.")]
        [TestCase("Estás en F12_Cubicles_East, sigue a F12_ServerRoom.", "Estás en los cubículos del ala este, sigue a la sala de servidores.")]
        [TestCase("Usa F12_Terminal_Security", "Usa la terminal de seguridad")]
        public void Humanize_TranslatesRoomsAndObjects(string input, string expected)
        {
            Assert.AreEqual(expected, HudTextSanitizer.Humanize(input));
        }

        [Test]
        public void Humanize_ReplacesSeveralIdsInOneSentence()
        {
            Assert.AreEqual(
                "Abre la puerta de las escaleras y luego el laboratorio.",
                HudTextSanitizer.Humanize("Abre F12_Door_Stairs y luego F06_Door_Lab."));
        }

        [TestCase("(F12_Door_Stairs)", "(La puerta de las escaleras)")]
        [TestCase("¿F06_Door_Lab?", "¿El laboratorio?")]
        [TestCase("Corre: F12_Door_Stairs!", "Corre: la puerta de las escaleras!")]
        [TestCase("«F12_Door_Stairs», dijo.", "«La puerta de las escaleras», dijo.")]
        public void Humanize_KeepsSurroundingPunctuation(string input, string expected)
        {
            Assert.AreEqual(expected, HudTextSanitizer.Humanize(input));
        }

        [TestCase("Acceso denegado: F99_Door_Vault requiere desbloqueo.", "Acceso denegado: una puerta requiere desbloqueo.")]
        [TestCase("Puerta F99_Door_Vault desbloqueada.", "Puerta desbloqueada.")]
        [TestCase("Reúnete conmigo en F12_Rooftop.", "Reúnete conmigo en ese lugar.")]
        [TestCase("Main_Door_Lobby cerrada.", "Una puerta cerrada.")]
        public void Humanize_UsesGenericNamesForUnknownIds(string input, string expected)
        {
            Assert.AreEqual(expected, HudTextSanitizer.Humanize(input));
        }

        [TestCase("Señal vital interrumpida. Reiniciando protocolo del Piso 12...")]
        [TestCase("(Te golpean: 1/4)")]
        [TestCase("J. RAMOS · SEGURIDAD")]
        [TestCase("snake_case sin id de juego")]
        [TestCase("")]
        public void Humanize_LeavesTextWithoutIdsUnchanged(string input)
        {
            Assert.AreEqual(input, HudTextSanitizer.Humanize(input));
        }

        [Test]
        public void Humanize_ReturnsNullForNull()
        {
            Assert.IsNull(HudTextSanitizer.Humanize(null));
        }

        [Test]
        public void Humanize_DoesNotMutateTheOriginal()
        {
            string original = "Puerta F12_Door_Stairs desbloqueada remotamente.";
            string shown = HudTextSanitizer.Humanize(original);
            Assert.AreEqual("Puerta F12_Door_Stairs desbloqueada remotamente.", original);
            Assert.AreNotEqual(original, shown);
        }

        [Test]
        public void Destination_FixesTheFloor1SafeRouteTarget()
        {
            // El fallback del Piso 1 (SAFE_PATH) ordena HIGHLIGHT_SAFE_ROUTE hacia F01_BioScanner
            Assert.AreEqual("el escáner maestro", DoorDisplayNames.Destination("F01_BioScanner"));
            Assert.AreEqual(DoorDisplayNames.UnknownPlace, DoorDisplayNames.Destination("F12_Rooftop"));
        }
    }
}
