# ADR-0001: Korean IME Strategy for Unity WebGL

## Status

Proposed

## Date

2026-04-23

## Last Verified

2026-04-23

## Decision Makers

Solo developer (project owner).

## Summary

Unity WebGL's built-in text input (`TMP_InputField`, `InputField`) drops hangul characters mid-composition on every major browser — this is acknowledged in Unity's own manual ("Input via IME is currently not supported on the Web platform") and tracked in the Unity Issue Tracker. The project cannot rely on it for the single-line Korean instruction field that drives the entire core loop. Solution strategy is **OSS-first, DIY-fallback**: on Day 1 evaluate mature open-source libraries (primary candidate: `kou-yeung/WebGLInput` — selected over `unity3d-jp/WebGLNativeInputField` after hands-on inspection because it ships with `TMP_InputField` support, installs as a UPM Git package, explicitly targets Unity 2023.2+ (compatible with 6.3 LTS), and was last updated 2025-12; the Unity Japan library is UGUI-only and 2+ years stale) against the Korean 2-Set composition validation matrix. If an OSS library passes validation, adopt it and spend the freed time on the custom bridge story. If none pass, fall back to the DIY approach specified in this ADR: an HTML `<input>` overlay + `.jslib` bridge routing browser IME composition events into Unity. Either path yields the same Unity-side contract (`IKoreanImeBridge`), so downstream work is unaffected.

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS |
| **Domain** | Input (+ WebGL platform) |
| **Knowledge Risk** | HIGH — Unity 6.3 LTS is post-LLM-cutoff; `TMP_InputField` behavior on WebGL remains a long-standing issue without an in-engine fix as of the pinned version |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md`, `docs/engine-reference/unity/current-best-practices.md`, Unity Manual — WebGL: Interacting with browser scripting (`.jslib` plugins), Unity WebGL text input known limitations |
| **Post-Cutoff APIs Used** | None — the approach uses browser DOM APIs (`compositionstart`/`compositionupdate`/`compositionend`/`input`) and the stable Unity `[DllImport("__Internal")]` / `SendMessage` interop path, all unchanged across Unity 6.x |
| **Verification Required** | Desktop Chrome, Safari, Firefox with 2-Set Korean IME (두벌식) composing multi-jamo syllables; same on iOS WKWebView inside `webview_flutter`; focus-loss recovery when the user clicks outside the overlay |

> **Note**: Knowledge Risk is HIGH. If Unity ships a native WebGL IME fix in a future LTS (improbable in 6.x lifetime), re-validate this ADR and consider superseding.

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | None |
| **Enables** | ADR-0002 (Bridge message protocol) — text-submission event reuses the Unity ↔ JS channel defined by this ADR |
| **Blocks** | Epic: *Core Gameplay Loop* — player instruction input is the entry point to every round, so no downstream gameplay work can ship until this ADR is Accepted and prototyped |
| **Ordering Note** | Prototype and validate on Day 1–2 of the timeline in `game-concept.md` §7. Escalate to "Superseded" if the Day 2 prototype cannot achieve hangul composition without loss — fallback plan in Alternative 2 below |

## Context

### Problem Statement

The core loop requires the player to type a single line of Korean (≤ 80 chars) per round. Korean input depends on the OS/browser IME performing jamo → syllable composition. Unity WebGL's text widgets do not expose the browser's composition buffer — they read the final keydown events only — so syllables being composed arrive as partial or missing hangul. The game is unplayable without correct Korean input, and this is the single biggest technical risk in the 14-day timeline.

### Current State

No text input is implemented. `TMP_InputField` in Unity WebGL has been confirmed broken for East Asian IME composition across Unity 2019 LTS through 6.x; this has not changed in 6.3 LTS. Unity's workaround documentation points developers at JavaScript interop rather than a first-party fix.

### Constraints

- **Unity limitation**: WebGL build does not propagate browser IME composition events to C# input widgets. Confirmed behavior, not a bug that will be fixed mid-timeline.
- **Two delivery targets**: Vercel web build (desktop browser, no Flutter) and Flutter iOS build (WKWebView wrapping the same Unity WebGL build). The IME approach must work in both without two parallel implementations.
- **Mobile IME behavior**: iOS soft keyboards only appear when a *real* DOM input element receives focus inside WKWebView. Unity's internal input widgets never produce that focus, so mobile users today cannot type Korean at all.
- **Timeline**: The spec allots Days 1–2 for this problem. Any approach that extends beyond that eats into core-loop days and jeopardizes the 14-day delivery.
- **Quality bar**: Characters lost mid-composition are immediately visible to any Korean reader and undermine the demo. Must be zero-loss for common 두벌식 input.

### Requirements

- **R1** Accept all 2-Set Korean (두벌식) compositions without dropping jamo.
- **R2** Work in Chrome, Safari, and Firefox on desktop, and in iOS WKWebView (via `webview_flutter`).
- **R3** Single implementation — one code path that serves both web and iOS builds.
- **R4** Integrate with the existing Unity ↔ JS bridge so text submission is a bridge event, not a side-channel.
- **R5** Submit-on-Enter and submit-on-blur both deliver the full composed string to Unity.
- **R6** p95 composition-to-Unity latency ≤ 50 ms per character (perceptible lag ruins feel).
- **R7** Focus management: clicking the Unity canvas away from the input area blurs and dismisses the overlay; clicking the instruction box re-focuses it.

## Decision

Adopt a **two-phase decision**:

**Phase 1 — Day 1 OSS evaluation (time-boxed to 2 hours).**
Integrate `kou-yeung/WebGLInput` (primary candidate — see Summary) into a throwaway scene with a `TMP_InputField` wrapped by its `WebGLInput` component and run the Korean 2-Set validation matrix in Validation Criteria. If it passes the full matrix — desktop Chrome/Safari/Firefox and iOS Flutter WKWebView — adopt it as-is and stop. If it fails, the Alternatives Considered §Alternative 5 list names fallback OSS candidates to try before escalating to Phase 2.

**Phase 2 — Fallback DIY (only if Phase 1 fails).**
Build the HTML overlay directly: render a transparent-bordered HTML `<input type="text">` element as an absolutely-positioned overlay pinned to the instruction box region of the Unity canvas. The overlay owns Korean IME composition natively through the browser. On `compositionend` (for IME finalization) and on `input` (for direct ASCII and backspace), the overlay's JavaScript pushes the current value into Unity via `SendMessage` on a dedicated C# GameObject. Unity treats the input box as a *view only* of the overlay's state — it never authors characters itself.

Both phases expose the **same Unity-side contract** (`KoreanImeBridge.OnTextChanged`, `OnSubmit`). This is the key architectural invariant — everything downstream (GameManager, Gemini call #1, monologue stream) stays identical regardless of which phase wins.

### Architecture

```
 Browser / WebView
 ┌─────────────────────────────────────────────────────────────┐
 │                                                             │
 │   ┌─────────────────────────────┐                           │
 │   │   Unity WebGL <canvas>      │                           │
 │   │                             │                           │
 │   │   ┌───────────────────────┐ │  z-index: 1                │
 │   │   │ InstructionBox (UI)   │ │  (Unity renders outline) │
 │   │   └───────────────────────┘ │                           │
 │   │   position: absolute        │                           │
 │   │   overlaid HTML input ──────┼──►  ┌──────────────────┐  │
 │   │   z-index: 10               │     │ HTML <input>     │  │
 │   └─────────────────────────────┘     │ (invisible bg,   │  │
 │                                       │  inherits font)  │  │
 │                                       └────────┬─────────┘  │
 │                                                │            │
 │   Korean IME (OS-level, browser-routed)        │            │
 │   2-Set keystrokes → compositionstart/update   │            │
 │   → compositionend → "안녕" (final)             │            │
 │                                                │            │
 │   KoreanImeOverlay.jslib ◄─────────────────────┘            │
 │         │                                                   │
 │         │ SendMessage("KoreanImeBridge",                     │
 │         │             "OnTextChanged" | "OnSubmit",          │
 │         │             jsonPayload)                           │
 │         ▼                                                   │
 │   Unity C#: KoreanImeBridge.cs                              │
 │         │                                                   │
 │         ▼                                                   │
 │   GameManager.OnInstructionSubmitted(text)                  │
 │         │                                                   │
 │         ▼                                                   │
 │   Gemini call #1                                            │
 └─────────────────────────────────────────────────────────────┘
```

### Key Interfaces

**`KoreanImeOverlay.jslib`** (JavaScript → Unity):

```javascript
mergeInto(LibraryManager.library, {
  // Called from C#. Creates (or re-shows) the overlay input
  // positioned at (x,y) with given size, in CSS pixels relative
  // to the Unity canvas origin.
  KoreanIme_Show: function (xPtr, y, w, h, placeholderPtr) {
    // lazy-create <input id="korean-ime-overlay">
    // apply absolute position, size, transparent background
    // attach compositionend + input + keydown(Enter) + blur listeners
    // forward events via SendMessage("KoreanImeBridge", ...)
    // focus()
  },

  KoreanIme_Hide: function () {
    // blur + display: none
  },

  KoreanIme_SetValue: function (textPtr) {
    // set input.value for Unity-initiated resets (e.g., clear on submit)
  },
});
```

**`KoreanImeBridge.cs`** (Unity side, receives `SendMessage` calls):

```csharp
using System.Runtime.InteropServices;
using UnityEngine;

public sealed class KoreanImeBridge : MonoBehaviour
{
    [DllImport("__Internal")] private static extern void KoreanIme_Show(
        string x, float y, float w, float h, string placeholder);
    [DllImport("__Internal")] private static extern void KoreanIme_Hide();
    [DllImport("__Internal")] private static extern void KoreanIme_SetValue(string text);

    public event System.Action<string> TextChanged;
    public event System.Action<string> Submitted;

    public void ShowOverlay(Rect canvasRect, string placeholder)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        KoreanIme_Show(canvasRect.x.ToString("F0"),
                       canvasRect.y, canvasRect.width, canvasRect.height,
                       placeholder);
#endif
    }

    public void HideOverlay()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        KoreanIme_Hide();
#endif
    }

    // Called by SendMessage from .jslib
    public void OnTextChanged(string text) => TextChanged?.Invoke(text);
    public void OnSubmit(string text)      => Submitted?.Invoke(text);
}
```

**Unity → Flutter passthrough**: This ADR defines the IME pipeline only. The existing Unity ↔ JS ↔ Flutter bridge (to be specified in ADR-0002) carries submitted instructions onward to Riverpod logging if needed. The IME overlay does **not** talk to Flutter directly.

### Implementation Guidelines

1. Place the overlay `<input>` inside the same DOM container as the Unity `<canvas>`, after it, so stacking context is predictable. Never move it outside that container — positioning relies on the canvas's offset parent.
2. On canvas resize (browser zoom, WebView reflow), re-compute and re-apply the overlay's position/size from the canvas's `getBoundingClientRect()`. Listen to `resize` and `DeviceOrientation` on mobile.
3. Use `font-family`, `font-size`, and `color` values on the overlay that match the Unity UI's `TMP_Text` style so the rendered text lines up visually. Background should be fully transparent; Unity draws the bordered box underneath.
4. Cap `maxlength="80"` on the overlay to enforce `INPUT_CHAR_CAP` at the DOM layer — no runtime validation needed in C#.
5. On `keydown` for `Enter` (composition inactive), call `OnSubmit` and immediately hide the overlay. On `compositionend`, push the resolved value via `OnTextChanged` but do NOT submit (user is still composing).
6. Track `isComposing` via `compositionstart`/`compositionend`; never submit while composing even if the user mashes Enter (common mid-hangul).
7. Editor fallback: in `UNITY_EDITOR`, skip the overlay entirely and use Unity's `TMP_InputField` for developer testing. Production Korean validation happens only in WebGL builds.
8. Do not use `pointer-events: none` on the overlay — it must receive clicks/taps to capture focus. Use Unity UI raycast filtering to avoid stealing clicks from other UI during overlay visibility.

## Alternatives Considered

### Alternative 1: Native Unity `TMP_InputField` with IME enabled

- **Description**: Use TextMesh Pro's `TMP_InputField` component directly, relying on its input handling for Korean composition.
- **Pros**: No JS interop, no overlay, single engine language.
- **Cons**: Empirically drops hangul mid-composition on WebGL builds. Mobile keyboard does not appear in WebView because the field is not a real DOM input.
- **Estimated Effort**: Lowest — until the test fails. Then the work is thrown away.
- **Rejection Reason**: Hard blocker — R1 (zero-loss composition) and R2 (mobile keyboard) both fail in under 5 minutes of manual testing. This is the reason the ADR exists.

### Alternative 2: Flutter `TextField` layered over the WebView, piped via JSChannel

- **Description**: Hide Unity's input region entirely. Render a Flutter `TextField` in the Riverpod-managed shell, positioned over the WebView at the instruction box location. On submit, forward the string through `JavaScriptChannel` into a JS glue function, which calls `SendMessage` to hand it to Unity.
- **Pros**: Flutter's `TextField` handles Korean IME perfectly. Native Cupertino look on iOS.
- **Cons**: The Vercel web build has **no Flutter shell** — this path dead-ends on desktop browsers, which are the primary delivery target. Solving that by using Alternative 2 only on iOS and Alternative 1 on web re-introduces the bug the ADR is designed to eliminate. Also introduces a geometry-sync problem: Flutter must mirror the Unity canvas's scroll/zoom/resize so its `TextField` stays aligned.
- **Estimated Effort**: High — requires per-platform code paths, plus continuous coordinate sync.
- **Rejection Reason**: Fails R3 (single implementation) and degrades the web build to Alternative 1's broken state. Held in reserve as Day-2 plan B if the chosen decision's prototype fails.

### Alternative 3: Hybrid per-platform (overlay on web, Flutter `TextField` on iOS)

- **Description**: Use HTML overlay for the desktop Vercel build; use a Flutter `TextField` layered above the WebView for the iOS build. Dispatch based on build target.
- **Pros**: Both targets get their best-fit native solution.
- **Cons**: Two code paths, two test matrices, two bug surfaces. Divergent input behavior between platforms. Weakens the "single bridge pipeline" architectural story the project depends on.
- **Estimated Effort**: Medium-high.
- **Rejection Reason**: R3 (single implementation) is violated. Complexity cost outweighs the marginal UX gain on iOS, especially because the chosen decision already uses real DOM inputs in WebView — mobile IME behavior is identical.

### Alternative 4: Roll a custom in-Unity IME state machine

- **Description**: Implement 2-Set hangul composition rules directly in C# (jamo → syllable state machine), reading raw keycodes from Unity's input system.
- **Pros**: Fully engine-native, no JS interop.
- **Cons**: Reimplements something the OS and every browser already provide for free. Does not handle 3-Set, vowel-harmony dictionaries, or user-customized IME. Ignores external IME events entirely.
- **Estimated Effort**: Very high (weeks, not days) and never reaches parity with OS IME.
- **Rejection Reason**: Hard incompatible with the 14-day timeline. Also wrong tool for the job.

### Alternative 5: Blindly adopt a single OSS library without fallback

- **Description**: Pick one of the mature OSS Unity WebGL IME libraries and commit to it without a DIY fallback path. Candidates surveyed (ordered by fit for this project):
  - [`kou-yeung/WebGLInput`](https://github.com/kou-yeung/WebGLInput) — **chosen primary for Phase 1**. TMP support (`WrappedTMPInputField`), UPM Git package (`https://github.com/kou-yeung/WebGLInput.git?path=Assets/WebGLSupport`), Unity 2023.2+ target (compatible with Unity 6.3 LTS), 200+ merged PRs, last updated 2025-12, MIT. Experimental mobile + UI Toolkit support.
  - [`unity3d-jp/WebGLNativeInputField`](https://github.com/unity3d-jp/WebGLNativeInputField) — Unity Japan official team. Two modes (popup via `window.prompt` or overlay HTML). Demoted from primary after hands-on inspection: no TMP support (UGUI `InputField` only), no UPM packaging, last commit 2024-03. Still a credible fallback if Phase 1 primary breaks on an edge case.
  - [`decentraland/webgl-ime-input`](https://github.com/decentraland/webgl-ime-input) — production-validated (Decentraland, a live commercial metaverse).
  - `WebGLSupport` — general-purpose WebGL helper package that includes IME handling.
  - [`rehanlabs/Unity-WebGL-HTML-InputFix`](https://github.com/rehanlabs/Unity-WebGL-HTML-InputFix) — community HTML input bridge.
- **Pros**: Fastest possible integration. No original IME code to write or maintain.
- **Cons**: Most of these libraries are primarily tested for Japanese IME. Korean 2-Set behavior is under-validated. None are documented against iOS WKWebView via `webview_flutter`. No guarantee any single choice passes the full validation matrix on Day 1. Committing without a fallback means a Day-2 blocker if the chosen library trips on Safari or WKWebView edge cases.
- **Estimated Effort**: 2–4 hours if the library works; days of unbounded firefighting if it silently breaks on one platform.
- **Rejection Reason**: The *chosen decision* actually absorbs this alternative as Phase 1 — but it **adds a DIY fallback** (Phase 2) so a library failure does not become a project blocker. Committing to a single library with no exit ramp is the rejected variant; the two-phase strategy is what was chosen.

## Consequences

### Positive

- Single code path works on Vercel web and iOS WKWebView with zero per-platform branching.
- Korean IME correctness is inherited from the browser and OS, which are already the best-in-class implementations.
- Mobile soft keyboards appear automatically because the overlay is a real DOM `<input>`.
- The `.jslib` bridge pattern established here is reusable for ADR-0002 (general Unity ↔ JS bridge), so this ADR pays forward into the broader bridge work.
- Focus management is explicit and testable (show/hide/value API).

### Negative

- Overlay position must be kept in sync with the Unity canvas on every resize/scroll — a non-trivial amount of coordinate math the programmer owns.
- Visual styling must be maintained in two places: Unity UI (the bordered box) and CSS (the overlay's font/color/padding). Drift produces visual glitches.
- `.jslib` files are not unit-testable in the usual C# test framework; the bridge round-trip has to be covered by manual or Playwright-style integration tests.
- Unity Editor cannot exercise the real overlay — developer testing of Korean input only happens in WebGL builds, slowing the iteration loop.

### Neutral

- The instruction box is a *composition* of Unity UI (visual) + HTML input (logic). Code review must check both sides when the instruction UX changes.
- Flutter shell is not involved in IME at all — it stays pure bridge and shell.

## Risks

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|-----------|
| Overlay position drifts after canvas resize (especially on mobile rotate) | Medium | Medium | Re-query `getBoundingClientRect()` on every `resize` and `orientationchange`; throttle to 60 Hz |
| `compositionend` fires but `input` value is stale on Safari | Medium | High | Read from `compositionend.data` directly when available; fall back to `input.value` after an `await microtask` |
| User taps Unity UI button while overlay has focus — click is swallowed by overlay | Medium | Medium | Set `pointer-events` on overlay to `none` outside its bbox; use a transparent background; hide overlay on `blur` |
| iOS WKWebView reports different composition event ordering than desktop Safari | Low | High | Include iOS WKWebView in Day-2 prototype acceptance gate; if it diverges, fall back to Alternative 2 for iOS only |
| Shift from single-line to multi-line input requirements later | Low | Low | Swap `<input>` → `<textarea>` with identical event handlers; schema unchanged |
| Unity updates `[DllImport("__Internal")]` contract in a future LTS | Very low | Medium | Pinned to Unity 6.3 LTS via `VERSION.md`; re-verify on any upgrade ADR |
| Phase 1 OSS library passes Japanese IME but drops Korean 2-Set jamo mid-composition (library primarily developed against Japanese input) | Medium | High | Include Korean 2-Set test string in Phase 1 validation matrix; fall into Phase 2 DIY on any dropped jamo |
| Phase 1 OSS library untested against Unity 6.3 LTS (most libraries developed against Unity 2019–2022 LTS) | Medium | Medium | Phase 1 evaluation itself exercises this — break reveals itself within 2 hours rather than leaking into Day 3+ |
| Phase 1 OSS library untested against iOS WKWebView via `webview_flutter` (libraries typically tested against desktop browsers only) | Medium | High | Include the iOS Simulator inside `webview_flutter` as a mandatory row in the Phase 1 matrix, not an afterthought |

## Performance Implications

| Metric | Before | Expected After | Budget |
|--------|--------|----------------|--------|
| CPU (frame time) | n/a (no input implementation) | +0.1ms (overlay event dispatch is one SendMessage per key) | 16.6ms total (60fps) |
| Memory | n/a | +~2KB JS heap for overlay element and handlers | 256MB WebGL build heap |
| Load Time | n/a | Negligible — overlay is created lazily on first focus | ≤ 15MB gzipped build |
| Network | n/a | None | n/a |

No measurable game-loop cost. Event dispatch is bound by IME composition rate (human typing), not frame rate.

## Migration Plan

This is a greenfield decision — no prior implementation to migrate from.

### Phase 1 — Day 1 OSS evaluation (time-boxed ~2 hours)

1. Add `kou-yeung/WebGLInput` as a dependency via UPM Git URL. Entry already committed to `game/Packages/manifest.json`:
   ```json
   "com.github.kou-yeung": "https://github.com/kou-yeung/WebGLInput.git?path=Assets/WebGLSupport"
   ```
   Opening the Unity project triggers automatic import into `Packages/` (UPM cache, not under `Assets/`) so the license boundary and source tree stay clean. `packages-lock.json` will pin the resolved commit on first open.
2. Create a throwaway scene `OSS_IME_Probe.unity` with a single `TMP_InputField` plus kou-yeung's `WebGLInput` component attached. See `docs/phase-1-ime-evaluation-protocol.md` for the full scene setup checklist.
3. Build WebGL → serve locally → run the Korean test string (`"안녕하세요 계란찜 먼저 풀어서 물 넣고 쪄줘"`) in Chrome, Safari, Firefox on macOS.
4. Open the same WebGL build inside a quick `webview_flutter` wrapper on iOS Simulator. (Disable Simulator's hardware keyboard; add the Korean keyboard in simulated iOS Settings so the real IME path is exercised.)
5. **PASS** (zero dropped jamo on all four platforms) → thin-wrapper the library behind the `KoreanImeBridge` C# contract (§Key Interfaces) so downstream code is ignorant of the backing implementation. Mark ADR `Accepted`. Proceed to Day 3.
6. **FAIL** on any platform → note the specific failure (Safari composition event ordering? WKWebView focus?) and move to Phase 2.

### Phase 2 — Day 2 DIY fallback (only if Phase 1 failed)

7. Stub `KoreanImeBridge.cs` and `KoreanImeOverlay.jslib` under `game/Assets/Scripts/Bridge/`. Verify `SendMessage` round-trip with an ASCII-only test in Chrome desktop.
8. Wire the overlay positioning to a fixed Unity UI `RectTransform`. Confirm visual alignment at 1x, 1.25x, 1.5x browser zoom.
9. Re-run the Phase 1 test matrix (Korean composition across Chrome/Safari/Firefox/iOS WKWebView).
10. Focus-loss and re-focus paths; Enter-to-submit; `maxlength` enforcement; blur-on-outside-tap.
11. Day 2 end: PASS → mark ADR `Accepted` and proceed to Day 3. FAIL → supersede this ADR with ADR-0001-rev2 adopting Alternative 3 (Hybrid per-platform: overlay for web, Flutter TextField for iOS), re-prototype on Day 3, push one day into the buffer.

**Rollback plan**: If Phase 1 adopted an OSS library and a bug surfaces post-Day 2, revert the dependency and fall into Phase 2 — the `KoreanImeBridge` contract isolates the change to one swap. If Phase 2 itself also fails desktop validation, supersede this ADR with ADR-0001-rev2 adopting Alternative 3 (hybrid per-platform). The cascading fallback costs at most one timeline day and breaks the "single implementation" goal but preserves delivery.

## Validation Criteria

- [ ] Typing "안녕하세요 계란찜 먼저 풀어서 물 넣고 쪄줘" in Chrome desktop produces exactly that string on `OnSubmit`, with no dropped or duplicated jamo.
- [ ] Same string typed in iOS WKWebView inside `webview_flutter` produces the same result.
- [ ] `maxlength=80` prevents input beyond 80 chars at the DOM layer (verified by paste attempt).
- [ ] Enter key during active composition does NOT submit; Enter after composition completes submits.
- [ ] Clicking a Unity UI button outside the overlay's bbox dismisses the overlay without losing the current order state.
- [ ] Overlay position stays aligned to the Unity instruction box through a browser zoom cycle (100% → 125% → 100%) with no visible offset.
- [ ] `OnTextChanged` fires on every composition finalization; `OnSubmit` fires exactly once per Enter.

## GDD Requirements Addressed

| GDD Document | System | Requirement | How This ADR Satisfies It |
|--------------|--------|-------------|---------------------------|
| `design/gdd/game-concept.md` | Input (§5 기술 스택) | "Korean IME: HTML `<input>` overlay + `.jslib` 브릿지 (Unity WebGL IME 우회)" | Implements the overlay and the `.jslib` interop defined here |
| `design/gdd/game-concept.md` | Bridge (§6.4 Korean IME 오버레이) | "Unity WebGL canvas 위에 HTML `<input>`을 절대 위치로 겹치기; `.jslib`에서 브라우저 `compositionend` / `input` 이벤트를 받아 Unity C#로 전달" | Architecture section mirrors the GDD exactly and adds the Unity-side `KoreanImeBridge` C# contract the GDD left unspecified |
| `design/gdd/game-concept.md` | Acceptance (§8) | "Korean IME 입력이 데스크톱 브라우저와 모바일 WKWebView 양쪽에서 조합 중 글자 손실 없이 동작" | Validation Criteria enumerates the exact test strings and platforms the acceptance refers to |
| `design/gdd/game-concept.md` | Detailed Rules (§12) | "지시 입력: 한국어 1줄, ≤80자. 엔터로 제출" | `maxlength=80` and Enter-outside-composition submit path implement this |
| `design/gdd/game-concept.md` | Edge Cases (§14) | "모바일 WebView에서 IME 포커스 잃음 → Flutter 측 입력 필드로 자동 fallback" | Rollback plan preserves this fallback if Day-2 prototype fails |

## Related

- **Enables**: ADR-0002 (Bridge message protocol) — Unity ↔ JS SendMessage channel contract builds on the same interop mechanism.
- **Related spec**: [`design/gdd/game-concept.md`](../../design/gdd/game-concept.md) §5, §6.4, §8, §12, §14.
- **Engine reference**: [`docs/engine-reference/unity/VERSION.md`](../engine-reference/unity/VERSION.md) — Unity 6.3 LTS pin.
- **Primary Phase 1 candidate**: [`kou-yeung/WebGLInput`](https://github.com/kou-yeung/WebGLInput) — TMP support, UPM git package, Unity 2023.2+ target, actively maintained (2025-12), MIT. Installed via `game/Packages/manifest.json` entry `com.github.kou-yeung`.
- **Phase 1 fallback OSS candidates** (if primary fails validation):
  - [`unity3d-jp/WebGLNativeInputField`](https://github.com/unity3d-jp/WebGLNativeInputField) — Unity Japan official team. UGUI only, 2024-03 last commit. Usable but requires switching from TMP to UGUI `InputField` for the instruction box
  - [`decentraland/webgl-ime-input`](https://github.com/decentraland/webgl-ime-input) — production-validated in a live commercial metaverse
  - [`rehanlabs/Unity-WebGL-HTML-InputFix`](https://github.com/rehanlabs/Unity-WebGL-HTML-InputFix) — generic HTML input bridge
- **Phase 1 evaluation protocol**: [`docs/phase-1-ime-evaluation-protocol.md`](../phase-1-ime-evaluation-protocol.md) — scene setup, test matrix, pass/fail criteria
- **Evidence that the problem is real** (Phase 2 justification):
  - [Unity Manual — IME in Unity](https://docs.unity3d.com/Manual/IMEInput.html) — states IME not supported on Web platform
  - [Unity Issue Tracker — IME languages not recognized in WebGL builds](https://issuetracker.unity3d.com/issues/ime-languages-are-not-recognized-when-entering-input-in-mobile-and-webgl-builds-1)
  - [Unity Discussions — WebGL IME (TMP_InputField) feature request](https://discussions.unity.com/t/question-feature-request-about-webgl-ime-tmp-inputfield/917334)
- **Code paths (to be created only if Phase 2 fires)**:
  - `game/Assets/Scripts/Bridge/KoreanImeBridge.cs` (always created — interface layer, both phases depend on it)
  - `game/Assets/Scripts/Bridge/KoreanImeOverlay.jslib` (Phase 2 only)
  - `game/Assets/Scripts/UI/InstructionBox.cs` (Unity UI component that requests overlay show/hide — always created)
