# Known Issue: Korean Font Rendering in Unity 6.3 LTS WebGL IL2CPP

**Status**: Resolved in Editor (macOS, Unity 6.3 LTS) on 2026-04-24 during
Day 3 prototype scene work — see §Resolution below. Not yet re-verified in
a WebGL IL2CPP build on the expanded range; the WebGL heap headroom at
512 MB gives confidence that the multi-atlas fix will carry through, but
a fresh Chrome/Safari build test is tracked as part of Day 7 WebGL
optimization.
**First seen**: 2026-04-24, during ADR-0001 Phase 1 OSS IME probe
**Impact**: Visual only — Korean IME pipeline (kou-yeung/WebGLInput → Unity C#) is functionally correct; typed text round-trips via clipboard exactly. Only the rendered glyph output is wrong.

## Symptom

After wiring Noto Sans KR SDF as the Hangul glyph source for `TMP_InputField`, typed Korean renders as either:

- `LiberationSans` tofu boxes (default state — no Korean font in play)
- Invisible pixels (after swapping `font`, `fontSharedMaterial`, or both)
- Fatal WASM `abort()` at keystroke (earlier configurations — see attempts below)

## Proofs the IME path itself works

- **Copy-paste verification**: Select-all + copy inside the probe input field, paste outside Unity. The pasted string is the Korean input with zero dropped or duplicated jamo — verified in Chrome on macOS with 두벌식 2-Set IME.
- **kou-yeung/WebGLInput component**: Attached to the TMP_InputField, forwards composition-corrected values to Unity via JS interop.
- **ADR-0001 §Validation Criteria row 1**: passes on copy-paste evidence.

The rendering issue is therefore *orthogonal* to Phase 1's acceptance. Phase 1 reports PASS on the IME criterion; font rendering is tracked separately here.

## Attempts that did not fix it

### A. `TMP_Settings.fallbackFontAssetTable` registration (Dynamic atlas)
Added Noto Sans KR to the global TMP fallback chain. First Hangul keystroke crashed with `RuntimeError: null function` — FreeType function pointers are not wired in the Unity 6.3 WebGL IL2CPP link step, so the runtime glyph rasterizer the fallback chain invokes does not exist.

### B. Fallback registration with Static atlas (atlas pre-baked, fallback still active)
Pre-baked 11 172 Hangul + ASCII + Jamo into the atlas, flipped `atlasPopulationMode` to Static, kept fallback registration. Runtime aborted with `RuntimeError: 67133512` then `20189112` on the first Korean keystroke — the fallback traversal code path still reaches something that no longer exists in the WASM image, independent of whether atlas rasterization is attempted.

### C. Primary-font assignment, no fallback (SDFAA render mode)
Assigned `textLabel.font = koreanAsset` and `textLabel.fontSharedMaterial = koreanAsset.material` on the InputField's `textComponent`. No crash. Text is invisible — glyphs lay out (caret moves, copy-paste works, character count advances) but no pixels reach the screen. Explicit `color = Color.black; alpha = 1` did not help.

### D. Primary-font assignment, SMOOTH_HINTED (bitmap) render mode
Regenerated the font asset with `GlyphRenderMode.SMOOTH_HINTED` to bypass SDFAA shader math. Same invisible outcome as C.

## Hypotheses for Day 3 investigation

1. **Atlas texture not serialized**: `TMP_FontAsset.CreateFontAsset` may create atlas textures as in-memory runtime objects that do not get written into the asset file when `AssetDatabase.CreateAsset` saves the main asset. Inspecting the `.asset` file with Unity's internal debug YAML dump would confirm.
2. **Material shader mismatch**: The material generated alongside the TMP font asset may use a shader variant that loses its SDF properties when IL2CPP strips unused shader keywords for WebGL. Setting `_WeightNormal`, `_ScaleRatioA`, `_GradientScale`, `_FaceColor` explicitly before save is untried.
3. **CanvasRenderer clip region**: The InputField's text render may be clipped by a `RectMask2D` whose visible rect shrinks when the font metrics change (line height, ascender/descender differ between LiberationSans and Noto Sans KR). Worth inspecting in Unity's Frame Debugger.
4. **Prefab generation vs headless setup**: `TMP_DefaultControls.CreateInputField` (used by Unity's menu item) sets up material references differently than manually instantiating + assigning. The build-pipeline `ApplyFontAndSave` step runs headlessly; any interactive-only side effect of the manual menu would be missing.

## Workarounds available for Day 3

- Use the Font Asset Creator window interactively to generate a known-good Noto Sans KR SDF asset and commit it alongside the build script (bypasses programmatic creation)
- Render Korean text via `TextMeshProUGUI` objects that are NOT inside `TMP_InputField` — e.g., the monologue stream or order display — which avoids whatever InputField-specific rendering quirk is at play
- Replace `TMP_InputField` with a plain `UnityEngine.UI.Text` overlay positioned above the HTML input; give up on TMP for the instruction field only
- Emit the composed text into a secondary TMP label below the input field (for user visual confirmation) rather than trying to render it in the input itself

## Resolution (Editor, 2026-04-24)

Day 3 prototype scene work revealed two additional failure modes that,
once fixed alongside the original invisible-glyph workaround, produce
correctly rendered Hangul labels in Unity 6.3 LTS Editor Play mode.

### Root causes

1. **Baked range too narrow.** The previous attempts capped the
   syllable range at `0xAC00–0xBFFF`, which misses everyday glyphs like
   `조 (U+C870)`, `장 (U+C7A5)`, `화 (U+D654)`, `카 (U+CE74)`,
   `운 (U+C6B4)`, `터 (U+D130)`. The cap was a hedge against the
   original WASM OOM at 90 pt SDFAA, no longer relevant at 50 pt
   SMOOTH_HINTED + 512 MB heap. Expanded to full Hangul Syllables
   block `0xAC00–0xD7A3` (11 172 glyphs).
2. **Multi-atlas pages not serialized.** TMP's `TryAddCharacters`
   silently creates extra `Texture2D` atlas pages when one page fills
   up, but does *not* register them with the AssetDatabase. Pages 2+
   get garbage-collected on the next domain reload, and any glyph
   living on them renders as tofu with Console warning
   *"The Font Atlas Texture … is missing."* The atlas-texture array
   has to be walked explicitly and each page attached as a sub-asset.

### Fix applied

In `game/Assets/Editor/KoreanFontSetup.cs`, after `BakeCharacterSet` and
before `SaveAssets`:

```csharp
foreach (var atlasTexture in fontAsset.atlasTextures)
{
    if (atlasTexture == null) continue;
    atlasTexture.hideFlags = HideFlags.HideInHierarchy;
    if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(atlasTexture)))
    {
        AssetDatabase.AddObjectToAsset(atlasTexture, fontAsset);
    }
}
// Also attach the root material the same way, then
// AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate).
```

In `game/Assets/Editor/MainKitchenSetup.cs`, `CreateStation` must:

- assign `tmp.font = koreanFont` **before** `tmp.text`, otherwise TMP's
  internal material cache binds to LiberationSans SDF at first layout
  and keeps using it even after reassignment;
- call `EditorUtility.SetDirty(tmp)` so the scene serialiser picks up
  the reassigned font reference instead of silently discarding it.

Setup now also calls `KoreanFontSetup.InstallKoreanFont()` up-front and
aborts if the resulting font asset can't be loaded — the menu path and
the headless build path now behave identically.

### Verification

- Editor Play mode: all 5 station labels (냉장고 / 도마 / 화구 / 조립대 /
  카운터) render cleanly, zero *"Font Atlas Texture … is missing"*
  warnings in Console, logged atlas page count matches the glyph
  distribution for SMOOTH_HINTED at 50 pt sampling.
- Not yet re-verified in a built WebGL player — tracked as a follow-up
  in Day 7 WebGL optimization.

## References

- ADR-0001 §Rollback plan (option to swap to Alternative 3 if rendering cannot be resolved) — no longer needed, retained for history
- docs/phase-1-ime-evaluation-protocol.md §Step 3 (desktop matrix results)
- kou-yeung/WebGLInput — confirmed working at the IME layer regardless of font rendering outcome
- Day 3 PR: https://github.com/0doyun/day-one-chef/pull/6 (fix commit `87344eb`)
