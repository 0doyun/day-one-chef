// EditMode tests for the Day 6 action executor. Focus is on the
// state-machine + event log contract — tick timing is verified only
// indirectly (last entry has non-zero t), since real-time waits would
// make the EditMode suite slow.

using System.Threading.Tasks;
using DayOneChef.Gameplay;
using DayOneChef.Gameplay.Data;
using NUnit.Framework;
using UnityEngine;

namespace DayOneChef.Tests
{
    public class ActionExecutorTests
    {
        private static IngredientDefinition MakeDef(
            IngredientType type,
            IngredientState initial,
            params IngredientState[] allowed)
        {
            var def = ScriptableObject.CreateInstance<IngredientDefinition>();
            def.Configure(type, type.ToString(), initial, allowed);
            return def;
        }

        private static KitchenState MakeKitchen()
        {
            var defs = new[]
            {
                MakeDef(IngredientType.Bread,   IngredientState.Raw,   IngredientState.Raw,   IngredientState.Cooked, IngredientState.Burnt),
                MakeDef(IngredientType.Patty,   IngredientState.Raw,   IngredientState.Raw,   IngredientState.Cooked, IngredientState.Burnt),
                MakeDef(IngredientType.Lettuce, IngredientState.Whole, IngredientState.Whole, IngredientState.Washed, IngredientState.Chopped),
                MakeDef(IngredientType.Egg,     IngredientState.Shell, IngredientState.Shell, IngredientState.Cracked, IngredientState.Beaten, IngredientState.Mixed, IngredientState.Cooked),
            };
            return new KitchenState(defs, System.Array.Empty<StationMarker>());
        }

        [Test]
        public async Task Execute_HappyPath_AppliesStateTransitions()
        {
            var kitchen = MakeKitchen();
            var exec = new ActionExecutor(kitchen);
            var response = new ChefActionResponse
            {
                monologue = "구울게요",
                actions = new[]
                {
                    new ChefAction { verb = "pickup", target = "빵" },
                    new ChefAction { verb = "cook",   target = "빵", param = "grill" },
                },
            };

            var log = await exec.ExecuteAsync(response);

            Assert.AreEqual(2, log.Count);
            Assert.IsFalse(log.Entries[0].skipped);
            Assert.IsFalse(log.Entries[1].skipped);
            Assert.AreEqual(IngredientState.Cooked, kitchen.GetState(IngredientType.Bread));
            Assert.AreEqual(IngredientType.Bread, kitchen.ChefHolding);
        }

        [Test]
        public async Task Execute_UnknownVerb_MarksSkippedNotThrows()
        {
            var kitchen = MakeKitchen();
            var exec = new ActionExecutor(kitchen);
            var response = new ChefActionResponse
            {
                actions = new[]
                {
                    new ChefAction { verb = "bake", target = "빵" }, // not in the 8-verb set
                },
            };

            var log = await exec.ExecuteAsync(response);

            Assert.AreEqual(1, log.Count);
            Assert.IsTrue(log.Entries[0].skipped);
            Assert.AreEqual("unknown verb", log.Entries[0].reason);
        }

        [Test]
        public async Task Execute_UnknownIngredient_MarksSkippedWithReason()
        {
            var kitchen = MakeKitchen();
            var exec = new ActionExecutor(kitchen);
            var response = new ChefActionResponse
            {
                actions = new[]
                {
                    new ChefAction { verb = "pickup", target = "양파" }, // not in definitions
                },
            };

            var log = await exec.ExecuteAsync(response);

            Assert.AreEqual(1, log.Count);
            Assert.IsTrue(log.Entries[0].skipped);
            Assert.That(log.Entries[0].reason, Does.Contain("unknown ingredient"));
        }

        [Test]
        public async Task Execute_DisallowedTransition_MarksSkipped()
        {
            // Cheese has no Cooked in its allowed states; cooking cheese
            // must skip rather than corrupt the kitchen.
            var defs = new[]
            {
                MakeDef(IngredientType.Cheese, IngredientState.Whole, IngredientState.Whole, IngredientState.Sliced),
            };
            var kitchen = new KitchenState(defs, System.Array.Empty<StationMarker>());
            var exec = new ActionExecutor(kitchen);
            var response = new ChefActionResponse
            {
                actions = new[]
                {
                    new ChefAction { verb = "cook", target = "치즈", param = "grill" },
                },
            };

            var log = await exec.ExecuteAsync(response);

            Assert.IsTrue(log.Entries[0].skipped);
            Assert.That(log.Entries[0].reason, Does.Contain("does not permit"));
            Assert.AreEqual(IngredientState.Whole, kitchen.GetState(IngredientType.Cheese));
        }

        [Test]
        public async Task Execute_SteamedEgg_OrderSensitiveEventLog()
        {
            // GDD §2 order 5 — the order-sensitive spotlight. Correct
            // sequence: crack → mix → cook. The log must preserve this
            // order and drive the egg into Mixed → Cooked.
            var kitchen = MakeKitchen();
            var exec = new ActionExecutor(kitchen);
            var response = new ChefActionResponse
            {
                actions = new[]
                {
                    new ChefAction { verb = "crack", target = "계란" },
                    new ChefAction { verb = "mix",   target = "계란" },
                    new ChefAction { verb = "cook",  target = "계란", param = "steam" },
                },
            };

            var log = await exec.ExecuteAsync(response);

            Assert.AreEqual(3, log.Count);
            Assert.AreEqual("crack", log.Entries[0].verb);
            Assert.AreEqual("mix",   log.Entries[1].verb);
            Assert.AreEqual("cook",  log.Entries[2].verb);
            Assert.IsFalse(log.Entries[0].skipped);
            Assert.IsFalse(log.Entries[1].skipped);
            Assert.IsFalse(log.Entries[2].skipped);
            Assert.AreEqual(IngredientState.Cooked, kitchen.GetState(IngredientType.Egg));
        }

        [Test]
        public async Task Execute_EmptyActions_ReturnsEmptyLog()
        {
            var kitchen = MakeKitchen();
            var exec = new ActionExecutor(kitchen);
            var response = new ChefActionResponse { actions = System.Array.Empty<ChefAction>() };

            var log = await exec.ExecuteAsync(response);

            Assert.AreEqual(0, log.Count);
        }
    }
}
