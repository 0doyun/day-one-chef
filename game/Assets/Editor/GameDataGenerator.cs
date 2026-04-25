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
        private const string GeminiConfigPath = "Assets/Data/GeminiConfig.asset";

        [MenuItem("Tools/Day One Chef/Generate Game Data")]
        public static void GenerateAll()
        {
            EnsureDirectories();

            var ingredients = GenerateIngredients();
            var recipes = GenerateRecipes(ingredients);
            var orders = GenerateOrders(recipes);
            GenerateCatalog(orders);
            EnsureGeminiConfig();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                $"[GameDataGenerator] Done. ingredients={ingredients.Count} " +
                $"recipes={recipes.Count} orders={orders.Count} catalog={CatalogPath} " +
                $"geminiConfig={GeminiConfigPath}");
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

            map[IngredientType.Cabbage] = UpsertIngredient(
                "Cabbage",
                IngredientType.Cabbage,
                displayName: "양배추",
                initial: IngredientState.Whole,
                allowed: new[] { IngredientState.Whole, IngredientState.Washed, IngredientState.Chopped });

            map[IngredientType.Potato] = UpsertIngredient(
                "Potato",
                IngredientType.Potato,
                displayName: "감자",
                initial: IngredientState.Whole,
                allowed: new[] {
                    IngredientState.Whole, IngredientState.Washed,
                    IngredientState.Chopped, IngredientState.Cooked,
                });

            return map;
        }

        private static Dictionary<string, Recipe> GenerateRecipes(
            Dictionary<IngredientType, IngredientDefinition> ingredients)
        {
            var map = new Dictionary<string, Recipe>();

            map["Toast"] = UpsertRecipe(
                "Toast",
                displayName: "토스트",
                orderSensitive: false,
                components: new[] {
                    new RecipeComponent { Type = IngredientType.Bread, RequiredState = IngredientState.Cooked },
                },
                procedureNotes: "빵을 화구에 올려 굽는다. 다른 재료는 쓰지 않는다.");

            // Day 13-C: salad now demands the implicit-wash step. Players
            // naturally wash leafy veg before chopping; AI without an
            // explicit wash instruction goes straight to the cutting
            // board and leaves dirt on. Three components (상추 + 양배추 +
            // 토마토) each requiring Chopped final state, with the
            // procedure note pinning the wash step the evaluator checks
            // in event_log.
            map["Salad"] = UpsertRecipe(
                "Salad",
                displayName: "샐러드",
                orderSensitive: false,
                components: new[] {
                    new RecipeComponent { Type = IngredientType.Lettuce, RequiredState = IngredientState.Chopped },
                    new RecipeComponent { Type = IngredientType.Cabbage, RequiredState = IngredientState.Chopped },
                    new RecipeComponent { Type = IngredientType.Tomato,  RequiredState = IngredientState.Chopped },
                },
                procedureNotes: "상추·양배추·토마토를 모두 깨끗이 씻은 뒤 도마에서 썰어 그릇에 담는다. 씻지 않으면 흙이 남아 손님이 먹지 못한다. 씻기 단계 누락은 실패.");

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
                },
                procedureNotes: "빵과 패티는 화구에서 굽고, 치즈는 슬라이스, 상추와 토마토는 도마에서 썰어서 조립한다.");

            // Day 13-C: 계란찜 — crack + beat (mix verb) + cook. Naive
            // "계란 익혀" forgets to crack; "계란 깨서 익혀" forgets to beat.
            // Recipe.Components only checks final Cooked state, so the
            // intermediate steps are enforced through the procedure note
            // and the evaluator's event-log audit.
            map["SteamedEgg"] = UpsertRecipe(
                "SteamedEgg",
                displayName: "계란찜",
                orderSensitive: true,
                components: new[] {
                    new RecipeComponent { Type = IngredientType.Egg, RequiredState = IngredientState.Cooked },
                },
                procedureNotes: "계란 껍질을 깨서 노른자·흰자를 잘 풀어준 뒤 화구에 올려 익힌다. 깨기·풀기·익히기 세 단계 모두 필수 — 깨지 않으면 통째 익은 달걀, 풀지 않으면 그냥 후라이가 된다.");

            // Day 13-C: 감자튀김 — humans automatically wash and chop
            // potatoes before frying; AI literally fries the dirt-clad
            // whole potato. Component check is just Cooked, but
            // procedure_notes drives the evaluator to require both
            // Washed and Chopped to appear in the event_log.
            map["Fries"] = UpsertRecipe(
                "Fries",
                displayName: "감자튀김",
                orderSensitive: true,
                components: new[] {
                    new RecipeComponent { Type = IngredientType.Potato, RequiredState = IngredientState.Cooked },
                },
                procedureNotes: "감자를 깨끗이 씻고(wash) 도마에서 썬(chop) 뒤 화구에 튀긴다. 씻기와 썰기 단계 모두 필수 — 안 씻으면 흙이 묻어 있고, 안 썰면 통감자가 그대로 익는다.");

            // Suppress the unused-parameter analyser — ingredient map is
            // wired through for future Day 6+ use (action executor will
            // look up definitions via this).
            _ = ingredients;
            return map;
        }

        private static List<Order> GenerateOrders(Dictionary<string, Recipe> recipes)
        {
            // Day 13-C round order — difficulty ramp from "obvious" to
            // "actually layered". Toast is the tutorial round; egg-fry
            // teaches the don't-over-process trap; salad introduces the
            // wash step; fries doubles up wash + chop; cheeseburger is
            // the multi-component closer.
            var list = new List<Order>(5);
            list.Add(UpsertOrder("Order_01_Toast",        recipes["Toast"],
                "토스트-01", CustomerMood.Bored,   "빵 화구에 올려서 구워줘"));
            list.Add(UpsertOrder("Order_02_SteamedEgg",   recipes["SteamedEgg"],
                "계란찜-01", CustomerMood.Waiting, "계란 깨서 풀고 화구에서 익혀줘"));
            list.Add(UpsertOrder("Order_03_Salad",        recipes["Salad"],
                "샐러드-01", CustomerMood.Waiting, "상추 양배추 토마토 모두 씻고 썰어서 그릇에 담아줘"));
            list.Add(UpsertOrder("Order_04_Fries",        recipes["Fries"],
                "감자튀김-01", CustomerMood.Waiting, "감자 씻고 썰어서 튀겨줘"));
            list.Add(UpsertOrder("Order_05_Cheeseburger", recipes["Cheeseburger"],
                "버거-01",   CustomerMood.Angry,   "패티 구운 다음 아래 빵에 패티 올리고 치즈 상추 올려서 빵으로 덮어"));
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

        private static void EnsureGeminiConfig()
        {
            // Create with defaults if missing; leave existing assets alone
            // so user-tuned values (temperature, timeout) survive re-runs.
            var config = AssetDatabase.LoadAssetAtPath<GeminiConfig>(GeminiConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<GeminiConfig>();
                AssetDatabase.CreateAsset(config, GeminiConfigPath);
                EditorUtility.SetDirty(config);
            }
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
            RecipeComponent[] components,
            string procedureNotes = null)
        {
            var path = $"{RecipeDir}/{fileName}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<Recipe>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<Recipe>();
                AssetDatabase.CreateAsset(asset, path);
            }
            asset.Configure(displayName, components, orderSensitive, procedureNotes);
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
