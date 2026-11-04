using Nexo.Narrative;
using Nexo.Player;
using NUnit.Framework;
using UnityEngine;

namespace Nexo.Tests
{
    /// <summary>Pase de jugabilidad (partes 5 y 6): empujón, cuchillo y diario de notas.</summary>
    public sealed class MeleeAndNotesTests
    {
        [Test]
        public void Shove_CooldownIsAboutOneAndAHalfSeconds()
        {
            Assert.IsFalse(MeleeRules.Ready(10f, 9f, MeleeRules.ShoveCooldown));
            Assert.IsTrue(MeleeRules.Ready(10.6f, 9f, MeleeRules.ShoveCooldown));
            Assert.AreEqual(0.5f, MeleeRules.CooldownProgress(9.75f, 9f, 1.5f), 1e-4f);
        }

        [Test]
        public void Cone_OnlyTargetsInFrontAndInRange()
        {
            Assert.IsTrue(MeleeRules.InCone(Vector3.zero, Vector3.forward, new Vector3(0.3f, 0f, 1.5f), 2.1f, 55f));
            Assert.IsFalse(MeleeRules.InCone(Vector3.zero, Vector3.forward, new Vector3(0f, 0f, -1f), 2.1f, 55f), "detrás");
            Assert.IsFalse(MeleeRules.InCone(Vector3.zero, Vector3.forward, new Vector3(0f, 0f, 3f), 2.1f, 55f), "lejos");
        }

        [Test]
        public void Knife_StealthOnlyFromBehindAndUnaware()
        {
            Vector3 infected = Vector3.zero, facing = Vector3.forward;
            Assert.IsTrue(MeleeRules.IsStealthTakedown(false, facing, infected, new Vector3(0f, 0f, -1f)), "por la espalda");
            Assert.IsFalse(MeleeRules.IsStealthTakedown(false, facing, infected, new Vector3(0f, 0f, 1f)), "de frente");
            Assert.IsFalse(MeleeRules.IsStealthTakedown(true, facing, infected, new Vector3(0f, 0f, -1f)), "ya te ha visto");
        }

        [Test]
        public void Journal_AddsOncePerNoteAndClears()
        {
            NoteJournal.Clear();
            Assert.IsTrue(NoteJournal.Add(6, "Dra. Rivas", "N1: no abráis la sala B."));
            Assert.IsFalse(NoteJournal.Add(6, "Dra. Rivas", "N1: no abráis la sala B."), "releer no duplica");
            Assert.IsTrue(NoteJournal.Add(12, "J. Román", "Otra nota."));
            Assert.AreEqual(2, NoteJournal.Count);
            Assert.IsTrue(NoteJournal.Contains(12, "J. Román", "Otra nota."));
            Assert.IsFalse(NoteJournal.Add(1, "X", "   "));
            NoteJournal.Clear();
            Assert.AreEqual(0, NoteJournal.Count);
        }
    }
}
