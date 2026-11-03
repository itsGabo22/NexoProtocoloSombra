using Nexo.UI.Menus;
using NUnit.Framework;

namespace Nexo.Tests
{
    [TestFixture]
    public sealed class FloorLabelsTests
    {
        [Test]
        public void Resolve_PrefersFloorMetadata()
        {
            Assert.AreEqual(6, FloorLabels.Resolve(6, 1));
        }

        [Test]
        public void Resolve_FallsBackToTrackedFloor_WhenNoMetadata()
        {
            Assert.AreEqual(1, FloorLabels.Resolve(0, 1));
        }

        [Test]
        public void Resolve_FallsBackToDefaultFloor_WhenNothingKnown()
        {
            Assert.AreEqual(FloorLabels.DefaultFloor, FloorLabels.Resolve(0, 0));
            Assert.AreEqual(12, FloorLabels.DefaultFloor);
        }

        [TestCase(12, "PISO 12 · OFICINAS")]
        [TestCase(6, "PISO 6 · LABORATORIOS")]
        [TestCase(1, "PISO 1 · VESTÍBULO")]
        [TestCase(3, "PISO 3")]
        public void Location_IncludesZoneName(int floor, string expected)
        {
            Assert.AreEqual(expected, FloorLabels.Location(floor));
        }

        [Test]
        public void Restarted_ShowsCurrentFloor()
        {
            Assert.AreEqual("PISO 6 REINICIADO", FloorLabels.Restarted(6));
            Assert.AreEqual("PISO 1 REINICIADO", FloorLabels.Restarted(1));
        }

        [Test]
        public void Format_ReplacesFloorAndZone()
        {
            Assert.AreEqual("TERMINAL PISO 6", FloorLabels.Format("TERMINAL PISO {0}", 6));
            Assert.AreEqual("Reiniciando protocolo del Piso 1 (VESTÍBULO)...", FloorLabels.Format("Reiniciando protocolo del Piso {0} ({1})...", 1));
            Assert.AreEqual("PISO 6 · LABORATORIOS", FloorLabels.Format(string.Empty, 6));
        }
    }
}
