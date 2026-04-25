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
using DayOneChef.Gameplay.UI;
using UnityEngine.UI;

namespace DayOneChef.Editor
{
    public static class MainKitchenSetup
    {
        private const string ScenePath = "Assets/Scenes/MainKitchen.unity";
        private const string SpriteDir = "Assets/Sprites";
        private const string WhiteSquarePath = "Assets/Sprites/WhiteSquare.png";
        private const string BadKitchenPath = "Assets/Sprites/External/BadKitchen.png";
        private const string KoreanFontAssetPath = "Assets/Fonts/NotoSansKR SDF.asset";
        private const string OrderCatalogPath = "Assets/Data/OrderCatalog.asset";
        private const string GeminiConfigPath = "Assets/Data/GeminiConfig.asset";
        private const string IngredientDir = "Assets/Data/Ingredients";

        [MenuItem("Tools/Day One Chef/Setup Main Kitchen")]
        public static void Setup()
        {
            EnsureWhiteSquareSprite();
            // Day 13 polish: prefer the generated top-down pixel art
            // over the side-perspective BadKitchen pack. The generator
            // is idempotent so this is safe to run on every build.
            PixelArtGenerator.GenerateAll();

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

            // Burst colors are tuned to read at a glance against each
            // station's body color. Day 13 also swaps the white-square
            // station bodies for slices of OpenGameArt's CC0 BadKitchen
            // pixel sheet (see EnsureBadKitchenSliced) — fridge / stove
            // are exact matches, the rest improvise from neighbouring
            // furniture cells.
            var fridgeSprite   = PixelArtGenerator.Load(PixelArtGenerator.Fridge);
            var boardSprite    = PixelArtGenerator.Load(PixelArtGenerator.CuttingBoard);
            var stoveSprite    = PixelArtGenerator.Load(PixelArtGenerator.Stove);
            var assemblySprite = PixelArtGenerator.Load(PixelArtGenerator.Assembly);
            var counterSprite  = PixelArtGenerator.Load(PixelArtGenerator.Counter);

            // Day 13-B "Sims room" pass: stations live at scale 1.0
            // inside a 16×12 walled room. The top wall row is at y=+5.5,
            // bottom at y=-5.5, side walls at x=±8. Stations sit
            // shoulder-to-shoulder along the top wall (y=4.5) so the
            // chef has the entire room interior to move through.
            CreateRoomWalls();
            CreateStation("Station_Fridge",       StationType.Fridge,       "냉장고",
                new Vector3(-5f, 3.5f, 0f), new Color(1f, 1f, 1f, 1f),
                new Color(0.85f, 0.95f, 1f, 1f), koreanFont,
                bodySprite: fridgeSprite, bodyScale: new Vector3(1.7f, 1.7f, 1f));
            CreateStation("Station_CuttingBoard", StationType.CuttingBoard, "도마",
                new Vector3(-1.7f, 3.5f, 0f), new Color(1f, 1f, 1f, 1f),
                new Color(1f, 1f, 1f, 1f), koreanFont,
                bodySprite: boardSprite, bodyScale: new Vector3(1.7f, 1.7f, 1f));
            CreateStation("Station_Stove",        StationType.Stove,        "화구",
                new Vector3( 1.7f, 3.5f, 0f), new Color(1f, 1f, 1f, 1f),
                new Color(1f, 0.78f, 0.30f, 1f), koreanFont,
                bodySprite: stoveSprite, bodyScale: new Vector3(1.7f, 1.7f, 1f));
            CreateStation("Station_Assembly",     StationType.Assembly,     "조립대",
                new Vector3( 5f, 3.5f, 0f), new Color(1f, 1f, 1f, 1f),
                new Color(1f, 0.95f, 0.65f, 1f), koreanFont,
                bodySprite: assemblySprite, bodyScale: new Vector3(1.7f, 1.7f, 1f));
            CreateStation("Station_Counter",      StationType.Counter,      "카운터",
                new Vector3( 0f, -2.7f, 0f), new Color(1f, 1f, 1f, 1f),
                new Color(0.85f, 1f, 0.85f, 1f), koreanFont,
                bodySprite: counterSprite, bodyScale: new Vector3(1.7f, 1.7f, 1f));

            var customerGo = CreateCustomer(
                new Vector3(0f, -3.7f, 0f),
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

            // Day 13 polish: real in-game HUD (input field, monologue,
            // order panel) on a Screen Space Overlay canvas.
            // Day 13-B: monologue moved to a world-space speech bubble
            // child of the chef so it tracks the animator and reads as
            // the chef's voice instead of a free-floating system box.
            var (chefMonoRoot, chefMonoText) = CreateChefMonologue(playerGo, koreanFont);
            var hud = CreateKitchenHud(koreanFont, chefMonoRoot, chefMonoText);
            gameRound.BindHud(hud);
            EditorUtility.SetDirty(gameRound);

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
            // ortho 5.5 zooms the kitchen so the 5 stations + chef +
            // customer fill the visible frame. Day 13's first pass at 8
            // left wide stripes of empty floor that read as "the screen
            // is mostly nothing".
            // Day 13-B "Sims room" pass: 6.5 ortho frames the entire
            // 16×12 walled room with a small margin so the wall sprites
            // are clearly visible as room boundaries.
            camera.orthographicSize = 5.2f;
            // Day 13-B: warm dark brown matches the master palette outline
            // (#2B1B17) so off-screen edges share the kitchen's tone
            // instead of fighting it with a cool slate gray.
            camera.backgroundColor = new Color(0.102f, 0.071f, 0.031f, 1f); // #1A1208
            cameraGo.transform.position = new Vector3(0f, 0f, -10f);
            if (cameraGo.GetComponent<CameraFollow>() == null)
            {
                cameraGo.AddComponent<CameraFollow>();
            }
        }

        private static void CreateFloor()
        {
            // Tiled wood floor — Kenney Roguelike Indoors plain plank
            // tile (cell 24,14 in the spritesheet). 16×11 fills the
            // interior of the 16×12 walled room with a 0.5 margin where
            // the bottom wall sits.
            var floorTile = PixelArtGenerator.Load(PixelArtGenerator.Floor);
            var floor = new GameObject("Floor");
            floor.transform.position = new Vector3(0f, 0f, 0.1f);
            floor.transform.localScale = Vector3.one;
            var sr = floor.AddComponent<SpriteRenderer>();
            sr.sprite = floorTile;
            sr.color = Color.white;
            sr.sortingOrder = 0;
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.size = new Vector2(13.6f, 9.6f);
        }

        private static void CreateRoomWalls()
        {
            // Day 13-B "Sims room": four solid wall rectangles framing
            // the play area. Walls use the master outline color
            // (#2B1B17) so the room boundary reads as a heavy frame
            // against the lighter wood floor inside. Sorting order 5
            // keeps walls below the chef (10) but above the floor (0).
            var wallSprite = AssetDatabase.LoadAssetAtPath<Sprite>(WhiteSquarePath);
            var wallColor = new Color(0.169f, 0.106f, 0.090f, 1f); // #2B1B17

            void Wall(string name, Vector3 pos, Vector2 size)
            {
                var go = new GameObject(name);
                go.transform.position = pos;
                go.transform.localScale = Vector3.one;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = wallSprite;
                sr.color = wallColor;
                sr.drawMode = SpriteDrawMode.Sliced;
                sr.size = size;
                sr.sortingOrder = 5;
            }

            // Day 13-B post-Flutter-recipe: room shrunk to fit ortho 5.2.
            // Inner interior: x ∈ [-7, +7], y ∈ [-4.7, +4.7].
            Wall("Wall_Top",    new Vector3(0f,  5.0f, 0f), new Vector2(14.6f, 0.6f));
            Wall("Wall_Bottom", new Vector3(0f, -5.0f, 0f), new Vector2(14.6f, 0.6f));
            Wall("Wall_Left",  new Vector3(-7.0f, 0f, 0f), new Vector2(0.6f, 10.6f));
            Wall("Wall_Right", new Vector3( 7.0f, 0f, 0f), new Vector2(0.6f, 10.6f));
        }

        private static GameObject CreatePlayer(TMP_FontAsset koreanFont)
        {
            var chefSprite = PixelArtGenerator.Load(PixelArtGenerator.Chef);
            var player = new GameObject("Player");
            player.tag = "Player";
            player.transform.position = Vector3.zero;
            // 48-px sprite at PPU 48 = 1 world unit. localScale 1.7
            // gives a chef ~65% the size of a 2.6-unit station — reads
            // clearly without crowding the kitchen.
            // Day 13-B "Sims room": chef at scale 1.2 reads as small but
            // visible against the 16×12 room. Was 2.2 in the older
            // single-screen-fills-the-canvas layout.
            player.transform.localScale = new Vector3(1.5f, 1.5f, 1f);
            var sr = player.AddComponent<SpriteRenderer>();
            sr.sprite = chefSprite;
            sr.color = Color.white;
            sr.sortingOrder = 10;
            // Day 13: PlayerController removed. The chef is driven
            // entirely by ChefAnimator from Gemini-issued actions; the
            // player never moves the chef directly, so listening for
            // WASD/arrow input only ever competed with text input
            // typing in the bottom field.

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

            // Day 13-B: held-item sprite — renders the actual ingredient
            // pixel icon next to the chef instead of "손에: 빵" text. The
            // text label still exists as a fallback for ingredients that
            // don't have a sprite mapped (none currently, but keeps the
            // system robust if a recipe adds a new IngredientType later).
            var itemGo = new GameObject("HeldItem");
            itemGo.transform.SetParent(player.transform, worldPositionStays: false);
            itemGo.transform.localPosition = new Vector3(0.55f, -0.45f, -0.1f);
            itemGo.transform.localScale = new Vector3(
                0.6f / player.transform.localScale.x,
                0.6f / player.transform.localScale.y,
                1f);
            var itemSr = itemGo.AddComponent<SpriteRenderer>();
            itemSr.sprite = null;
            itemSr.sortingOrder = 12;
            itemGo.SetActive(false);

            // Build the IngredientType → sprite lookup the animator uses.
            var ingSprites = new[]
            {
                (IngredientType.Bread,   PixelArtGenerator.Load(PixelArtGenerator.Ing_Bread)),
                (IngredientType.Patty,   PixelArtGenerator.Load(PixelArtGenerator.Ing_Patty)),
                (IngredientType.Cheese,  PixelArtGenerator.Load(PixelArtGenerator.Ing_Cheese)),
                (IngredientType.Lettuce, PixelArtGenerator.Load(PixelArtGenerator.Ing_Lettuce)),
                (IngredientType.Tomato,  PixelArtGenerator.Load(PixelArtGenerator.Ing_Tomato)),
                (IngredientType.Egg,     PixelArtGenerator.Load(PixelArtGenerator.Ing_Egg)),
            };

            // Day 13-B: little thought bubble that sits above the chef's
            // head while the Gemini call is in flight. Replaces the old
            // bottom-status row "셰프가 머리를 굴리는 중…" with an in-fiction
            // visual — players watch the chef, not text rows.
            var thinkRoot = new GameObject("ThinkingBubble");
            thinkRoot.transform.SetParent(player.transform, worldPositionStays: false);
            thinkRoot.transform.localPosition = new Vector3(0.0f, 1.5f, -0.1f);
            thinkRoot.transform.localScale = new Vector3(
                1f / player.transform.localScale.x,
                1f / player.transform.localScale.y,
                1f);

            var thinkBgGo = new GameObject("Bg");
            thinkBgGo.transform.SetParent(thinkRoot.transform, worldPositionStays: false);
            thinkBgGo.transform.localPosition = Vector3.zero;
            thinkBgGo.transform.localScale = new Vector3(1.6f, 1.0f, 1f);
            var thinkBg = thinkBgGo.AddComponent<SpriteRenderer>();
            thinkBg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(WhiteSquarePath);
            thinkBg.color = new Color(0.95f, 0.93f, 0.85f, 0.95f);
            thinkBg.sortingOrder = 12;

            var thinkTextGo = new GameObject("Text");
            thinkTextGo.transform.SetParent(thinkRoot.transform, worldPositionStays: false);
            thinkTextGo.transform.localPosition = new Vector3(0f, 0.0f, -0.05f);
            var thinkTmp = thinkTextGo.AddComponent<TextMeshPro>();
            thinkTmp.font = koreanFont;
            thinkTmp.fontSize = 2.6f;
            thinkTmp.alignment = TextAlignmentOptions.Center;
            thinkTmp.color = new Color(0.18f, 0.13f, 0.10f, 1f);
            thinkTmp.sortingOrder = 13;
            thinkTmp.text = "·";
            thinkTmp.rectTransform.sizeDelta = new Vector2(1.5f, 1.0f);
            EditorUtility.SetDirty(thinkTmp);
            thinkRoot.SetActive(false);

            // Same SerializedObject dance as Customer._orderBubble — a
            // direct setter doesn't survive into the saved scene YAML.
            var serialized = new SerializedObject(animator);
            serialized.FindProperty("_heldLabel").objectReferenceValue = tmp;
            serialized.FindProperty("_heldItemSr").objectReferenceValue = itemSr;
            serialized.FindProperty("_thinkingRoot").objectReferenceValue = thinkRoot;
            serialized.FindProperty("_thinkingText").objectReferenceValue = thinkTmp;
            var arr = serialized.FindProperty("_ingredientSprites");
            arr.arraySize = ingSprites.Length;
            for (var i = 0; i < ingSprites.Length; i++)
            {
                var elem = arr.GetArrayElementAtIndex(i);
                elem.FindPropertyRelative("type").enumValueIndex = (int)ingSprites[i].Item1;
                elem.FindPropertyRelative("sprite").objectReferenceValue = ingSprites[i].Item2;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return player;
        }

        private static void CreateStation(string name, StationType type, string label,
                                           Vector3 pos, Color color, Color burstColor,
                                           TMP_FontAsset koreanFont,
                                           Sprite bodySprite = null,
                                           Vector3? bodyScale = null)
        {
            var burstSprite = AssetDatabase.LoadAssetAtPath<Sprite>(WhiteSquarePath);
            var sprite = bodySprite != null ? bodySprite : burstSprite;
            var go = new GameObject(name);
            go.transform.position = pos;
            go.transform.localScale = bodyScale ?? new Vector3(2.2f, 1.6f, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = 5;
            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = Vector2.one;

            var marker = go.AddComponent<StationMarker>();
            // Burst still uses the white square so particles are crisp
            // and visibly tinted, regardless of which body sprite the
            // station wears.
            marker.Configure(type, label, burstColor, burstSprite);

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(go.transform, worldPositionStays: false);
            // Day 13-B polish: label dropped below the station body so
            // it stops fighting with the screen-space order panel that
            // sits at the top of the canvas. Smaller font (3f instead
            // of 4f) keeps it inside the station footprint.
            labelGo.transform.localPosition = new Vector3(0f, -0.55f, -0.1f);
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
            tmp.fontSize = 3f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.black;
            tmp.sortingOrder = 11;
            EditorUtility.SetDirty(tmp);
        }

        private static GameObject CreateCustomer(Vector3 pos, Color color, TMP_FontAsset koreanFont)
        {
            var customerSprite = PixelArtGenerator.Load(PixelArtGenerator.Customer);
            var go = new GameObject("Customer");
            go.transform.position = pos;
            // Day 13-B "Sims room": match the chef scale.
            go.transform.localScale = new Vector3(1.5f, 1.5f, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = customerSprite;
            sr.color = Color.white;
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

            // Day 13 v2: ASCII face label removed — the customer
            // sprite now ships its own baked-in face, and stacking a
            // TMP `:|` on top of that face read as "two faces overlapping".
            // Mood feedback survives via the right-side result panel.

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

        private static (GameObject root, TMP_Text text) CreateChefMonologue(
            GameObject player, TMP_FontAsset koreanFont)
        {
            // World-space speech bubble parented under the chef so the
            // ChefAnimator's transform animation drags it along. The
            // bubble has its own SpriteRenderer bg (white square, dark
            // tint) plus a TextMeshPro foreground at higher sortingOrder.
            // Inactive by default — KitchenHUD.StartMonologue toggles it.
            var rootGo = new GameObject("MonologueBubble");
            rootGo.transform.SetParent(player.transform, worldPositionStays: false);
            // localScale of player is 1.7. Apply inverse so the bubble
            // reads at the same world-space size regardless of chef
            // scale tweaks (matches the HeldLabel pattern just above).
            // Day 13-B: 1.2 puts the bubble visually attached to the
            // chef's head (sits right above the cook). Smaller offset
            // also gives more vertical headroom at the y=3 station row.
            rootGo.transform.localPosition = new Vector3(0f, 1.2f, -0.1f);
            rootGo.transform.localScale = new Vector3(
                1f / player.transform.localScale.x,
                1f / player.transform.localScale.y,
                1f);

            var bgGo = new GameObject("Bg");
            bgGo.transform.SetParent(rootGo.transform, worldPositionStays: false);
            bgGo.transform.localPosition = Vector3.zero;
            // 6.4 wide × 2.2 tall — sized for fontSize 2.2 so the
            // chef's monologue reads big at room-overview camera ortho
            // 6.5. Bubble extends slightly above chef but stops well
            // below the room top wall (y=5.5).
            bgGo.transform.localScale = new Vector3(6.4f, 2.2f, 1f);
            var bgSr = bgGo.AddComponent<SpriteRenderer>();
            bgSr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(WhiteSquarePath);
            bgSr.color = new Color(0.12f, 0.09f, 0.07f, 0.94f);
            bgSr.sortingOrder = 12;

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(rootGo.transform, worldPositionStays: false);
            textGo.transform.localPosition = new Vector3(0f, 0f, -0.05f);
            var tmp = textGo.AddComponent<TextMeshPro>();
            tmp.font = koreanFont;
            tmp.fontSize = 2.2f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(1f, 0.96f, 0.82f, 1f);
            tmp.sortingOrder = 13;
            tmp.enableWordWrapping = true;
            tmp.text = string.Empty;
            tmp.rectTransform.sizeDelta = new Vector2(6.0f, 2.0f);
            EditorUtility.SetDirty(tmp);

            rootGo.SetActive(false);
            return (rootGo, tmp);
        }

        private static KitchenHUD CreateKitchenHud(
            TMP_FontAsset koreanFont, GameObject monologueRoot, TMP_Text monologueText)
        {
            // Screen Space Overlay so the HUD never moves with the
            // world camera. EventSystem is required for TMP_InputField
            // submit + button click; create both fresh on every Setup
            // run for idempotence.
            var canvasGo = new GameObject("HUDCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            var eventSystemGo = new GameObject("EventSystem");
            eventSystemGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystemGo.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();

            var hud = canvasGo.AddComponent<KitchenHUD>();

            // Day 13-B: a single OrderCard container holds counter +
            // title + components so they read as one block (Gestalt
            // proximity). Anchored top-center, warm dark brown bg
            // (#1E1612) to share palette with the chef sprite outline
            // instead of the previous cool slate that fought it.
            var cardGo = new GameObject("OrderCard");
            var cardRt = cardGo.AddComponent<RectTransform>();
            cardRt.SetParent(canvasGo.transform, false);
            cardRt.anchorMin = new Vector2(0.5f, 1f);
            cardRt.anchorMax = new Vector2(0.5f, 1f);
            cardRt.pivot = new Vector2(0.5f, 1f);
            cardRt.anchoredPosition = new Vector2(0f, -12f);
            cardRt.sizeDelta = new Vector2(560f, 140f);
            var cardBg = cardGo.AddComponent<Image>();
            cardBg.color = new Color(0.118f, 0.086f, 0.071f, 0.92f);

            var orderCounter = CreateText(cardGo.transform, "OrderCounter",
                anchorMin: new Vector2(0f, 1f), anchorMax: new Vector2(0f, 1f),
                pivot: new Vector2(0f, 1f), anchored: new Vector2(14f, -8f),
                size: new Vector2(160f, 22f),
                fontSize: 12, color: new Color(0.78f, 0.78f, 0.86f, 0.95f), font: koreanFont,
                bg: null);
            orderCounter.alignment = TextAlignmentOptions.TopLeft;

            var orderTitle = CreateText(cardGo.transform, "OrderTitle",
                anchorMin: new Vector2(0.5f, 1f), anchorMax: new Vector2(0.5f, 1f),
                pivot: new Vector2(0.5f, 1f), anchored: new Vector2(0f, -24f),
                size: new Vector2(540f, 36f),
                fontSize: 26, color: new Color(0.96f, 0.91f, 0.78f, 1f), font: koreanFont,
                bg: null);
            orderTitle.alignment = TextAlignmentOptions.Center;

            // Day 13-B: chip row replaces the old text "빵 → 익힘  •  …"
            // because first-time players couldn't decode the arrow syntax.
            // KitchenHUD.BuildComponentChips fills this container with one
            // [icon + 한글 라벨] chip per recipe component.
            var rowGo = new GameObject("OrderComponentsRow",
                typeof(RectTransform), typeof(HorizontalLayoutGroup));
            var rowRt = (RectTransform)rowGo.transform;
            rowRt.SetParent(cardGo.transform, false);
            rowRt.anchorMin = new Vector2(0.5f, 0f);
            rowRt.anchorMax = new Vector2(0.5f, 0f);
            rowRt.pivot = new Vector2(0.5f, 0f);
            rowRt.anchoredPosition = new Vector2(0f, 6f);
            rowRt.sizeDelta = new Vector2(540f, 64f);
            var hlg = rowGo.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = 10f;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;

            // Input panel intentionally NOT created on the Unity canvas —
            // macOS WKWebView's TMP_InputField IME composition broke
            // hangul into bare jamo. The Flutter shell hosts a native
            // text field instead and pushes the composed string through
            // BridgeIncoming.SubmitInstruction.
            TMP_InputField input = null;
            Button sendBtn = null;

            // Status row sits directly under the OrderCard so the
            // "셰프 생각 중…" feedback during the 2-4s Gemini call
            // appears in the player's primary attention zone instead
            // of at the bottom of the screen where it used to hide.
            var statusText = CreateText(canvasGo.transform, "StatusText",
                anchorMin: new Vector2(0.5f, 1f), anchorMax: new Vector2(0.5f, 1f),
                pivot: new Vector2(0.5f, 1f), anchored: new Vector2(0f, -118f),
                size: new Vector2(420f, 26f),
                fontSize: 13, color: new Color(1f, 0.85f, 0.35f, 1f), font: koreanFont,
                bg: null);
            statusText.alignment = TextAlignmentOptions.Center;

            var so = new SerializedObject(hud);
            so.FindProperty("_orderCardRoot").objectReferenceValue = cardGo;
            so.FindProperty("_orderTitle").objectReferenceValue = orderTitle;
            so.FindProperty("_orderCounter").objectReferenceValue = orderCounter;
            so.FindProperty("_orderComponentsRow").objectReferenceValue = rowGo.GetComponent<RectTransform>();
            so.FindProperty("_chipFont").objectReferenceValue = koreanFont;
            var iconArr = so.FindProperty("_ingredientIcons");
            var iconEntries = new[]
            {
                (IngredientType.Bread,   PixelArtGenerator.Load(PixelArtGenerator.Ing_Bread)),
                (IngredientType.Patty,   PixelArtGenerator.Load(PixelArtGenerator.Ing_Patty)),
                (IngredientType.Cheese,  PixelArtGenerator.Load(PixelArtGenerator.Ing_Cheese)),
                (IngredientType.Lettuce, PixelArtGenerator.Load(PixelArtGenerator.Ing_Lettuce)),
                (IngredientType.Tomato,  PixelArtGenerator.Load(PixelArtGenerator.Ing_Tomato)),
                (IngredientType.Egg,     PixelArtGenerator.Load(PixelArtGenerator.Ing_Egg)),
            };
            iconArr.arraySize = iconEntries.Length;
            for (var i = 0; i < iconEntries.Length; i++)
            {
                var elem = iconArr.GetArrayElementAtIndex(i);
                elem.FindPropertyRelative("type").enumValueIndex = (int)iconEntries[i].Item1;
                elem.FindPropertyRelative("sprite").objectReferenceValue = iconEntries[i].Item2;
            }
            so.FindProperty("_monologueRoot").objectReferenceValue = monologueRoot;
            so.FindProperty("_monologueText").objectReferenceValue = monologueText;
            so.FindProperty("_input").objectReferenceValue = input;
            so.FindProperty("_sendButton").objectReferenceValue = sendBtn;
            so.FindProperty("_statusText").objectReferenceValue = statusText;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(hud);
            return hud;
        }

        private static TMP_Text CreateText(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchored,
            Vector2 size, int fontSize, Color color, TMP_FontAsset font, Color? bg)
        {
            var go = new GameObject(name);
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchored;
            rt.sizeDelta = size;
            if (bg.HasValue)
            {
                var img = go.AddComponent<Image>();
                img.color = bg.Value;
            }
            var labelGo = new GameObject("Label");
            var labelRt = labelGo.AddComponent<RectTransform>();
            labelRt.SetParent(rt, false);
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = new Vector2(12f, 6f);
            labelRt.offsetMax = new Vector2(-12f, -6f);
            var tmp = labelGo.AddComponent<TextMeshProUGUI>();
            tmp.font = font;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.text = string.Empty;
            return tmp;
        }

        private static TMP_InputField CreateInputField(Transform parent, TMP_FontAsset font)
        {
            var go = new GameObject("Input");
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(16f, -22f);
            rt.offsetMax = new Vector2(-120f, 22f);

            var img = go.AddComponent<Image>();
            img.color = new Color(0.16f, 0.16f, 0.20f, 1f);
            var input = go.AddComponent<TMP_InputField>();

            var textArea = new GameObject("TextArea");
            var taRt = textArea.AddComponent<RectTransform>();
            taRt.SetParent(rt, false);
            taRt.anchorMin = Vector2.zero;
            taRt.anchorMax = Vector2.one;
            taRt.offsetMin = new Vector2(12f, 4f);
            taRt.offsetMax = new Vector2(-12f, -4f);
            textArea.AddComponent<RectMask2D>();

            var placeholderGo = new GameObject("Placeholder");
            var phRt = placeholderGo.AddComponent<RectTransform>();
            phRt.SetParent(taRt, false);
            phRt.anchorMin = Vector2.zero;
            phRt.anchorMax = Vector2.one;
            phRt.offsetMin = Vector2.zero;
            phRt.offsetMax = Vector2.zero;
            var ph = placeholderGo.AddComponent<TextMeshProUGUI>();
            ph.font = font;
            ph.fontSize = 18;
            ph.color = new Color(1f, 1f, 1f, 0.4f);
            ph.alignment = TextAlignmentOptions.MidlineLeft;
            ph.text = "한국어로 지시 입력 후 Enter…";

            var textGo = new GameObject("Text");
            var txRt = textGo.AddComponent<RectTransform>();
            txRt.SetParent(taRt, false);
            txRt.anchorMin = Vector2.zero;
            txRt.anchorMax = Vector2.one;
            txRt.offsetMin = Vector2.zero;
            txRt.offsetMax = Vector2.zero;
            var tx = textGo.AddComponent<TextMeshProUGUI>();
            tx.font = font;
            tx.fontSize = 18;
            tx.color = Color.white;
            tx.alignment = TextAlignmentOptions.MidlineLeft;

            input.textViewport = taRt;
            input.textComponent = tx;
            input.placeholder = ph;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.characterLimit = 80;
            input.onFocusSelectAll = false;
            return input;
        }

        private static Button CreateSendButton(Transform parent, TMP_FontAsset font)
        {
            var go = new GameObject("SendButton");
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(1f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.anchoredPosition = new Vector2(-16f, 0f);
            rt.sizeDelta = new Vector2(96f, 44f);

            var img = go.AddComponent<Image>();
            img.color = new Color(0.95f, 0.78f, 0.30f, 1f);
            var btn = go.AddComponent<Button>();

            var labelGo = new GameObject("Label");
            var labelRt = labelGo.AddComponent<RectTransform>();
            labelRt.SetParent(rt, false);
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;
            var tmp = labelGo.AddComponent<TextMeshProUGUI>();
            tmp.font = font;
            tmp.fontSize = 18;
            tmp.color = new Color(0.15f, 0.10f, 0.05f, 1f);
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.text = "지시 전송";
            return btn;
        }

        // BadKitchen.png is 320×120, 5 cols × 2 rows of 64×60 cells
        // (CC0 from OpenGameArt). Layout:
        //   Row 0 (top):    Cabinet, Pan, Bottle, Plate, Jar
        //   Row 1 (bottom): Table,   Chair, Stove, Fridge, Wall
        // We slice once at scene-setup time so the importer settings
        // and SpriteRect array survive headless rebuilds (the
        // build-webgl scripts call Setup → this method).
        private static readonly (string name, int col, int row)[] BadKitchenCells =
        {
            ("BadKitchen_Cabinet", 0, 0),
            ("BadKitchen_Pan",     1, 0),
            ("BadKitchen_Bottle",  2, 0),
            ("BadKitchen_Plate",   3, 0),
            ("BadKitchen_Jar",     4, 0),
            ("BadKitchen_Table",   0, 1),
            ("BadKitchen_Chair",   1, 1),
            ("BadKitchen_Stove",   2, 1),
            ("BadKitchen_Fridge",  3, 1),
            ("BadKitchen_Wall",    4, 1),
        };

        private static void EnsureBadKitchenSliced()
        {
            if (!File.Exists(BadKitchenPath))
            {
                Debug.LogWarning(
                    $"[MainKitchenSetup] {BadKitchenPath} missing — stations will fall " +
                    "back to colored white squares.");
                return;
            }
            var importer = (TextureImporter)AssetImporter.GetAtPath(BadKitchenPath);
            if (importer == null) return;

            const int cellW = 64;
            const int cellH = 60;
            const int rows = 2;

            var rects = new List<SpriteMetaData>(BadKitchenCells.Length);
            foreach (var cell in BadKitchenCells)
            {
                // Unity's sprite y-axis is bottom-up. Our `row` enum is
                // top-down (0 = visually top), so flip on the way in.
                var unityRow = (rows - 1) - cell.row;
                rects.Add(new SpriteMetaData
                {
                    name = cell.name,
                    rect = new Rect(cell.col * cellW, unityRow * cellH, cellW, cellH),
                    pivot = new Vector2(0.5f, 0.5f),
                    alignment = (int)SpriteAlignment.Center,
                });
            }

            // Pixel art settings: point filter, no compression, PPU
            // tuned so a single 64-px sprite at localScale=2.2 renders
            // ~4.4 world units (matching the previous white-square
            // station footprint).
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            // PPU 48 keeps a 64-px sprite at ~1.33 world units; the
            // per-station localScale ~1.5 below brings the rendered
            // body to ~2 world units, which fits inside the 4-unit
            // spacing between stations without overlap.
            importer.spritePixelsPerUnit = 48f;
            importer.isReadable = false;
            importer.spritesheet = rects.ToArray();
            importer.SaveAndReimport();
            Debug.Log($"[MainKitchenSetup] BadKitchen.png sliced into {rects.Count} sprites.");
        }

        private static Sprite LoadBadKitchenSprite(string spriteName)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(BadKitchenPath);
            foreach (var a in assets)
            {
                if (a is Sprite s && s.name == spriteName) return s;
            }
            Debug.LogWarning($"[MainKitchenSetup] sprite '{spriteName}' missing in BadKitchen sheet.");
            return null;
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
