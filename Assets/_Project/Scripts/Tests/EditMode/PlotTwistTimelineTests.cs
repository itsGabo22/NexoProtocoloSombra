using System;
using Nexo.Narrative;
using NUnit.Framework;

namespace Nexo.Tests
{
    [TestFixture]
    public sealed class PlotTwistTimelineTests
    {
        [Test]
        public void Steps_AreSortedByOffset_AndTotalDurationIsLastStepEnd()
        {
            var steps = new[]
            {
                new TwistStep(3.5f, TwistAction.ElenaLine, 3f, 0),
                new TwistStep(0f, TwistAction.StartTwist),
                new TwistStep(2f, TwistAction.LightRamp, 1.5f),
                new TwistStep(0.8f, TwistAction.HudCorruption),
                new TwistStep(10.5f, TwistAction.ShowCredits, 0.5f)
            };

            var timeline = new TwistTimeline(steps, reduceMotion: false);

            CollectionAssert.AreEqual(
                new[] { TwistAction.StartTwist, TwistAction.HudCorruption, TwistAction.LightRamp, TwistAction.ElenaLine, TwistAction.ShowCredits },
                ActionsOf(timeline));
            for (int i = 1; i < timeline.Steps.Count; i++)
            {
                Assert.LessOrEqual(timeline.Steps[i - 1].Offset, timeline.Steps[i].Offset);
            }
            Assert.AreEqual(timeline.Steps[timeline.Steps.Count - 1].End, timeline.TotalDuration, 1e-4f);
            Assert.AreEqual(11f, timeline.TotalDuration, 1e-4f);
        }

        [Test]
        public void ReduceMotion_LightRampHasZeroDuration_AndOrderIsUnchanged()
        {
            TwistTimeline normal = TwistTimeline.CreateDefault(3, 3.5f, 1.5f, 4f, reduceMotion: false);
            TwistTimeline reduced = TwistTimeline.CreateDefault(3, 3.5f, 1.5f, 4f, reduceMotion: true);

            CollectionAssert.AreEqual(ActionsOf(normal), ActionsOf(reduced));
            Assert.AreEqual(1.5f, FindRamp(normal).Duration, 1e-4f);
            Assert.AreEqual(0f, FindRamp(reduced).Duration, 1e-4f);
            Assert.AreEqual(FindRamp(normal).Offset, FindRamp(reduced).Offset, 1e-4f);

            // La corrupción del HUD se mantiene con reducir movimiento
            CollectionAssert.Contains(ActionsOf(reduced), TwistAction.HudCorruption);
        }

        [Test]
        public void EmptyList_ReturnsZeroDuration()
        {
            var timeline = new TwistTimeline(Array.Empty<TwistStep>(), reduceMotion: false);

            Assert.AreEqual(0, timeline.Steps.Count);
            Assert.AreEqual(0f, timeline.TotalDuration);
        }

        [Test]
        public void NegativeOffset_IsRejected()
        {
            // Decisión documentada en TwistTimeline: los offsets negativos se rechazan, no se recortan
            var steps = new[] { new TwistStep(-0.5f, TwistAction.StartTwist) };

            Assert.Throws<ArgumentOutOfRangeException>(() => new TwistTimeline(steps, reduceMotion: false));
        }

        [Test]
        public void DefaultTimeline_MatchesTheGameSequence()
        {
            TwistTimeline timeline = TwistTimeline.CreateDefault(3, 3.5f, 1.5f, 4f, reduceMotion: false);

            CollectionAssert.AreEqual(
                new[]
                {
                    TwistAction.StartTwist, TwistAction.HudCorruption, TwistAction.LightRamp,
                    TwistAction.ElenaLine, TwistAction.ElenaLine, TwistAction.ElenaLine, TwistAction.ShowCredits
                },
                ActionsOf(timeline));
            Assert.AreEqual(0.8f, timeline.Steps[1].Offset, 1e-4f);
            Assert.AreEqual(3.5f, timeline.Steps[3].Offset, 1e-4f);
            // 3.5 + 3 × 3.5 = 14 s de líneas; créditos 4 s después
            Assert.AreEqual(18f, timeline.TotalDuration, 1e-4f);
        }

        private static TwistAction[] ActionsOf(TwistTimeline timeline)
        {
            var actions = new TwistAction[timeline.Steps.Count];
            for (int i = 0; i < actions.Length; i++) actions[i] = timeline.Steps[i].Action;
            return actions;
        }

        private static TwistStep FindRamp(TwistTimeline timeline)
        {
            foreach (TwistStep step in timeline.Steps)
            {
                if (step.Action == TwistAction.LightRamp) return step;
            }
            Assert.Fail("La línea de tiempo no contiene la rampa de luces.");
            return default;
        }
    }
}
