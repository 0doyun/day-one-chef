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

Unity WebGL's built-in text input (`TMP_InputField`, `InputField`) drops hangul characters mid-composition on every major browser, so the project cannot rely on it for the single-line Korean instruction field that drives the entire core loop. We will place an HTML `<input>` element as a positioned overlay above the Unity canvas and route browser IME composition events into Unity via a `.jslib` bridge, giving us one working implementation that serves both the Vercel web build and the iOS Flutter WebView shell (WKWebView-backed).

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

Render a transparent-bordered HTML `<input type="text">` element as an absolutely-positioned overlay pinned to the instruction box region of the Unity canvas. The overlay owns Korean IME composition natively through the browser. On `compositionend` (for IME finalization) and on `input` (for direct ASCII and backspace), the overlay's JavaScript pushes the current value into Unity via `SendMessage` on a dedicated C# GameObject. Unity treats the input box as a *view only* of the overlay's state — it never authors characters itself.

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

1. Day 1: Stub `KoreanImeBridge.cs` and `KoreanImeOverlay.jslib`. Verify `SendMessage` round-trip with an ASCII-only test in Chrome desktop.
2. Day 1: Wire the overlay positioning to a fixed Unity UI `RectTransform`. Confirm visual alignment at 1x, 1.25x, 1.5x browser zoom.
3. Day 2: Korean IME test matrix — Chrome/Safari/Firefox on macOS; iOS WKWebView inside `webview_flutter`. (When testing in the iOS Simulator specifically, disable Simulator's hardware keyboard and add the Korean keyboard in simulated iOS Settings so the real IME path is exercised.) Record hangul from "ㄱ ㅏ" → "가" → "감사합니다" with zero dropped jamo.
4. Day 2: Focus-loss and re-focus paths; Enter-to-submit; maxlength enforcement; blur-on-outside-tap.
5. Day 2 end: PASS → mark ADR Accepted and proceed to Day 3. FAIL → switch to Alternative 2 (Flutter TextField) for the iOS path only, re-prototype on Day 3, push one day into the buffer.

**Rollback plan**: If the overlay approach fails desktop validation on Day 2, supersede this ADR with ADR-0001-rev2 adopting Alternative 3 (hybrid). The fallback costs one timeline day and breaks the "single implementation" goal but preserves delivery.

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
- **Code paths (to be created)**:
  - `game/Assets/Scripts/Bridge/KoreanImeBridge.cs`
  - `game/Assets/Scripts/Bridge/KoreanImeOverlay.jslib`
  - `game/Assets/Scripts/UI/InstructionBox.cs` (Unity UI component that requests overlay show/hide)
