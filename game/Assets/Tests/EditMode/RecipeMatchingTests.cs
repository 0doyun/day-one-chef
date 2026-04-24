// EditMode tests for Recipe.Matches — the shallow (no event-log)
// evaluator path used by Day 4. Day 6 adds an order-sensitive check
// on top of this.

using DayOneChef.Gameplay.Data;
using NUnit.Framework;
using UnityEngine;

namespace DayOneChef.Tests
{
    public class RecipeMatchingTests
    {
        private static Recipe MakeRecipe(bool orderSensitive, params RecipeComponent[] components)
        {
            var recipe = ScriptableObject.CreateInstance<Recipe>();
            recipe.Configure("TestRecipe", components, orderSensitive);
            return recipe;
        }

        [Test]
        public void Matches_ExactComponents_ReturnsTrue()
        {
            var recipe = MakeRecipe(false,
                new RecipeComponent { Type = IngredientType.Bread, RequiredState = IngredientState.Cooked });
            var actual = new[] { (IngredientType.Bread, IngredientState.Cooked) };

            Assert.IsTrue(recipe.Matches(actual));

            Object.DestroyImmediate(recipe);
        }

        [Test]
        public void Matches_MissingComponent_ReturnsFalse()
        {
            var recipe = MakeRecipe(false,
                new RecipeComponent { Type = IngredientType.Lettuce, RequiredState = IngredientState.Chopped },
                new RecipeComponent { Type = IngredientType.Tomato,  RequiredState = IngredientState.Chopped });
            var actual = new[] { (IngredientType.Lettuce, IngredientState.Chopped) };

            Assert.IsFalse(recipe.Matches(actual));

            Object.DestroyImmediate(recipe);
        }

        [Test]
        public void Matches_WrongState_ReturnsFalse()
        {
            var recipe = MakeRecipe(false,
                new RecipeComponent { Type = IngredientType.Patty, RequiredState = IngredientState.Cooked });
            var actual = new[] { (IngredientType.Patty, IngredientState.Raw) };

            Assert.IsFalse(recipe.Matches(actual));

            Object.DestroyImmediate(recipe);
        }

        [Test]
        public void Matches_DuplicateRequirement_ConsumedOnce()
        {
            // Cheeseburger has bread × 2 (top/bottom bun) — ensure the
            // shallow matcher doesn't double-count a single bread.
            var recipe = MakeRecipe(false,
                new RecipeComponent { Type = IngredientType.Bread, RequiredState = IngredientState.Cooked },
                new RecipeComponent { Type = IngredientType.Bread, RequiredState = IngredientState.Cooked });

            Assert.IsFalse(recipe.Matches(new[] {
                (IngredientType.Bread, IngredientState.Cooked),
            }));
            Assert.IsTrue(recipe.Matches(new[] {
                (IngredientType.Bread, IngredientState.Cooked),
                (IngredientType.Bread, IngredientState.Cooked),
            }));

            Object.DestroyImmediate(recipe);
        }

        [Test]
        public void OrderSensitive_FlagPersists()
        {
            var recipe = MakeRecipe(true,
                new RecipeComponent { Type = IngredientType.Egg, RequiredState = IngredientState.Cooked });
            Assert.IsTrue(recipe.OrderSensitive);
            Object.DestroyImmediate(recipe);
        }
    }
}
