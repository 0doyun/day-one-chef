// EditMode tests for the Day 4 ingredient state machine.
// Verifies Ingredient.TrySetState honours IngredientDefinition.AllowedStates
// and emits StateChanged on legal transitions.

using DayOneChef.Gameplay;
using DayOneChef.Gameplay.Data;
using NUnit.Framework;
using UnityEngine;

namespace DayOneChef.Tests
{
    public class IngredientStateTests
    {
        private static IngredientDefinition MakeDefinition(
            IngredientType type,
            IngredientState initial,
            params IngredientState[] allowed)
        {
            var def = ScriptableObject.CreateInstance<IngredientDefinition>();
            def.Configure(type, type.ToString(), initial, allowed);
            return def;
        }

        [Test]
        public void TrySetState_AllowedTransition_UpdatesAndFires()
        {
            var def = MakeDefinition(IngredientType.Patty, IngredientState.Raw,
                IngredientState.Raw, IngredientState.Cooked, IngredientState.Burnt);
            var go = new GameObject("TestIngredient");
            var ingredient = go.AddComponent<Ingredient>();
            ingredient.Configure(def);

            IngredientState? observedPrev = null;
            IngredientState? observedNext = null;
            ingredient.StateChanged += (_, prev, next) =>
            {
                observedPrev = prev;
                observedNext = next;
            };

            var ok = ingredient.TrySetState(IngredientState.Cooked);

            Assert.IsTrue(ok);
            Assert.AreEqual(IngredientState.Cooked, ingredient.CurrentState);
            Assert.AreEqual(IngredientState.Raw, observedPrev);
            Assert.AreEqual(IngredientState.Cooked, observedNext);

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(def);
        }

        [Test]
        public void TrySetState_DisallowedTransition_RejectsAndKeepsState()
        {
            var def = MakeDefinition(IngredientType.Cheese, IngredientState.Whole,
                IngredientState.Whole, IngredientState.Sliced);
            var go = new GameObject("TestIngredient");
            var ingredient = go.AddComponent<Ingredient>();
            ingredient.Configure(def);

            // Expect the warning so the test doesn't log-explode other tests.
            UnityEngine.TestTools.LogAssert.Expect(
                LogType.Warning, new System.Text.RegularExpressions.Regex(".*does not permit state.*"));

            var ok = ingredient.TrySetState(IngredientState.Cooked);

            Assert.IsFalse(ok);
            Assert.AreEqual(IngredientState.Whole, ingredient.CurrentState);

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(def);
        }

        [Test]
        public void TrySetState_SameState_NoOpReturnsTrue()
        {
            var def = MakeDefinition(IngredientType.Bread, IngredientState.Raw,
                IngredientState.Raw, IngredientState.Cooked);
            var go = new GameObject("TestIngredient");
            var ingredient = go.AddComponent<Ingredient>();
            ingredient.Configure(def);

            var fired = false;
            ingredient.StateChanged += (_, _, _) => fired = true;

            var ok = ingredient.TrySetState(IngredientState.Raw);

            Assert.IsTrue(ok);
            Assert.IsFalse(fired);
            Assert.AreEqual(IngredientState.Raw, ingredient.CurrentState);

            Object.DestroyImmediate(go);
            Object.DestroyImmediate(def);
        }
    }
}
