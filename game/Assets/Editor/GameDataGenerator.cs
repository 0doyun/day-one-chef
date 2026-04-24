// Generates the 6 ingredient definitions + 5 recipes + 5 orders + the
// catalog listing them in tutorial sequence. Matches the spec in
// design/gdd/game-concept.md §2 (orders), §3.2 (ingredient states),
// §12 (tutorial order lock).
//
// All assets live under Assets/Data/ and are committed to the repo so
// other tooling (MainKitchenSetup, Play mode) can load them by path.
// Re-running the menu entry is idempotent: existing assets are
// reconfigured in place to preserve GUIDs + scene references.

#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using DayOneChef.Gameplay.Data;

namespace DayOneChef.Editor
{
    public static class GameDataGenerator
    {
        private const string DataRoot = "Assets/Data";
        private const string IngredientDir = "Assets/Data/Ingredients";
        private const string RecipeDir = "Assets/Data/Recipes";
        private const string OrderDir = "Assets/Data/Orders";
        private const string CatalogPath = "Assets/Data/OrderCatalog.asset";

        [MenuItem("Tools/Day One Chef/Generate Game Data")]
        public static void GenerateAll()
        {
            EnsureDirectories();

            var ingredients = GenerateIngredients();
            var recipes = GenerateRecipes(ingredients);
            var orders = GenerateOrders(recipes);
            GenerateCatalog(orders);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                $"[GameDataGenerator] Done. ingredients={ingredients.Count} " +
                $"recipes={recipes.Count} orders={orders.Count} catalog={CatalogPath}");
        }

        private static void EnsureDirectories()
        {
            Directory.CreateDirectory(DataRoot);
            Directory.CreateDirectory(IngredientDir);
            Directory.CreateDirectory(RecipeDir);
            Directory.CreateDirectory(OrderDir);
            AssetDatabase.Refresh();
        }

        private static Dictionary<IngredientType, IngredientDefinition> GenerateIngredients()
        {
            var map = new Dictionary<IngredientType, IngredientDefinition>();

            map[IngredientType.Patty] = UpsertIngredient(
                "Patty",
                IngredientType.Patty,
                displayName: "패티",
                initial: IngredientState.Raw,
                allowed: new[] { IngredientState.Raw, IngredientState.Cooked, IngredientState.Burnt });

            map[IngredientType.Bread] = UpsertIngredient(
                "Bread",
                IngredientType.Bread,
                displayName: "빵",
                initial: IngredientState.Raw,
                allowed: new[] { IngredientState.Raw, IngredientState.Cooked, IngredientState.Burnt });

            map[IngredientType.Cheese] = UpsertIngredient(
                "Cheese",
                IngredientType.Cheese,
                displayName: "치즈",
                initial: IngredientState.Whole,
                allowed: new[] { IngredientState.Whole, IngredientState.Sliced });

            map[IngredientType.Lettuce] = UpsertIngredient(
                "Lettuce",
                IngredientType.Lettuce,
                displayName: "상추",
                initial: IngredientState.Whole,
                allowed: new[] { IngredientState.Whole, IngredientState.Washed, IngredientState.Chopped });

            map[IngredientType.Tomato] = UpsertIngredient(
                "Tomato",
                IngredientType.Tomato,
                displayName: "토마토",
                initial: IngredientState.Whole,
                allowed: new[] { IngredientState.Whole, IngredientState.Chopped });

            map[IngredientType.Egg] = UpsertIngredient(
                "Egg",
                IngredientType.Egg,
                displayName: "계란",
                initial: IngredientState.Shell,
                allowed: new[] {
                    IngredientState.Shell, IngredientState.Cracked,
                    IngredientState.Beaten, IngredientState.Mixed,
                    IngredientState.Cooked,
                });

            return map;
        }

        private static Dictionary<string, Recipe> GenerateRecipes(
            Dictionary<IngredientType, IngredientDefinition> ingredients)
        {
            var map = new Dictionary<string, Recipe>();

            map["Toast"] = UpsertRecipe(
                "Toast",
                displayName: "플레인 토스트",
                orderSensitive: false,
                components: new[] {
                    new RecipeComponent { Type = IngredientType.Bread, RequiredState = IngredientState.Cooked },
                });

            map["Salad"] = UpsertRecipe(
                "Salad",
                displayName: "샐러드",
                orderSensitive: false,
                components: new[] {
                    new RecipeComponent { Type = IngredientType.Lettuce, RequiredState = IngredientState.Chopped },
                    new RecipeComponent { Type = IngredientType.Tomato,  RequiredState = IngredientState.Chopped },
                });

            map["Cheeseburger"] = UpsertRecipe(
                "Cheeseburger",
                displayName: "치즈버거",
                orderSensitive: false,
                components: new[] {
                    new RecipeComponent { Type = IngredientType.Bread,   RequiredState = IngredientState.Cooked },
                    new RecipeComponent { Type = IngredientType.Patty,   RequiredState = IngredientState.Cooked },
                    new RecipeComponent { Type = IngredientType.Cheese,  RequiredState = IngredientState.Sliced },
                    new RecipeComponent { Type = IngredientType.Lettuce, RequiredState = IngredientState.Chopped },
                    new RecipeComponent { Type = IngredientType.Bread,   RequiredState = IngredientState.Cooked },
                });

            map["Omelet"] = UpsertRecipe(
                "Omelet",
                displayName: "오믈렛",
                orderSensitive: false,
                components: new[] {
                    new RecipeComponent { Type = IngredientType.Egg, RequiredState = IngredientState.Cooked },
                });

            // 계란찜 — the showcase order-sensitive recipe (GDD §2 주문5,
            // §4.2). Day 4 can only flag this; the event_log-based
            // order check lands on Day 6 alongside the action executor.
            map["SteamedEgg"] = UpsertRecipe(
                "SteamedEgg",
                displayName: "계란찜",
                orderSensitive: true,
                components: new[] {
                    new RecipeComponent { Type = IngredientType.Egg, RequiredState = IngredientState.Cooked },
                });

            // Suppress the unused-parameter analyser — ingredient map is
            // wired through for future Day 6+ use (action executor will
            // look up definitions via this).
            _ = ingredients;
            return map;
        }

        private static List<Order> GenerateOrders(Dictionary<string, Recipe> recipes)
        {
            var list = new List<Order>(5);
            list.Add(UpsertOrder("Order_01_Toast",        recipes["Toast"],
                "토스트-01", CustomerMood.Bored,   "빵 화구에 올려서 구워줘"));
            list.Add(UpsertOrder("Order_02_Salad",        recipes["Salad"],
                "샐러드-01", CustomerMood.Waiting, "상추랑 토마토 썰어서 접시에 담아줘"));
            list.Add(UpsertOrder("Order_03_Cheeseburger", recipes["Cheeseburger"],
                "버거-01",   CustomerMood.Waiting, "패티 구운 다음 아래 빵에 패티 올리고 치즈 상추 올려서 빵으로 덮어"));
            list.Add(UpsertOrder("Order_04_Omelet",       recipes["Omelet"],
                "오믈렛-01", CustomerMood.Waiting, "계란 풀어서 팬에 붓고 반으로 접어줘"));
            list.Add(UpsertOrder("Order_05_SteamedEgg",   recipes["SteamedEgg"],
                "계란찜-01", CustomerMood.Angry,   "그릇에 계란 먼저 깨고 그 다음에 물을 섞어서 쪄줘"));
            return list;
        }

        private static void GenerateCatalog(List<Order> orders)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<OrderCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<OrderCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            catalog.Configure(orders.ToArray());
            EditorUtility.SetDirty(catalog);
        }

        private static IngredientDefinition UpsertIngredient(
            string fileName,
            IngredientType type,
            string displayName,
            IngredientState initial,
            IngredientState[] allowed)
        {
            var path = $"{IngredientDir}/{fileName}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<IngredientDefinition>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<IngredientDefinition>();
                AssetDatabase.CreateAsset(asset, path);
            }
            asset.Configure(type, displayName, initial, allowed);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static Recipe UpsertRecipe(
            string fileName,
            string displayName,
            bool orderSensitive,
            RecipeComponent[] components)
        {
            var path = $"{RecipeDir}/{fileName}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<Recipe>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<Recipe>();
                AssetDatabase.CreateAsset(asset, path);
            }
            asset.Configure(displayName, components, orderSensitive);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static Order UpsertOrder(
            string fileName,
            Recipe recipe,
            string orderId,
            CustomerMood mood,
            string exampleInstruction)
        {
            var path = $"{OrderDir}/{fileName}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<Order>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<Order>();
                AssetDatabase.CreateAsset(asset, path);
            }
            asset.Configure(orderId, recipe, mood, exampleInstruction);
            EditorUtility.SetDirty(asset);
            return asset;
        }
    }
}
#endif
