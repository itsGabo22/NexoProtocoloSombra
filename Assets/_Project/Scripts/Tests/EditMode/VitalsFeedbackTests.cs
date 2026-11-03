using Nexo.UI;
using Nexo.UI.Menus;
using NUnit.Framework;

namespace Nexo.Tests
{
    /// <summary>
    /// Feedback de daño del HUD (VitalsFeedback) y conversión del volumen general (AudioPrefs).
    /// </summary>
    [TestFixture]
    public sealed class VitalsFeedbackTests
    {
        [TestCase("100% ILESO", 100)]
        [TestCase("67% HERIDO", 67)]
        [TestCase("8% CRÍTICO", 8)]
        [TestCase("  0% CRÍTICO", 0)]
        public void TryParsePercent_ReadsPlayerHealthText(string text, int expected)
        {
            Assert.IsTrue(VitalsFeedback.TryParsePercent(text, out int percent));
            Assert.AreEqual(expected, percent);
        }

        [TestCase("98.4% INFECTADO")]
        [TestCase("NO INFECTADO")]
        [TestCase("")]
        [TestCase(null)]
        [TestCase("100 ILESO")]
        public void TryParsePercent_RejectsOtherTexts(string text)
        {
            Assert.IsFalse(VitalsFeedback.TryParsePercent(text, out _));
        }

        [Test]
        public void IsDamage_OnlyWhenPercentDrops()
        {
            Assert.IsTrue(VitalsFeedback.IsDamage(100, 92));
            Assert.IsFalse(VitalsFeedback.IsDamage(92, 92));
            Assert.IsFalse(VitalsFeedback.IsDamage(33, 100), "Restaurar la vida no es daño.");
            Assert.IsFalse(VitalsFeedback.IsDamage(VitalsFeedback.Unknown, 50), "El primer valor no es daño.");
        }

        [TestCase(100, false)]
        [TestCase(67, false)]
        [TestCase(35, false)]
        [TestCase(34, true)]
        [TestCase(8, true)]
        [TestCase(0, true)]
        public void IsLowHealth_FromLastHealthPoint(int percent, bool expected)
        {
            Assert.AreEqual(expected, VitalsFeedback.IsLowHealth(percent));
        }

        [TestCase(100, 1f)]
        [TestCase(50, 0.5f)]
        [TestCase(0, 0f)]
        [TestCase(150, 1f)]
        [TestCase(-20, 0f)]
        public void MasterVolume_MapsPercentToListenerVolume(int percent, float expected)
        {
            Assert.AreEqual(expected, AudioPrefs.ToListenerVolume(percent), 0.0001f);
        }
    }
}
