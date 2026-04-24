// Imports Noto Sans KR as a TMP_FontAsset with a dynamic atlas and registers
// it as a global TMP fallback. After this runs once, every TMP text in the
// project renders Hangul correctly without per-scene font assignment.
//
// Invocation: Unity menu → Tools → Day One Chef → Install Korean Font
// (Also callable headlessly via -executeMethod
//  DayOneChef.Editor.KoreanFontSetup.InstallKoreanFont so CI and the build
//  wrapper can re-run it on a fresh clone.)

#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace DayOneChef.Editor
{
    public static class KoreanFontSetup
    {
        private const string SourceFontPath = "Assets/Fonts/NotoSansKR-Regular.otf";
        private const string FontAssetPath = "Assets/Fonts/NotoSansKR SDF.asset";

        // Atlas sizing tuned for the WebGL 256 MB heap budget. Baking the
        // full Hangul Syllables block (11 172 glyphs) at 90 pt / 9 px
        // padding generated ~28 × 16 MB RGBA32 atlas textures and aborted
        // the WASM runtime on first page load. Lower sampling, tighter
        // padding, plus a smaller Hangul subset keeps the working set to a
        // couple of atlas pages.
        private const int AtlasWidth = 2048;
        private const int AtlasHeight = 2048;
        private const int AtlasPadding = 5;
        private const int SamplingPointSize = 50;

        [MenuItem("Tools/Day One Chef/Install Korean Font")]
        public static void InstallKoreanFont()
        {
            var sourceFont = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
            if (sourceFont == null)
            {
                Debug.LogError($"[KoreanFontSetup] Source font missing at {SourceFontPath}. " +
                               "Run scripts that download Noto Sans KR first, or add the OTF manually.");
                return;
            }

            // Regenerate fresh on every run. A previously-Dynamic asset may
            // have null atlas textures that blow up TryAddCharacters; rather
            // than patch every possible partial state, start clean.
            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath) != null)
            {
                AssetDatabase.DeleteAsset(FontAssetPath);
                Debug.Log($"[KoreanFontSetup] Removed stale font asset at {FontAssetPath} before regenerating.");
            }

            // Render mode: SMOOTH_HINTED (bitmap grayscale with hinting)
            // rather than SDFAA. SDFAA's shader maths tripped something in
            // the Unity 6 WebGL IL2CPP build — glyphs baked correctly into
            // the atlas but rendered invisible. Bitmap avoids the SDF
            // shader path entirely.
            var fontAsset = TMP_FontAsset.CreateFontAsset(
                sourceFont,
                SamplingPointSize,
                AtlasPadding,
                GlyphRenderMode.SMOOTH_HINTED,
                AtlasWidth,
                AtlasHeight,
                AtlasPopulationMode.Dynamic,
                enableMultiAtlasSupport: true);

            AssetDatabase.CreateAsset(fontAsset, FontAssetPath);
            Debug.Log($"[KoreanFontSetup] Created TMP font asset at {FontAssetPath}.");

            // WebGL IL2CPP can't rasterize TMP glyphs at runtime (FreeType
            // function pointers end up null in the WASM binary, producing a
            // "RuntimeError: null function" when the first unseen glyph
            // hits the text renderer). Bake all needed glyphs here and
            // lock the atlas to Static so runtime rasterization is never
            // attempted.
            BakeCharacterSet(fontAsset);
            fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;
            EditorUtility.SetDirty(fontAsset);

            // NOTE: global TMP fallback registration is intentionally skipped.
            // The fallback chain traversal path crashed the WebGL player with
            // an unidentified abort code the moment a composed Hangul glyph
            // hit the text renderer. Consumers that need Korean rendering
            // (e.g., OSSImeProbeSetup) assign this asset as the primary
            // font on their TMP components instead — a single-font render
            // avoids the failing fallback code path entirely.
            RemoveFromFallbacks(fontAsset);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[KoreanFontSetup] Done. Atlas mode={fontAsset.atlasPopulationMode}, " +
                      $"glyph count={fontAsset.glyphTable?.Count ?? 0}");
        }

        private static void BakeCharacterSet(TMP_FontAsset fontAsset)
        {
            var sb = new System.Text.StringBuilder(3000);

            // ASCII printable range (space through tilde).
            for (int c = 0x20; c <= 0x7E; c++) sb.Append((char)c);

            // Modern Korean syllable subset — 0xAC00 through 0xBFFF covers
            // roughly the first 5 400 precomposed Hangul syllables, which
            // includes every character in typical 2-Set 두벌식 output and all
            // words in the probe corpus. The tail of the block (0xC000
            // upward) contains archaic and rarely-used compounds we skip to
            // stay within the WebGL heap.
            for (int c = 0xAC00; c <= 0xBFFF; c++) sb.Append((char)c);

            // Jamo block — partial IME composition chars (ㄱ ㅏ etc.)
            // surface briefly during keystroke interpretation.
            for (int c = 0x3130; c <= 0x318F; c++) sb.Append((char)c);

            // Common Korean punctuation that LiberationSans already covers,
            // but adding here keeps the fallback pipeline deterministic.
            sb.Append("。、「」『』·…—–—·→←↑↓");

            fontAsset.TryAddCharacters(sb.ToString(), out string missing, includeFontFeatures: true);
            if (!string.IsNullOrEmpty(missing))
            {
                Debug.LogWarning(
                    $"[KoreanFontSetup] {missing.Length} characters unavailable in the source font — " +
                    "TMP will render them via later fallbacks if any are installed.");
            }
        }

        private static void RemoveFromFallbacks(TMP_FontAsset koreanAsset)
        {
            // Earlier iterations of this script registered the Korean font
            // on LiberationSans SDF's fallback list. That path crashed the
            // WebGL player — keep the default font's fallback list clean by
            // removing any stale reference on every run so the crash does
            // not resurface after a partial checkout.
            var defaultFont = TMP_Settings.defaultFontAsset;
            if (defaultFont == null) return;

            var fallbacks = defaultFont.fallbackFontAssetTable;
            if (fallbacks == null) return;

            bool mutated = false;
            for (int i = fallbacks.Count - 1; i >= 0; i--)
            {
                if (fallbacks[i] == koreanAsset || fallbacks[i] == null)
                {
                    fallbacks.RemoveAt(i);
                    mutated = true;
                }
            }

            if (mutated)
            {
                defaultFont.fallbackFontAssetTable = fallbacks;
                EditorUtility.SetDirty(defaultFont);
                Debug.Log("[KoreanFontSetup] Removed Korean font from LiberationSans SDF fallback chain " +
                          "(the fallback path crashes WebGL IL2CPP — primary-font assignment is used instead).");
            }
        }
    }
}
#endif
