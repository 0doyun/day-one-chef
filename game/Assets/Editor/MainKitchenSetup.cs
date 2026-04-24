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
using DayOneChef.Gameplay;

namespace DayOneChef.Editor
{
    public static class MainKitchenSetup
    {
        private const string ScenePath = "Assets/Scenes/MainKitchen.unity";
        private const string SpriteDir = "Assets/Sprites";
        private const string WhiteSquarePath = "Assets/Sprites/WhiteSquare.png";
        private const string KoreanFontAssetPath = "Assets/Fonts/NotoSansKR SDF.asset";

        [MenuItem("Tools/Day One Chef/Setup Main Kitchen")]
        public static void Setup()
        {
            EnsureWhiteSquareSprite();

            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.DefaultGameObjects,
                NewSceneMode.Single);

            ConfigureCamera(out var cameraGo);
            CreateFloor();
            var playerGo = CreatePlayer();
            cameraGo.GetComponent<CameraFollow>().Target = playerGo.transform;

            CreateStation("Station_Fridge",       StationType.Fridge,       "냉장고",
                new Vector3(-7f,  4f, 0f), new Color(0.56f, 0.79f, 0.90f, 1f));
            CreateStation("Station_CuttingBoard", StationType.CuttingBoard, "도마",
                new Vector3(-3f,  4f, 0f), new Color(0.80f, 0.63f, 0.42f, 1f));
            CreateStation("Station_Stove",        StationType.Stove,        "화구",
                new Vector3( 3f,  4f, 0f), new Color(0.90f, 0.40f, 0.30f, 1f));
            CreateStation("Station_Assembly",     StationType.Assembly,     "조립대",
                new Vector3( 7f,  4f, 0f), new Color(0.85f, 0.85f, 0.85f, 1f));
            CreateStation("Station_Counter",      StationType.Counter,      "카운터",
                new Vector3( 0f, -4f, 0f), new Color(0.45f, 0.75f, 0.55f, 1f));

            var sceneDir = Path.GetDirectoryName(ScenePath);
            if (!string.IsNullOrEmpty(sceneDir)) Directory.CreateDirectory(sceneDir);
            EditorSceneManager.SaveScene(scene, ScenePath);

            RegisterSceneInBuildSettings(ScenePath);
            Debug.Log($"[MainKitchenSetup] Wrote {ScenePath} with 5 stations + player + camera follow.");
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

        private static GameObject CreatePlayer()
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
            return player;
        }

        private static void CreateStation(string name, StationType type, string label,
                                           Vector3 pos, Color color)
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
            tmp.text = label;
            tmp.fontSize = 4f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.black;
            var koreanFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(KoreanFontAssetPath);
            if (koreanFont != null) tmp.font = koreanFont;
            tmp.sortingOrder = 11;
        }

        private static void EnsureWhiteSquareSprite()
        {
            if (File.Exists(WhiteSquarePath))
            {
                return;
            }

            Directory.CreateDirectory(SpriteDir);

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

            var importer = (TextureImporter)AssetImporter.GetAtPath(WhiteSquarePath);
            if (importer == null)
            {
                Debug.LogError($"[MainKitchenSetup] TextureImporter missing for {WhiteSquarePath}.");
                return;
            }
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
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
