using Nexo.UI;
using NUnit.Framework;

namespace Nexo.Tests
{
    /// <summary>
    /// Estados y textos del panel de arma del HUD (AmmoDisplayState).
    /// </summary>
    [TestFixture]
    public sealed class AmmoDisplayStateTests
    {
        [TestCase(7, 21, AmmoStatus.Normal)]
        [TestCase(3, 0, AmmoStatus.Normal)]
        [TestCase(2, 14, AmmoStatus.Low)]
        [TestCase(1, 0, AmmoStatus.Low)]
        [TestCase(0, 7, AmmoStatus.Empty)]
        [TestCase(0, 0, AmmoStatus.Depleted)]
        public void Resolve_ReturnsStateForMagazineAndReserve(int magazine, int reserve, AmmoStatus expected)
        {
            Assert.AreEqual(expected, AmmoDisplayState.Resolve(magazine, reserve, 7, false));
        }

        [TestCase(0, 0)]
        [TestCase(0, 7)]
        [TestCase(7, 21)]
        public void Resolve_ReloadingWinsOverEveryOtherState(int magazine, int reserve)
        {
            Assert.AreEqual(AmmoStatus.Reloading, AmmoDisplayState.Resolve(magazine, reserve, 7, true));
        }

        [Test]
        public void Resolve_TreatsNegativeValuesAsEmpty()
        {
            Assert.AreEqual(AmmoStatus.Depleted, AmmoDisplayState.Resolve(-3, -1, 7, false));
            Assert.AreEqual(AmmoStatus.Empty, AmmoDisplayState.Resolve(-1, 5, 7, false));
        }

        [Test]
        public void Counter_FormatsMagazineSlashReserve()
        {
            Assert.AreEqual("7 / 21", AmmoDisplayState.Counter(7, 21, 7));
            Assert.AreEqual("0 / 0", AmmoDisplayState.Counter(0, 0, 7));
        }

        [Test]
        public void Counter_ClampsOutOfRangeValues()
        {
            Assert.AreEqual("7 / 0", AmmoDisplayState.Counter(12, -4, 7));
            Assert.AreEqual("0 / 3", AmmoDisplayState.Counter(-2, 3, 7));
        }

        [TestCase(AmmoStatus.Normal, "")]
        [TestCase(AmmoStatus.Low, "MUNICIÓN BAJA")]
        [TestCase(AmmoStatus.Empty, "RECARGAR [R]")]
        [TestCase(AmmoStatus.Depleted, "SIN MUNICIÓN")]
        [TestCase(AmmoStatus.Reloading, "RECARGANDO…")]
        public void StatusText_IsSpanishAndNamesTheReloadKey(AmmoStatus status, string expected)
        {
            Assert.AreEqual(expected, AmmoDisplayState.StatusText(status));
        }

        [Test]
        public void StatusClass_IsEmptyOnlyForNormal()
        {
            Assert.AreEqual(string.Empty, AmmoDisplayState.StatusClass(AmmoStatus.Normal));
            Assert.AreEqual("hud-weapon--low", AmmoDisplayState.StatusClass(AmmoStatus.Low));
            Assert.AreEqual("hud-weapon--empty", AmmoDisplayState.StatusClass(AmmoStatus.Empty));
            Assert.AreEqual("hud-weapon--depleted", AmmoDisplayState.StatusClass(AmmoStatus.Depleted));
            Assert.AreEqual("hud-weapon--reloading", AmmoDisplayState.StatusClass(AmmoStatus.Reloading));
        }

        [TestCase(7, 7)]
        [TestCase(0, 1)]
        [TestCase(-5, 1)]
        [TestCase(30, AmmoDisplayState.MaxCells)]
        public void CellCount_StaysBetweenOneAndMax(int magazineSize, int expected)
        {
            Assert.AreEqual(expected, AmmoDisplayState.CellCount(magazineSize));
        }

        [TestCase(5, 7, 5)]
        [TestCase(0, 7, 0)]
        [TestCase(9, 7, 7)]
        [TestCase(20, 30, AmmoDisplayState.MaxCells)]
        public void FilledCells_NeverExceedsDrawnCells(int magazine, int magazineSize, int expected)
        {
            Assert.AreEqual(expected, AmmoDisplayState.FilledCells(magazine, magazineSize));
        }

        [Test]
        public void PickupText_ShowsPlusRoundsOrNothing()
        {
            Assert.AreEqual("+7", AmmoDisplayState.PickupText(7));
            Assert.AreEqual(string.Empty, AmmoDisplayState.PickupText(0));
            Assert.AreEqual(string.Empty, AmmoDisplayState.PickupText(-2));
        }
    }
}
