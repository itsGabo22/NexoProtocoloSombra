using Nexo.Narrative;
using NUnit.Framework;

namespace Nexo.Tests
{
    [TestFixture]
    public sealed class FloorProgressionOrderTests
    {
        private static readonly string[] GameFloors = { "Floor12_Offices", "Floor06_Labs", "Floor01_Lobby" };

        [Test]
        public void NextIndex_AdvancesThroughTheFloorsInOrder()
        {
            var order = new FloorOrder(GameFloors);

            Assert.AreEqual(0, order.CurrentIndex);
            Assert.IsTrue(order.TryGetNext(out int next));
            Assert.AreEqual(1, next);

            Assert.IsTrue(order.Advance());
            Assert.AreEqual("Floor06_Labs", order.CurrentScene);
            Assert.IsTrue(order.Advance());
            Assert.AreEqual("Floor01_Lobby", order.CurrentScene);
        }

        [Test]
        public void LastFloor_ReturnsNoNext_AndDoesNotAdvance()
        {
            var order = new FloorOrder(GameFloors);
            order.Advance();
            order.Advance();

            Assert.IsTrue(order.IsLast);
            Assert.IsFalse(order.TryGetNext(out int next));
            Assert.AreEqual(-1, next);
            Assert.IsFalse(order.Advance());
            Assert.AreEqual(2, order.CurrentIndex);
        }

        [Test]
        public void Reset_ReturnsToTheFirstFloor()
        {
            var order = new FloorOrder(GameFloors);
            order.Advance();
            order.Advance();

            order.Reset();

            Assert.AreEqual(0, order.CurrentIndex);
            Assert.AreEqual("Floor12_Offices", order.CurrentScene);
        }

        [Test]
        public void EmptyOrder_HasNoNextFloor()
        {
            var order = new FloorOrder(null);

            Assert.AreEqual(0, order.Count);
            Assert.IsTrue(order.IsLast);
            Assert.IsFalse(order.Advance());
            Assert.IsNull(order.CurrentScene);
        }

        [TestCase("Floor12_Offices", 12)]
        [TestCase("Floor06_Labs", 6)]
        [TestCase("Floor01_Lobby", 1)]
        [TestCase("Systems_Persistent", -1)]
        [TestCase("Floor_Lobby", -1)]
        public void ParseFloorNumber_ReadsTheNumberFromTheSceneName(string sceneName, int expected)
        {
            Assert.AreEqual(expected, FloorOrder.ParseFloorNumber(sceneName));
        }
    }
}
