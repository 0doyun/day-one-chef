# Known Issue: Korean Font Rendering in Unity 6.3 LTS WebGL IL2CPP

**Status**: Open — deferred to Day 3 UI work
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

## References

- ADR-0001 §Rollback plan (option to swap to Alternative 3 if rendering cannot be resolved)
- docs/phase-1-ime-evaluation-protocol.md §Step 3 (desktop matrix results)
- kou-yeung/WebGLInput — confirmed working at the IME layer regardless of font rendering outcome
