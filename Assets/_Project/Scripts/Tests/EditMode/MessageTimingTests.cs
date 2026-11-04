using Nexo.AI.Elena;
using Nexo.UI;
using NUnit.Framework;

namespace Nexo.Tests
{
    /// <summary>Pase de jugabilidad (parte 2): tiempos de lectura, cola de subtítulos y claves de voz de Elena.</summary>
    public sealed class MessageTimingTests
    {
        [Test]
        public void Duration_FollowsFormulaInsideClamp()
        {
            // 1,4 + 0,055 × 100 = 6,9 s
            Assert.AreEqual(6.9f, MessageTiming.Seconds(100, 0f, 0), 1e-4f);
        }

        [Test]
        public void Duration_ShortLinesUseMinimum_LongLinesCapAtEleven()
        {
            Assert.AreEqual(3.5f, MessageTiming.Seconds(5, 0f, 0), 1e-4f);
            Assert.AreEqual(11f, MessageTiming.Seconds(400, 0f, 0), 1e-4f);
        }

        [Test]
        public void Duration_CoversVoiceClipPlusTail()
        {
            Assert.AreEqual(9.3f, MessageTiming.Seconds(20, 8.5f, 0), 1e-4f);
            Assert.AreEqual(6.9f, MessageTiming.Seconds(100, 2f, 0), 1e-4f, "una voz corta no acorta la lectura");
        }

        [Test]
        public void Duration_SlowReadingIsLonger()
        {
            Assert.AreEqual(6.9f * MessageTiming.SlowFactor, MessageTiming.Seconds(100, 0f, 1), 1e-4f);
            Assert.Greater(MessageTiming.MinimumSeconds(0f, 1), MessageTiming.MinimumSeconds(0f, 0));
        }

        [Test]
        public void Minimum_NeverShorterThanVoice()
        {
            Assert.AreEqual(3.5f, MessageTiming.MinimumSeconds(0f, 0), 1e-4f);
            Assert.AreEqual(5.8f, MessageTiming.MinimumSeconds(5f, 0), 1e-4f);
        }

        private static SubtitleQueue.Entry E(string text, float duration = 6f, float min = 3.5f, int priority = 0)
        {
            return new SubtitleQueue.Entry { Text = text, Duration = duration, Minimum = min, Priority = priority };
        }

        [Test]
        public void Queue_NewLinesWaitForCurrent()
        {
            var q = new SubtitleQueue();
            Assert.IsTrue(q.Push(E("A"), 0f));
            Assert.IsFalse(q.Push(E("B"), 1f), "B no corta a A");
            Assert.AreEqual("A", q.Current.Text);
            Assert.IsFalse(q.Tick(3f), "A sigue antes de su mínimo");
            Assert.IsTrue(q.Tick(3.5f), "con cola, A cede al cumplir su mínimo");
            Assert.AreEqual("B", q.Current.Text);
            Assert.IsFalse(q.Tick(8f));
            Assert.IsTrue(q.Tick(9.5f), "sin cola, B dura su tiempo completo");
            Assert.IsFalse(q.HasCurrent);
        }

        [Test]
        public void Queue_UrgentLinePreempts()
        {
            var q = new SubtitleQueue();
            q.Push(E("A"), 0f);
            Assert.IsTrue(q.Push(E("(Te golpean: 1/4)", 3.5f, 3.5f, SubtitlePriority.Urgent), 0.5f));
            Assert.AreEqual("(Te golpean: 1/4)", q.Current.Text);
        }

        [Test]
        public void Queue_IgnoresDuplicatesAndIsBounded()
        {
            var q = new SubtitleQueue();
            q.Push(E("A"), 0f);
            Assert.IsFalse(q.Push(E("A"), 0.1f));
            for (int i = 0; i < 10; i++) q.Push(E("L" + i), 0.2f);
            Assert.AreEqual(SubtitleQueue.Capacity, q.PendingCount);
            q.Clear();
            Assert.IsFalse(q.HasCurrent);
            Assert.AreEqual(0, q.PendingCount);
        }

        [Test]
        public void Priority_DamageAndGrabAreUrgent()
        {
            Assert.AreEqual(SubtitlePriority.Urgent, SubtitlePriority.Classify("(Te golpean: 2/4)"));
            Assert.AreEqual(SubtitlePriority.Urgent, SubtitlePriority.Classify("(¡Te agarra! Forcejea para quitártelo de encima)"));
            Assert.AreEqual(SubtitlePriority.Normal, SubtitlePriority.Classify("ELENA: Ven despacio."));
        }

        [Test]
        public void VoiceKey_NormalizesSpeakerAccentsAndPunctuation()
        {
            Assert.AreEqual("corre alex", ElenaVoiceText.Normalize("ELENA: ¡Corre, Alex!"));
            Assert.AreEqual(ElenaVoiceText.Normalize("Ya, ya, ya... ¡listo! Te abrí la puerta."),
                            ElenaVoiceText.Normalize("ya ya ya listo te abri la puerta"));
            Assert.IsTrue(ElenaVoiceText.IsElenaSubtitle("ELENA: hola"));
            Assert.IsFalse(ElenaVoiceText.IsElenaSubtitle("¿ELENA?: ven aquí"), "la voz falsa no es Elena");
        }
    }
}
