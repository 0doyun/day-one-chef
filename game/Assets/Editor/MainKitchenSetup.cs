// One-shot scene generator for the Day 3 prototype MainKitchen scene.
// Runs in both interactive Editor (Tools → Day One Chef → Setup Main Kitchen)
// and batch mode (called from WebGLBuildScript.BuildWebGLMain).
//
// Unlike OSSImeProbeSetup this does NOT use ExecuteMenuItem, so it is
// fully batch-safe. The scene + placeholder sprite are derived artifacts
// — both are gitignored and regenerated every build.

#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.Collections.Generic;
using DayOneChef.Bridge;
using DayOneChef.Gameplay;
using DayOneChef.Gameplay.Data;

namespace DayOneChef.Editor
{
    public static class MainKitchenSetup
    {
        private const string ScenePath = "Assets/Scenes/MainKitchen.unity";
        private const string SpriteDir = "Assets/Sprites";
        private const string WhiteSquarePath = "Assets/Sprites/WhiteSquare.png";
        private const string KoreanFontAssetPath = "Assets/Fonts/NotoSansKR SDF.asset";
        private const string OrderCatalogPath = "Assets/Data/OrderCatalog.asset";
        private const string GeminiConfigPath = "Assets/Data/GeminiConfig.asset";
        private const string IngredientDir = "Assets/Data/Ingredients";

        [MenuItem("Tools/Day One Chef/Setup Main Kitchen")]
        public static void Setup()
        {
            EnsureWhiteSquareSprite();

            // Ensure NotoSansKR SDF is freshly baked before the scene
            // captures font references. Running the menu entry out of
            // order (Setup without Install first) or with a stale asset
            // from a previous range will otherwise leave the labels
            // pointing at LiberationSans SDF and every 한글 glyph tofus.
            KoreanFontSetup.InstallKoreanFont();

            // Day 4: make sure the order catalog + ingredient/recipe
            // definitions exist before the scene tries to reference them.
            // Idempotent — reconfigures existing assets rather than
            // recreating, so GUIDs survive across re-runs.
            GameDataGenerator.GenerateAll();

            var koreanFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(KoreanFontAssetPath);
            if (koreanFont == null)
            {
                Debug.LogError(
                    $"[MainKitchenSetup] {KoreanFontAssetPath} did not load after " +
                    "KoreanFontSetup.InstallKoreanFont(). Aborting — the labels need " +
                    "this asset for Hangul rendering.");
                return;
            }
            var catalog = AssetDatabase.LoadAssetAtPath<OrderCatalog>(OrderCatalogPath);
            if (catalog == null)
            {
                Debug.LogError(
                    $"[MainKitchenSetup] {OrderCatalogPath} missing after " +
                    "GameDataGenerator.GenerateAll(). Aborting.");
                return;
            }
            Debug.Log(
                $"[MainKitchenSetup] Using NotoSansKR SDF with {koreanFont.glyphTable?.Count ?? 0} glyphs, " +
                $"OrderCatalog with {catalog.Count} orders.");

            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.DefaultGameObjects,
                NewSceneMode.Single);

            ConfigureCamera(out var cameraGo);
            CreateFloor();
            var playerGo = CreatePlayer(koreanFont);
            cameraGo.GetComponent<CameraFollow>().Target = playerGo.transform;

            CreateStation("Station_Fridge",       StationType.Fridge,       "냉장고",
                new Vector3(-7f,  4f, 0f), new Color(0.56f, 0.79f, 0.90f, 1f), koreanFont);
            CreateStation("Station_CuttingBoard", StationType.CuttingBoard, "도마",
                new Vector3(-3f,  4f, 0f), new Color(0.80f, 0.63f, 0.42f, 1f), koreanFont);
            CreateStation("Station_Stove",        StationType.Stove,        "화구",
                new Vector3( 3f,  4f, 0f), new Color(0.90f, 0.40f, 0.30f, 1f), koreanFont);
            CreateStation("Station_Assembly",     StationType.Assembly,     "조립대",
                new Vector3( 7f,  4f, 0f), new Color(0.85f, 0.85f, 0.85f, 1f), koreanFont);
            CreateStation("Station_Counter",      StationType.Counter,      "카운터",
                new Vector3( 0f, -4f, 0f), new Color(0.45f, 0.75f, 0.55f, 1f), koreanFont);

            var customerGo = CreateCustomer(
                new Vector3(0f, -6.2f, 0f),
                new Color(0.85f, 0.60f, 0.90f, 1f),
                koreanFont);

            var geminiConfig = AssetDatabase.LoadAssetAtPath<GeminiConfig>(GeminiConfigPath);
            var ingredientDefs = LoadIngredientDefinitions();
            CreateBridgeReceiver();
            var gameRound = CreateGameRoot(
                catalog, customerGo.GetComponent<Customer>(), geminiConfig, ingredientDefs);
            var posterGo = new GameObject("DebugInstructionPoster");
            var poster = posterGo.AddComponent<DebugInstructionPoster>();
            poster.Bind(gameRound);
            EditorUtility.SetDirty(poster);

            var sceneDir = Path.GetDirectoryName(ScenePath);
            if (!string.IsNullOrEmpty(sceneDir)) Directory.CreateDirectory(sceneDir);
            EditorSceneManager.SaveScene(scene, ScenePath);

            RegisterSceneInBuildSettings(ScenePath);
            Debug.Log($"[MainKitchenSetup] Wrote {ScenePath} with 5 stations + player + camera follow + customer + game round.");
        }

        private static void ConfigureCamera(out GameObject cameraGo)
        {
            var camera = Camera.main;
            cameraGo = camera != null ? camera.gameObject : new GameObject("Main Camera");
            if (camera == null) camera = cameraGo.AddComponent<Camera>();
            cameraGo.tag = "MainCamera";
            camera.orthographic = true;
            camera.orthographicSize = 8f;
            camera.backgroundColor = new Color(0.18f, 0.18f, 0.22f, 1f);
            cameraGo.transform.position = new Vector3(0f, 0f, -10f);
            if (cameraGo.GetComponent<CameraFollow>() == null)
            {
                cameraGo.AddComponent<CameraFollow>();
            }
        }

        private static void CreateFloor()
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(WhiteSquarePath);
            var floor = new GameObject("Floor");
            floor.transform.position = new Vector3(0f, 0f, 0.1f);
            floor.transform.localScale = new Vector3(20f, 12f, 1f);
            var sr = floor.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = new Color(0.72f, 0.64f, 0.50f, 1f);
            sr.sortingOrder = 0;
        }

        private static GameObject CreatePlayer(TMP_FontAsset koreanFont)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(WhiteSquarePath);
            var player = new GameObject("Player");
            player.tag = "Player";
            player.transform.position = Vector3.zero;
            player.transform.localScale = new Vector3(0.8f, 1f, 1f);
            var sr = player.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = new Color(0.95f, 0.80f, 0.30f, 1f);
            sr.sortingOrder = 10;
            var col = player.AddComponent<CircleCollider2D>();
            col.radius = 0.45f;
            var rb = player.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.linearDamping = 5f;
            player.AddComponent<PlayerController>();

            // Day 13 polish: ChefAnimator + held-ingredient label child.
            // The label is parented under Player so it follows the chef
            // as the animator drives the transform.
            var animator = player.AddComponent<ChefAnimator>();
            var labelGo = new GameObject("HeldLabel");
            labelGo.transform.SetParent(player.transform, worldPositionStays: false);
            labelGo.transform.localPosition = new Vector3(0f, 1.0f, -0.1f);
            labelGo.transform.localScale = new Vector3(
                1f / player.transform.localScale.x,
                1f / player.transform.localScale.y,
                1f);
            var tmp = labelGo.AddComponent<TextMeshPro>();
            tmp.font = koreanFont;
            tmp.text = string.Empty;
            tmp.fontSize = 3.2f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(0.05f, 0.05f, 0.05f, 1f);
            tmp.sortingOrder = 12;
            labelGo.SetActive(false);
            EditorUtility.SetDirty(tmp);

            // Same SerializedObject dance as Customer._orderBubble — a
            // direct setter doesn't survive into the saved scene YAML.
            var serialized = new SerializedObject(animator);
            serialized.FindProperty("_heldLabel").objectReferenceValue = tmp;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return player;
        }

        private static void CreateStation(string name, StationType type, string label,
                                           Vector3 pos, Color color, TMP_FontAsset koreanFont)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(WhiteSquarePath);
            var go = new GameObject(name);
            go.transform.position = pos;
            go.transform.localScale = new Vector3(2.2f, 1.6f, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = 5;
            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = Vector2.one;

            var marker = go.AddComponent<StationMarker>();
            marker.Configure(type, label);

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(go.transform, worldPositionStays: false);
            labelGo.transform.localPosition = new Vector3(0f, 0.7f, -0.1f);
            labelGo.transform.localScale = new Vector3(
                1f / go.transform.localScale.x,
                1f / go.transform.localScale.y,
                1f);
            var tmp = labelGo.AddComponent<TextMeshPro>();
            // Assign the font BEFORE any text. TMP's internal material
            // caches pick the first font they see at text-layout time,
            // and setting .text before .font leaves the label bound to
            // LiberationSans SDF's material even after reassignment.
            tmp.font = koreanFont;
            tmp.text = label;
            tmp.fontSize = 4f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.black;
            tmp.sortingOrder = 11;
            EditorUtility.SetDirty(tmp);
        }

        private static GameObject CreateCustomer(Vector3 pos, Color color, TMP_FontAsset koreanFont)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(WhiteSquarePath);
            var go = new GameObject("Customer");
            go.transform.position = pos;
            go.transform.localScale = new Vector3(1.2f, 1.6f, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = 8;

            var customer = go.AddComponent<Customer>();

            // Order bubble — a child TMP label above the customer.
            // Wired to the Customer via AttachBubble so Customer.Configure
            // (called every round start by GameRound) can update the text
            // to the current recipe's display name.
            var bubbleGo = new GameObject("OrderBubble");
            bubbleGo.transform.SetParent(go.transform, worldPositionStays: false);
            bubbleGo.transform.localPosition = new Vector3(0f, 1.3f, -0.1f);
            bubbleGo.transform.localScale = new Vector3(
                1f / go.transform.localScale.x,
                1f / go.transform.localScale.y,
                1f);
            var tmp = bubbleGo.AddComponent<TextMeshPro>();
            tmp.font = koreanFont;
            tmp.text = "주문 대기 중…";
            tmp.fontSize = 4f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.black;
            tmp.sortingOrder = 12;
            EditorUtility.SetDirty(tmp);

            // Use SerializedObject so the private [SerializeField] field
            // `_orderBubble` is written back through Unity's serialisation
            // pipeline. Directly calling AttachBubble + SetDirty was
            // not enough for the reference to persist into the scene
            // YAML — the Play mode version had _orderBubble == null.
            var serialized = new SerializedObject(customer);
            serialized.FindProperty("_orderBubble").objectReferenceValue = tmp;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return go;
        }

        private static GameRound CreateGameRoot(
            OrderCatalog catalog,
            Customer customer,
            GeminiConfig geminiConfig,
            IngredientDefinition[] ingredientDefs)
        {
            var root = new GameObject("GameRoot");
            var round = root.AddComponent<GameRound>();
            round.Bind(catalog, customer, geminiConfig);
            round.BindIngredients(ingredientDefs);
            // Serialise the ingredient array through SerializedObject so
            // the reference survives scene save — same reason
            // Customer._orderBubble needs this path (see Day 4 notes).
            var so = new SerializedObject(round);
            var prop = so.FindProperty("_ingredientDefinitions");
            prop.arraySize = ingredientDefs.Length;
            for (var i = 0; i < ingredientDefs.Length; i++)
            {
                prop.GetArrayElementAtIndex(i).objectReferenceValue = ingredientDefs[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return round;
        }

        private static void CreateBridgeReceiver()
        {
            // Flutter sends messages with
            //   unityInstance.SendMessage('BridgeReceiver', 'MethodName', '')
            // so the GameObject name must be stable across builds — do
            // not rename without updating the Flutter side in
            // app/lib/src/shell/flutter_bridge.dart.
            var existing = GameObject.Find("BridgeReceiver");
            if (existing == null)
            {
                var go = new GameObject("BridgeReceiver");
                go.AddComponent<BridgeIncoming>();
                Debug.Log("[MainKitchenSetup] Created BridgeReceiver GameObject.");
            }
        }

        private static IngredientDefinition[] LoadIngredientDefinitions()
        {
            var list = new List<IngredientDefinition>();
            foreach (IngredientType type in System.Enum.GetValues(typeof(IngredientType)))
            {
                var path = $"{IngredientDir}/{type}.asset";
                var def = AssetDatabase.LoadAssetAtPath<IngredientDefinition>(path);
                if (def == null)
                {
                    Debug.LogWarning($"[MainKitchenSetup] Missing ingredient asset at {path} — " +
                                     "run Tools → Day One Chef → Generate Game Data first.");
                    continue;
                }
                list.Add(def);
            }
            return list.ToArray();
        }

        private static void EnsureWhiteSquareSprite()
        {
            Directory.CreateDirectory(SpriteDir);

            if (!File.Exists(WhiteSquarePath))
            {
                var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                var pixels = new Color32[16];
                for (var i = 0; i < pixels.Length; i++)
                {
                    pixels[i] = new Color32(255, 255, 255, 255);
                }
                tex.SetPixels32(pixels);
                tex.Apply();
                File.WriteAllBytes(WhiteSquarePath, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.Refresh();
            }

            // Always reassert the importer settings. A 4 px source at the
            // default PPU of 100 rendered the player and stations as
            // single-pixel dots. PPU 4 makes one texel equal one world
            // unit so `localScale = (2.2, 1.6)` produces sprites that
            // visibly occupy 2.2 × 1.6 world units.
            var importer = (TextureImporter)AssetImporter.GetAtPath(WhiteSquarePath);
            if (importer == null)
            {
                Debug.LogError($"[MainKitchenSetup] TextureImporter missing for {WhiteSquarePath}.");
                return;
            }
            var needsReimport =
                importer.textureType != TextureImporterType.Sprite ||
                importer.spriteImportMode != SpriteImportMode.Single ||
                importer.filterMode != FilterMode.Point ||
                importer.textureCompression != TextureImporterCompression.Uncompressed ||
                !Mathf.Approximately(importer.spritePixelsPerUnit, 4f);
            if (needsReimport)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.spritePixelsPerUnit = 4f;
                importer.SaveAndReimport();
            }
        }

        private static void RegisterSceneInBuildSettings(string path)
        {
            var existing = EditorBuildSettings.scenes;
            foreach (var s in existing)
            {
                if (s.path == path) return;
            }
            var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(existing);
            list.Add(new EditorBuildSettingsScene(path, enabled: true));
            EditorBuildSettings.scenes = list.ToArray();
        }
    }
}
#endif
