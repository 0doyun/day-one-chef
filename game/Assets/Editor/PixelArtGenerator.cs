// Procedural top-down pixel art for Day One Chef. Editor-only script
// that paints 48×48 PNGs straight from code, configured as Sprites
// with Point filter and PPU 48 so a single sprite renders ~1 world
// unit at localScale 1.
//
// Why generated, not downloaded: the externally-sourced kitchen packs
// we tried (BadKitchen, etc.) all use a side perspective, which clashes
// with the game's strict top-down view. Generating in code keeps the
// silhouette consistent (everything is a tight 48×48 top-down icon),
// the palette unified, and the asset pipeline reproducible — the PNGs
// are derived artifacts, not committed source-of-truth.

#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DayOneChef.Editor
{
    public static class PixelArtGenerator
    {
        public const string GeneratedDir = "Assets/Sprites/Generated";
        // Day 13 v2: source resolution dropped from 48 to 24 so each
        // source pixel renders as a chunky block on screen — gives the
        // "low-resolution, deliberately silly" look the design wants.
        // PPU drops in lockstep so 1 sprite still = 1 world unit base.
        public const int CanvasSize = 24;
        public const float PixelsPerUnit = 24f;

        public const string Chef         = "Chef";
        public const string Customer     = "Customer";
        public const string Fridge       = "Fridge";
        public const string CuttingBoard = "CuttingBoard";
        public const string Stove        = "Stove";
        public const string Assembly     = "Assembly";
        public const string Counter      = "Counter";
        public const string Floor        = "FloorTile";

        // Day 13-B ingredient icons — 16-px chunky pixel art per recipe
        // ingredient. Drawn from scratch so the held-item sprite + recipe
        // hint UI share exactly the same icon vocabulary.
        public const string Ing_Bread    = "Ing_Bread";
        public const string Ing_Patty    = "Ing_Patty";
        public const string Ing_Cheese   = "Ing_Cheese";
        public const string Ing_Lettuce  = "Ing_Lettuce";
        public const string Ing_Tomato   = "Ing_Tomato";
        public const string Ing_Egg      = "Ing_Egg";

        // --- Master palette (12 colors) --------------------------------------
        // Day 13-B: every sprite color must come from this list. Variable
        // names below are aliases that point INTO this palette so painters
        // can stay readable. Result: kitchen reads as one illustration
        // instead of 8 disjoint sprites.
        //
        // Inspired by Endesga-32 (warm subset). Outline is forced to a
        // single deep brown so all silhouettes share the same edge tone.
        private static readonly Color32 P_Outline    = new(43, 27, 23, 255);   // #2B1B17
        private static readonly Color32 P_Cream      = new(242, 237, 224, 255); // #F2EDE0
        private static readonly Color32 P_Skin       = new(245, 200, 138, 255); // #F5C88A
        private static readonly Color32 P_Yellow     = new(240, 184, 48, 255);  // #F0B830
        private static readonly Color32 P_Tan        = new(200, 140, 80, 255);  // #C88C50
        private static readonly Color32 P_Red        = new(212, 64, 48, 255);   // #D44030
        private static readonly Color32 P_Green      = new(112, 168, 64, 255);  // #70A840
        private static readonly Color32 P_Pink       = new(216, 124, 160, 255); // #D87CA0
        private static readonly Color32 P_GrayLight  = new(184, 176, 172, 255); // #B8B0AC
        private static readonly Color32 P_GrayDark   = new(92, 80, 80, 255);    // #5C5050
        private static readonly Color32 P_FloorA     = new(196, 172, 132, 255); // #C4AC84
        private static readonly Color32 P_FloorB     = new(178, 152, 116, 255); // #B29874

        // Aliases — painters call these names; values point to the master.
        // 2-tone + 1 accent rule: each sprite uses (a) outline, (b) one
        // body color, (c) one shadow tone, (d) at most one accent color.
        private static readonly Color32 Outline      = P_Outline;

        // Chef: yellow body + cream toque + skin face. Accent: none (the
        // toque IS the accent).
        private static readonly Color32 ChefHat      = P_Cream;
        private static readonly Color32 ChefShirt    = P_Yellow;
        private static readonly Color32 ChefSkin     = P_Skin;

        // Customer: pink body + same skin. Hair is just outline color so
        // it reads as a heavy block silhouette.
        private static readonly Color32 CustShirt    = P_Pink;
        private static readonly Color32 CustHair     = P_Outline;

        // Fridge: cream body + gray-light shadow + red magnet accent.
        private static readonly Color32 FridgeBody   = P_Cream;
        private static readonly Color32 FridgeShade  = P_GrayLight;
        private static readonly Color32 FridgeHandle = P_GrayDark;
        private static readonly Color32 FridgeMagnet = P_Red;

        // Cutting board: tan top + outline edge + green herb accent +
        // shared knife (gray-light blade, gray-dark handle).
        private static readonly Color32 BoardTop     = P_Tan;
        private static readonly Color32 BoardSide    = P_Outline;
        private static readonly Color32 KnifeBlade   = P_GrayLight;
        private static readonly Color32 KnifeHandle  = P_GrayDark;
        private static readonly Color32 VegLeaf      = P_Green;

        // Stove: gray-dark body + outline cooktop + gray-light burner ring
        // + red burner accent (the only "hot" thing).
        private static readonly Color32 StoveBody    = P_GrayDark;
        private static readonly Color32 StoveTop     = P_Outline;
        private static readonly Color32 BurnerRing   = P_GrayLight;
        private static readonly Color32 BurnerHot    = P_Red;

        // Assembly: cream top + gray-light edge shadow + green herb
        // accent (the only color thing — all 5 stations now have one
        // accent, so Assembly stops looking like a placeholder).
        private static readonly Color32 AssemblyTop  = P_Cream;
        private static readonly Color32 AssemblySide = P_GrayLight;

        // Counter: tan wood + outline edge + cream plate + yellow food
        // accent (the only "appetizing" color in the kitchen by intent).
        private static readonly Color32 CounterWood  = P_Tan;
        private static readonly Color32 CounterEdge  = P_Outline;
        private static readonly Color32 PlateColor   = P_Cream;
        private static readonly Color32 PlateFood    = P_Yellow;
        private static readonly Color32 BreadCrust   = P_Tan;

        // Floor: SOLID warm-tan, no seams or pattern. Kenney station
        // sprites provide all visual structure; floor is just a stage.
        private static readonly Color32 FloorBase    = P_FloorA;
        private static readonly Color32 FloorTone    = P_FloorA; // identical to base
        private static readonly Color32 FloorSeam    = P_FloorA; // identical to base

        public static void GenerateAll()
        {
            Directory.CreateDirectory(GeneratedDir);
            // Day 13-B: characters AND stations now come from external
            // CC0 Kenney packs (kenney.nl, public domain):
            //   - Chef / Customer  ← Roguelike Characters (16×16)
            //   - Fridge / CuttingBoard / Stove / Assembly / Counter
            //                      ← Roguelike Indoor pack (16×16)
            // The PNGs are placed next to the procedural ones; we only
            // re-import them with Point filter + PPU 16 so they read
            // crisp at the player's localScale. Floor is still drawn
            // procedurally (we want a low-contrast tiled floor that
            // doesn't compete with the Kenney furniture).
            ConfigureExternalSprite(Chef,         16f);
            ConfigureExternalSprite(Customer,     16f);
            ConfigureExternalSprite(Fridge,       16f);
            ConfigureExternalSprite(CuttingBoard, 16f);
            ConfigureExternalSprite(Stove,        16f);
            ConfigureExternalSprite(Assembly,     16f);
            ConfigureExternalSprite(Counter,      16f);
            // Floor: revert to procedural single-color tile. The Kenney
            // wood-plank floor showed prominent horizontal seams that
            // tiled into "venetian blinds" stripes across the room.
            // A clean solid warm-tan reads better as a stage for the
            // Kenney furniture and characters.
            WriteSprite(Floor, BuildFloor);
            // Day 13-B v2: ingredient icons swapped from procedural to
            // OpenGameArt CC0 32×32 pixel sprites (food-ocal pack —
            // grain/bread_loaf, meat/steak+egg_large, dairy/cheese_wedge,
            // vegetable/lettuce, fruit/tomato). They share the same
            // shaded-pixel-art aesthetic as the Kenney character pack
            // so the chef + held ingredient read as one game.
            ConfigureExternalSprite(Ing_Bread,   32f);
            ConfigureExternalSprite(Ing_Patty,   32f);
            ConfigureExternalSprite(Ing_Cheese,  32f);
            ConfigureExternalSprite(Ing_Lettuce, 32f);
            ConfigureExternalSprite(Ing_Tomato,  32f);
            ConfigureExternalSprite(Ing_Egg,     32f);
            AssetDatabase.Refresh();
        }

        private static void ConfigureExternalSprite(string name, float ppu)
        {
            var path = $"{GeneratedDir}/{name}.png";
            if (!File.Exists(path)) return;
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.spritePixelsPerUnit = ppu;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }

        public static Sprite Load(string name)
        {
            var path = $"{GeneratedDir}/{name}.png";
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // --- Sprite painters ------------------------------------------------

        private static void BuildChef(Color32[] px, int s)
        {
            // Day 13-B redo #3 — art-director redesign based on
            // external references: Kenney Roguelike Characters, Overcooked
            // Dribbble pixel chef, SLYNYRD pixelblog-22, Cainos top-down.
            //
            // Common rule from those refs: TOQUE IS A RECTANGLE, not a
            // disc. A circle reads as Pac-Man at this resolution — every
            // working pixel chef in the wild uses a tall narrow
            // rectangle for the toque, with a slightly wider brim band.
            //
            // Layout (24×24, y=0 is bottom):
            //   y=15..21 — toque body (8 wide × 7 tall rectangle)
            //   y=13..14 — toque brim (10 wide × 2)
            //   y= 6..12 — shirt block (14 wide × 7)
            //   y= 6..12 — apron accent stripe (4 wide, centered)

            // Toque body — 8×7 with full outline.
            FillRect(px, s, 8, 15, 8, 7, ChefHat);
            DrawOutlineRect(px, s, 8, 15, 8, 7, Outline);

            // Brim — 10×2 cream band. Skip top/bottom outline (would
            // overwrite the entire band since perimeter = area at h=2);
            // outline only the left and right ends so the brim flares
            // out from under the toque.
            FillRect(px, s, 7, 13, 10, 2, ChefHat);
            SetSafe(px, s, 7,  13, Outline);
            SetSafe(px, s, 7,  14, Outline);
            SetSafe(px, s, 16, 13, Outline);
            SetSafe(px, s, 16, 14, Outline);

            // Shirt block — 14×7 with full outline.
            FillRect(px, s, 5, 6, 14, 7, ChefShirt);
            DrawOutlineRect(px, s, 5, 6, 14, 7, Outline);

            // Apron stripe — 4-wide cream column down the centre with
            // 1-px outline rails so it reads as a stripe, not a hole.
            FillRect(px, s, 10, 6, 4, 7, ChefHat);
            for (var y = 6; y <= 12; y++)
            {
                SetSafe(px, s, 10, y, Outline);
                SetSafe(px, s, 13, y, Outline);
            }
        }

        private static void BuildCustomer(Color32[] px, int s)
        {
            // Day 13-B redo #3 — Cainos / SLYNYRD reference.
            // RULE: head = solid filled rectangle, no face pixels.
            // Hair color + body color are the only identity markers.
            // Customer is wider-shorter (head 10×7) so the silhouette
            // differs from the chef (toque 8×7 narrow-tall) at a glance.
            //
            // Layout (24×24):
            //   y=15..21 — head block (10 wide × 7 tall, dark hair)
            //   y=13..14 — neck skin strip (4 wide × 2)
            //   y= 6..12 — shirt block (14 wide × 7, pink)

            FillRect(px, s, 7, 15, 10, 7, CustHair);
            DrawOutlineRect(px, s, 7, 15, 10, 7, Outline);

            FillRect(px, s, 10, 13, 4, 2, ChefSkin);

            FillRect(px, s, 5, 6, 14, 7, CustShirt);
            DrawOutlineRect(px, s, 5, 6, 14, 7, Outline);
        }

        private static void BuildFridge(Color32[] px, int s)
        {
            // 2-tone + 1 accent: cream body + gray-light bottom-shadow,
            // single red magnet as the only color note.
            // Identity pixels: door split | handle | magnet (=3).
            FillRect(px, s, 4, 2, 16, 20, FridgeBody);
            DrawOutlineRect(px, s, 4, 2, 16, 20, Outline);
            FillRect(px, s, 4, 2, 16, 2, FridgeShade);  // base shadow band
            FillRect(px, s, 4, 11, 16, 1, Outline);     // door split
            FillRect(px, s, 17, 13, 1, 5, FridgeHandle); // single handle
            FillRect(px, s, 7, 16, 3, 3, FridgeMagnet); // accent magnet
            DrawOutlineRect(px, s, 7, 16, 3, 3, Outline);
        }

        private static void BuildCuttingBoard(Color32[] px, int s)
        {
            // 2-tone + 1 accent: tan top + outline shadow, green herb.
            // Identity pixels: knife | herb sprig | grain mark (=3).
            FillRect(px, s, 2, 6, 20, 12, BoardTop);
            DrawOutlineRect(px, s, 2, 6, 20, 12, Outline);
            FillRect(px, s, 2, 6, 20, 1, BoardSide); // bottom shadow
            // Knife (1 unit): blade + handle, single outline pass.
            FillRect(px, s, 12, 11, 8, 2, KnifeBlade);
            FillRect(px, s, 8,  11, 4, 2, KnifeHandle);
            DrawOutlineRect(px, s, 8, 11, 12, 2, Outline);
            // Herb sprig (1 unit): 3 green pixels in the corner.
            FillRect(px, s, 4, 14, 2, 2, VegLeaf);
            SetSafe(px, s, 6, 15, VegLeaf);
            // Grain mark (1 unit): single pixel scratch line.
            FillRect(px, s, 4, 9, 4, 1, BoardSide);
        }

        private static void BuildStove(Color32[] px, int s)
        {
            // 2-tone + 1 accent: gray-dark body + outline cooktop, red
            // burners. Knob row dropped to match other stations'
            // 3-identity-pixel budget.
            // Identity pixels: cooktop frame | burner left | burner right (=3).
            FillRect(px, s, 3, 2, 18, 20, StoveBody);
            DrawOutlineRect(px, s, 3, 2, 18, 20, Outline);
            FillRect(px, s, 5, 7, 14, 13, StoveTop);
            DrawOutlineRect(px, s, 5, 7, 14, 13, Outline);
            FillCircleOutlined(px, s, 9,  13, 3, BurnerRing, Outline);
            FillRect(px, s, 8, 12, 3, 3, BurnerHot);
            FillCircleOutlined(px, s, 15, 13, 3, BurnerRing, Outline);
            FillRect(px, s, 14, 12, 3, 3, BurnerHot);
        }

        private static void BuildAssembly(Color32[] px, int s)
        {
            // Day 13-B: was a blank rectangle reading as "missing asset".
            // Now 2-tone + 1 accent matching the other stations.
            // Identity pixels: prep grid | mixing bowl | herb (=3).
            FillRect(px, s, 2, 6, 20, 12, AssemblyTop);
            DrawOutlineRect(px, s, 2, 6, 20, 12, Outline);
            FillRect(px, s, 2, 6, 20, 1, AssemblySide);
            FillRect(px, s, 2, 17, 20, 1, AssemblySide);
            // Prep grid (1 unit): a 3-pixel chopping mark mid-counter.
            FillRect(px, s, 5, 11, 5, 1, AssemblySide);
            FillRect(px, s, 5, 13, 5, 1, AssemblySide);
            // Mixing bowl (1 unit): small outlined cream disc on the
            // right — gives the eye something to land on.
            FillCircleOutlined(px, s, 16, 12, 3, AssemblyTop, Outline);
            FillRect(px, s, 14, 12, 5, 2, AssemblySide);
            // Herb accent (1 unit): green sprig keeps Assembly in the
            // same color family as the cutting board.
            FillRect(px, s, 17, 14, 2, 2, VegLeaf);
        }

        private static void BuildCounter(Color32[] px, int s)
        {
            // 2-tone + 1 accent: tan wood + outline edge, yellow food
            // on the cream plate.
            // Identity pixels: edge band | plate | food blob (=3).
            FillRect(px, s, 2, 6, 20, 12, CounterWood);
            DrawOutlineRect(px, s, 2, 6, 20, 12, Outline);
            FillRect(px, s, 2, 6, 20, 1, CounterEdge);
            FillCircleOutlined(px, s, 12, 12, 5, PlateColor, Outline);
            FillCircle(px, s, 12, 12, 3, PlateFood);
            FillRect(px, s, 11, 11, 2, 1, BreadCrust);
        }

        private static void BuildFloor(Color32[] px, int s)
        {
            // 4-tile checker so the 24-px sprite Tiled-mode renders as
            // a low-contrast kitchen tile pattern.
            for (var y = 0; y < s; y++)
            {
                for (var x = 0; x < s; x++)
                {
                    var checker = ((x / 6) + (y / 6)) % 2 == 0;
                    px[y * s + x] = checker ? FloorBase : FloorTone;
                }
            }
            // Tile seams every 6 px.
            for (var i = 0; i < s; i++)
            {
                if (i % 6 == 0)
                {
                    for (var j = 0; j < s; j++)
                    {
                        px[i * s + j] = FloorSeam;
                        px[j * s + i] = FloorSeam;
                    }
                }
            }
        }

        // --- Ingredient icons (Day 13-B) -----------------------------------
        // 24×24 canvas, ingredient sprite drawn centered in the inner
        // 16×16. The remaining 4-px margin lets the icons read clearly
        // when stacked in a recipe-hint row OR floated next to the chef
        // as a held-item indicator.

        private static readonly Color32 IngBreadCrust = new(217, 161, 96, 255);  // baked tan
        private static readonly Color32 IngBreadInner = new(245, 220, 170, 255); // soft inside
        private static readonly Color32 IngPattyMid   = new(150, 90, 60, 255);   // beef brown
        private static readonly Color32 IngPattyDark  = new(110, 60, 40, 255);   // grill mark
        private static readonly Color32 IngCheeseMid  = new(245, 200, 80, 255);
        private static readonly Color32 IngCheeseDark = new(210, 160, 50, 255);
        private static readonly Color32 IngTomatoRed  = new(220, 70, 60, 255);
        private static readonly Color32 IngTomatoStem = new(95, 145, 70, 255);
        private static readonly Color32 IngEggWhite   = new(252, 248, 235, 255);
        private static readonly Color32 IngEggYolk    = new(245, 195, 70, 255);

        private static void BuildIngBread(Color32[] px, int s)
        {
            // Sliced loaf — wide tan rectangle with rounded corners + crust band.
            FillRect(px, s, 4, 7, 16, 9, IngBreadCrust);
            DrawOutlineRect(px, s, 4, 7, 16, 9, Outline);
            FillRect(px, s, 5, 8, 14, 6, IngBreadInner);
            // soft top crust shadow
            FillRect(px, s, 5, 13, 14, 1, IngBreadCrust);
            // round corners by knocking out corners
            SetSafe(px, s, 4, 7,  new Color32(0,0,0,0));
            SetSafe(px, s, 19, 7, new Color32(0,0,0,0));
            SetSafe(px, s, 4, 15, new Color32(0,0,0,0));
            SetSafe(px, s, 19, 15, new Color32(0,0,0,0));
        }

        private static void BuildIngPatty(Color32[] px, int s)
        {
            // Thick beef patty — flattened oval with a lighter top stripe and a grill-mark dot.
            FillCircleOutlined(px, s, 12, 12, 7, IngPattyMid, Outline);
            // squash to oval — clear bottom row, top row
            for (var x = 0; x < s; x++)
            {
                SetSafe(px, s, x, 5, new Color32(0,0,0,0));
                SetSafe(px, s, x, 19, new Color32(0,0,0,0));
            }
            // grill marks
            FillRect(px, s, 9, 13, 1, 1, IngPattyDark);
            FillRect(px, s, 14, 11, 1, 1, IngPattyDark);
            FillRect(px, s, 11, 9, 1, 1, IngPattyDark);
        }

        private static void BuildIngCheese(Color32[] px, int s)
        {
            // Yellow slice — slight angle suggests a slice on a board.
            FillRect(px, s, 4, 8, 16, 8, IngCheeseMid);
            DrawOutlineRect(px, s, 4, 8, 16, 8, Outline);
            // bottom shadow band
            FillRect(px, s, 4, 8, 16, 1, IngCheeseDark);
            // 3 small holes
            SetSafe(px, s, 8, 12, IngCheeseDark);
            SetSafe(px, s, 13, 13, IngCheeseDark);
            SetSafe(px, s, 16, 11, IngCheeseDark);
        }

        private static void BuildIngLettuce(Color32[] px, int s)
        {
            // Wavy green leaf — irregular silhouette.
            for (var y = 7; y <= 16; y++)
            {
                for (var x = 5; x <= 18; x++)
                {
                    px[y * s + x] = P_Green;
                }
            }
            // wavy edge bites
            SetSafe(px, s, 5, 7,  new Color32(0,0,0,0));
            SetSafe(px, s, 5, 16, new Color32(0,0,0,0));
            SetSafe(px, s, 18, 7,  new Color32(0,0,0,0));
            SetSafe(px, s, 18, 16, new Color32(0,0,0,0));
            SetSafe(px, s, 8, 7, new Color32(0,0,0,0));
            SetSafe(px, s, 14, 7, new Color32(0,0,0,0));
            SetSafe(px, s, 8, 16, new Color32(0,0,0,0));
            SetSafe(px, s, 14, 16, new Color32(0,0,0,0));
            DrawOutlineRect(px, s, 5, 7, 14, 10, Outline);
            // central vein
            FillRect(px, s, 11, 9, 1, 6, IngTomatoStem);
        }

        private static void BuildIngTomato(Color32[] px, int s)
        {
            // Round red tomato + tiny green stem cap.
            FillCircleOutlined(px, s, 12, 11, 7, IngTomatoRed, Outline);
            // highlight
            SetSafe(px, s, 9, 14, new Color32(255, 140, 130, 255));
            SetSafe(px, s, 10, 15, new Color32(255, 140, 130, 255));
            // green stem cap
            FillRect(px, s, 10, 17, 4, 1, IngTomatoStem);
            FillRect(px, s, 11, 18, 2, 1, IngTomatoStem);
        }

        private static void BuildIngEgg(Color32[] px, int s)
        {
            // Whole egg — vertical oval white shell.
            FillCircleOutlined(px, s, 12, 12, 6, IngEggWhite, Outline);
            // squeeze into vertical oval (clear sides)
            for (var y = 0; y < s; y++)
            {
                SetSafe(px, s, 5, y, new Color32(0,0,0,0));
                SetSafe(px, s, 18, y, new Color32(0,0,0,0));
            }
            // tiny yolk hint
            SetSafe(px, s, 12, 13, IngEggYolk);
            SetSafe(px, s, 11, 13, IngEggYolk);
        }

        // --- Painting primitives -------------------------------------------

        private static void WriteSprite(string name, System.Action<Color32[], int> painter)
        {
            var s = CanvasSize;
            var px = new Color32[s * s];
            for (var i = 0; i < px.Length; i++) px[i] = new Color32(0, 0, 0, 0);

            painter(px, s);

            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            tex.SetPixels32(px);
            tex.Apply();
            var path = $"{GeneratedDir}/{name}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            ConfigureSprite(path);
        }

        private static void ConfigureSprite(string path)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }

        private static void FillRect(Color32[] px, int s, int x0, int y0, int w, int h, Color32 c)
        {
            for (var y = y0; y < y0 + h; y++)
            {
                for (var x = x0; x < x0 + w; x++)
                {
                    if (x < 0 || x >= s || y < 0 || y >= s) continue;
                    px[y * s + x] = c;
                }
            }
        }

        private static void FillRoundedRect(Color32[] px, int s, int x0, int y0, int w, int h, Color32 fill, Color32 outline)
        {
            FillRect(px, s, x0, y0, w, h, fill);
            // Knock out single-pixel corners for a softer silhouette.
            SetSafe(px, s, x0,         y0,         new Color32(0, 0, 0, 0));
            SetSafe(px, s, x0 + w - 1, y0,         new Color32(0, 0, 0, 0));
            SetSafe(px, s, x0,         y0 + h - 1, new Color32(0, 0, 0, 0));
            SetSafe(px, s, x0 + w - 1, y0 + h - 1, new Color32(0, 0, 0, 0));
            for (var x = x0; x < x0 + w; x++)
            {
                if (x == x0 || x == x0 + w - 1) continue;
                SetSafe(px, s, x, y0, outline);
                SetSafe(px, s, x, y0 + h - 1, outline);
            }
            for (var y = y0; y < y0 + h; y++)
            {
                if (y == y0 || y == y0 + h - 1) continue;
                SetSafe(px, s, x0, y, outline);
                SetSafe(px, s, x0 + w - 1, y, outline);
            }
        }

        private static void DrawOutlineRect(Color32[] px, int s, int x0, int y0, int w, int h, Color32 c)
        {
            for (var x = x0; x < x0 + w; x++)
            {
                SetSafe(px, s, x, y0, c);
                SetSafe(px, s, x, y0 + h - 1, c);
            }
            for (var y = y0; y < y0 + h; y++)
            {
                SetSafe(px, s, x0, y, c);
                SetSafe(px, s, x0 + w - 1, y, c);
            }
        }

        private static void FillCircle(Color32[] px, int s, int cx, int cy, int r, Color32 c)
        {
            var r2 = r * r;
            for (var y = cy - r; y <= cy + r; y++)
            {
                for (var x = cx - r; x <= cx + r; x++)
                {
                    var dx = x - cx;
                    var dy = y - cy;
                    if (dx * dx + dy * dy > r2) continue;
                    SetSafe(px, s, x, y, c);
                }
            }
        }

        private static void FillCircleOutlined(Color32[] px, int s, int cx, int cy, int r, Color32 fill, Color32 outline)
        {
            FillCircle(px, s, cx, cy, r, fill);
            // Trace outline by drawing pixels just outside r-1 but inside r.
            var rOuter = r * r;
            var rInner = (r - 1) * (r - 1);
            for (var y = cy - r; y <= cy + r; y++)
            {
                for (var x = cx - r; x <= cx + r; x++)
                {
                    var dx = x - cx;
                    var dy = y - cy;
                    var d2 = dx * dx + dy * dy;
                    if (d2 <= rOuter && d2 > rInner) SetSafe(px, s, x, y, outline);
                }
            }
        }

        private static void SetSafe(Color32[] px, int s, int x, int y, Color32 c)
        {
            if (x < 0 || x >= s || y < 0 || y >= s) return;
            px[y * s + x] = c;
        }
    }
}
#endif
