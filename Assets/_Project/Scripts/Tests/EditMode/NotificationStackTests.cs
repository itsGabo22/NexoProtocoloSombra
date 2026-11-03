using System;
using Nexo.UI;
using NUnit.Framework;

namespace Nexo.Tests
{
    /// <summary>
    /// NotificationStack: deduplicación con ventana de 8 s, tope de 3 tarjetas y notificaciones falsas.
    /// </summary>
    [TestFixture]
    public sealed class NotificationStackTests
    {
        private double _now;
        private NotificationStack _stack;

        private double Clock() => _now;

        [SetUp]
        public void SetUp()
        {
            _now = 100.0;
            _stack = new NotificationStack(Clock);
        }

        [Test]
        public void SameNotificationWhileVisible_RefreshesTheSameCard()
        {
            NotificationResult first = _stack.Push("NEXOOS", "Acceso denegado", false);
            _now += 2.0;
            NotificationResult second = _stack.Push("NEXOOS", "Acceso denegado", false);

            Assert.AreEqual(NotificationAction.Create, first.Action);
            Assert.AreEqual(NotificationAction.Refresh, second.Action);
            Assert.AreEqual(first.CardId, second.CardId);
            Assert.AreEqual(1, _stack.VisibleCount);
        }

        [Test]
        public void TenPresses_StillOneCard()
        {
            for (int i = 0; i < 10; i++)
            {
                _stack.Push("NEXOOS", "Acceso denegado", false);
                _now += 0.5;
            }
            Assert.AreEqual(1, _stack.VisibleCount);
        }

        [Test]
        public void SameNotificationAfterExpiry_WithinWindow_IsSuppressed()
        {
            NotificationResult first = _stack.Push("J. RAMOS · SEGURIDAD", "Nota", false);
            _now += 5.5;
            _stack.Expire(first.CardId);
            _now += 1.0;

            NotificationResult again = _stack.Push("J. RAMOS · SEGURIDAD", "Nota", false);
            Assert.AreEqual(NotificationAction.Suppress, again.Action);
            Assert.AreEqual(NotificationResult.None, again.CardId);
            Assert.AreEqual(0, _stack.VisibleCount);
        }

        [Test]
        public void SameNotificationOutsideWindow_CreatesANewCard()
        {
            NotificationResult first = _stack.Push("NEXOOS", "Acceso denegado", false);
            _now += 5.5;
            _stack.Expire(first.CardId);
            _now += NotificationStack.DefaultDedupeWindowSeconds;

            NotificationResult again = _stack.Push("NEXOOS", "Acceso denegado", false);
            Assert.AreEqual(NotificationAction.Create, again.Action);
            Assert.AreNotEqual(first.CardId, again.CardId);
        }

        [Test]
        public void FourthDifferentNotification_EvictsTheOldest()
        {
            NotificationResult a = _stack.Push("A", "uno", false);
            NotificationResult b = _stack.Push("B", "dos", false);
            NotificationResult c = _stack.Push("C", "tres", false);
            NotificationResult d = _stack.Push("D", "cuatro", false);

            Assert.AreEqual(NotificationResult.None, c.EvictedId);
            Assert.AreEqual(a.CardId, d.EvictedId);
            Assert.AreEqual(NotificationStack.DefaultMaxVisible, _stack.VisibleCount);
            Assert.AreEqual(b.CardId, _stack.VisibleIdAt(0), "El orden se conserva: la más antigua queda arriba.");
            Assert.AreEqual(d.CardId, _stack.VisibleIdAt(2));
        }

        [Test]
        public void Refresh_DoesNotEvictAnything()
        {
            _stack.Push("A", "uno", false);
            _stack.Push("B", "dos", false);
            _stack.Push("C", "tres", false);
            NotificationResult again = _stack.Push("B", "dos", false);

            Assert.AreEqual(NotificationAction.Refresh, again.Action);
            Assert.AreEqual(NotificationResult.None, again.EvictedId);
            Assert.AreEqual(3, _stack.VisibleCount);
        }

        [Test]
        public void FakeNotification_IsNotMergedWithARealOne()
        {
            NotificationResult real = _stack.Push("ELENA", "Alex... ven aquí.", false);
            NotificationResult fake = _stack.Push("ELENA", "Alex... ven aquí.", true);

            Assert.AreEqual(NotificationAction.Create, fake.Action);
            Assert.AreNotEqual(real.CardId, fake.CardId);
            Assert.AreEqual(2, _stack.VisibleCount);
        }

        [Test]
        public void FakeNotification_RepeatedWhileVisible_RefreshesItsOwnCard()
        {
            NotificationResult fake = _stack.Push("ELENA", "Alex... ven aquí.", true);
            NotificationResult again = _stack.Push("ELENA", "Alex... ven aquí.", true);

            Assert.AreEqual(NotificationAction.Refresh, again.Action);
            Assert.AreEqual(fake.CardId, again.CardId);
        }

        [Test]
        public void DifferentSendersWithSameBody_AreDifferentCards()
        {
            NotificationResult a = _stack.Push("NEXOOS", "Puerta bloqueada", false);
            NotificationResult b = _stack.Push("ELENA", "Puerta bloqueada", false);

            Assert.AreEqual(NotificationAction.Create, b.Action);
            Assert.AreNotEqual(a.CardId, b.CardId);
        }

        [Test]
        public void ExpireUnknownCard_IsIgnored()
        {
            _stack.Push("A", "uno", false);
            _stack.Expire(999);
            Assert.AreEqual(1, _stack.VisibleCount);
        }

        [Test]
        public void Clear_ForgetsCardsAndHistory()
        {
            _stack.Push("A", "uno", false);
            _stack.Clear();

            Assert.AreEqual(0, _stack.VisibleCount);
            Assert.AreEqual(NotificationAction.Create, _stack.Push("A", "uno", false).Action);
        }

        [Test]
        public void Constructor_RejectsInvalidArguments()
        {
            Assert.Throws<ArgumentNullException>(() => new NotificationStack(null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new NotificationStack(Clock, 0));
        }
    }
}
