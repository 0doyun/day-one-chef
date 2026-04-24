// EditMode tests for the Day 4 OrderQueue. Locks the GDD §12 contract:
// the tutorial queue is fixed-sequence, non-random, non-refillable.

using DayOneChef.Gameplay;
using DayOneChef.Gameplay.Data;
using NUnit.Framework;
using UnityEngine;

namespace DayOneChef.Tests
{
    public class OrderQueueTests
    {
        private static Order MakeOrder(string id)
        {
            var order = ScriptableObject.CreateInstance<Order>();
            order.Configure(id, null, CustomerMood.Waiting, string.Empty);
            return order;
        }

        [Test]
        public void Queue_Progression_FollowsInsertOrder()
        {
            var orders = new[] { MakeOrder("A"), MakeOrder("B"), MakeOrder("C") };
            var queue = new OrderQueue(orders);

            Assert.AreEqual("A", queue.Current.OrderId);
            Assert.AreEqual(0, queue.ProcessedCount);
            queue.Advance();
            Assert.AreEqual("B", queue.Current.OrderId);
            queue.Advance();
            Assert.AreEqual("C", queue.Current.OrderId);
            queue.Advance();
            Assert.IsTrue(queue.IsExhausted);
            Assert.IsNull(queue.Current);

            foreach (var o in orders) Object.DestroyImmediate(o);
        }

        [Test]
        public void Advance_PastEnd_DoesNotThrow()
        {
            var orders = new[] { MakeOrder("A") };
            var queue = new OrderQueue(orders);
            queue.Advance();
            // Extra advances should be safe no-ops — prevents the "final
            // round" UI from crashing if it double-advances.
            Assert.DoesNotThrow(() => queue.Advance());
            Assert.DoesNotThrow(() => queue.Advance());
            Assert.IsTrue(queue.IsExhausted);

            Object.DestroyImmediate(orders[0]);
        }

        [Test]
        public void Queue_TotalCount_MatchesInput()
        {
            var orders = new[] { MakeOrder("A"), MakeOrder("B"), MakeOrder("C"), MakeOrder("D"), MakeOrder("E") };
            var queue = new OrderQueue(orders);

            Assert.AreEqual(5, queue.Count);

            foreach (var o in orders) Object.DestroyImmediate(o);
        }
    }
}
