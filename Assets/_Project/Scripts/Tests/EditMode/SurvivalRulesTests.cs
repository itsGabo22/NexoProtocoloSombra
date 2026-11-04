using Nexo.AI.Infected;
using Nexo.Player;
using NUnit.Framework;
using UnityEngine;

namespace Nexo.Tests
{
    /// <summary>Pase de jugabilidad (parte 1): salto, mirada en escondite, puntos de control, curación y daño.</summary>
    public sealed class SurvivalRulesTests
    {
        // ---------- Salto ----------

        [Test]
        public void Jump_AllowedOnlyGroundedFreeStandingAndAfterGrace()
        {
            Assert.IsTrue(JumpRules.CanJump(true, false, false, false, 1f, 0.35f));
            Assert.IsFalse(JumpRules.CanJump(false, false, false, false, 1f, 0.35f), "en el aire");
            Assert.IsFalse(JumpRules.CanJump(true, true, false, false, 1f, 0.35f), "UI/forcejeo/cinemática");
            Assert.IsFalse(JumpRules.CanJump(true, false, true, false, 1f, 0.35f), "agachado");
            Assert.IsFalse(JumpRules.CanJump(true, false, false, true, 1f, 0.35f), "escondido");
            Assert.IsFalse(JumpRules.CanJump(true, false, false, false, 0.1f, 0.35f), "misma pulsación que liberó el control");
        }

        [Test]
        public void Jump_TakeOffSpeedReachesHeight()
        {
            float v = JumpRules.TakeOffSpeed(0.9f, -19.62f);
            float apex = v * v / (2f * 19.62f);
            Assert.AreEqual(0.9f, apex, 1e-3f);
            Assert.AreEqual(0f, JumpRules.TakeOffSpeed(-1f, -19.62f));
        }

        // ---------- Mirada en el escondite ----------

        [Test]
        public void LookLimits_ClampToHidingRange()
        {
            Assert.AreEqual(70f, LookLimits.Clamp(120f, LookLimits.HidingYaw));
            Assert.AreEqual(-70f, LookLimits.Clamp(-95f, LookLimits.HidingYaw));
            Assert.AreEqual(-35f, LookLimits.Clamp(-60f, LookLimits.HidingPitch));
            Assert.AreEqual(12f, LookLimits.Clamp(12f, LookLimits.HidingPitch));
        }

        // ---------- Puntos de control ----------

        [Test]
        public void Checkpoints_FloorStartIsFirstCheckpoint()
        {
            var log = new CheckpointLog();
            log.Reset(new Vector3(1f, 0f, 2f), 90f, 0f);
            Assert.IsTrue(log.TryChoose(10f, 2.5f, out Vector3 p, out float yaw));
            Assert.AreEqual(new Vector3(1f, 0f, 2f), p);
            Assert.AreEqual(90f, yaw);
        }

        [Test]
        public void Checkpoints_RecentSafeGroundIgnoredUntilOldEnough()
        {
            var log = new CheckpointLog();
            log.Reset(Vector3.zero, 0f, 0f);
            log.SetCheckpoint(new Vector3(5f, 0f, 0f), 0f, 10f, "control 1");
            log.AddSafeGround(new Vector3(9f, 0f, 0f), 0f, 19f);   // demasiado reciente al caer en t = 20
            log.TryChoose(20f, 2.5f, out Vector3 p, out _);
            Assert.AreEqual(new Vector3(5f, 0f, 0f), p, "el borde del que acaba de caer no sirve");

            log.AddSafeGround(new Vector3(7f, 0f, 0f), 0f, 15f);   // posterior al control y con antigüedad suficiente
            log.TryChoose(20f, 2.5f, out p, out _);
            Assert.AreEqual(new Vector3(7f, 0f, 0f), p);
        }

        [Test]
        public void Checkpoints_NewerCheckpointBeatsOlderSafeGround()
        {
            var log = new CheckpointLog();
            log.Reset(Vector3.zero, 0f, 0f);
            log.AddSafeGround(new Vector3(3f, 0f, 0f), 0f, 5f);
            log.SetCheckpoint(new Vector3(8f, 0f, 0f), 45f, 12f, "control 2");
            log.TryChoose(30f, 2.5f, out Vector3 p, out float yaw);
            Assert.AreEqual(new Vector3(8f, 0f, 0f), p);
            Assert.AreEqual(45f, yaw);
            Assert.AreEqual("control 2", log.LastLabel);
        }

        [Test]
        public void Checkpoints_ResetForgetsSafeGround()
        {
            var log = new CheckpointLog();
            log.Reset(Vector3.zero, 0f, 0f);
            log.AddSafeGround(new Vector3(3f, 0f, 0f), 0f, 5f);
            log.Reset(new Vector3(-4f, 0f, 0f), 0f, 100f);
            log.TryChoose(200f, 2.5f, out Vector3 p, out _);
            Assert.AreEqual(new Vector3(-4f, 0f, 0f), p);
        }

        [Test]
        public void Fall_DetectedBelowThreshold()
        {
            Assert.IsTrue(CheckpointLog.IsFallen(17.9f, 24f, 6f));
            Assert.IsFalse(CheckpointLog.IsFallen(18.1f, 24f, 6f));
            Assert.IsFalse(CheckpointLog.IsFallen(25f, 24f, 6f), "un salto no es una caída");
        }

        // ---------- Botiquín ----------

        [Test]
        public void Heal_AddsOnePointAndClearsHits()
        {
            HealthRules.Heal(2, 3, 3, 1, out int points, out int hits);
            Assert.AreEqual(3, points);
            Assert.AreEqual(0, hits);
        }

        [Test]
        public void Heal_NeverExceedsMax()
        {
            HealthRules.Heal(3, 1, 3, 1, out int points, out int hits);
            Assert.AreEqual(3, points);
            Assert.AreEqual(0, hits);
            HealthRules.Heal(1, 0, 3, 5, out points, out _);
            Assert.AreEqual(3, points);
        }

        // ---------- Daño a infectados ----------

        [Test]
        public void Damage_HeadshotIsAlwaysLethal()
        {
            for (int health = 1; health <= 10; health++)
                Assert.GreaterOrEqual(InfectedDamageRules.ShotDamage(HitZone.Head, health), health);
        }

        [Test]
        public void Damage_StandardTakesThreeBodyShots_CrawlerTwo()
        {
            Assert.AreEqual(3, InfectedDamageRules.BodyShotsToKill(InfectedBehavior.Patroller, 0));
            Assert.AreEqual(2, InfectedDamageRules.BodyShotsToKill(InfectedBehavior.Crawler, 0));
            Assert.AreEqual(5, InfectedDamageRules.BodyShotsToKill(InfectedBehavior.Patroller, 5), "el override del punto manda");
        }

        [Test]
        public void Damage_LegsAndTorsoDealOne()
        {
            Assert.AreEqual(1, InfectedDamageRules.ShotDamage(HitZone.Legs, 3));
            Assert.AreEqual(1, InfectedDamageRules.ShotDamage(HitZone.Torso, 3));
        }

        [Test]
        public void Damage_KnifeStealthIsLethal_FrontalDealsTwo()
        {
            Assert.AreEqual(3, InfectedDamageRules.KnifeHit(true, 3));
            Assert.AreEqual(2, InfectedDamageRules.KnifeHit(false, 3));
        }
    }
}
