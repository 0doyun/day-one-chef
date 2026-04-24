# ADR-0002: Bridge Message Protocol

## Status

Proposed (2026-04-25, Day 9). Promotes to Accepted after end-to-end
verification on both macOS debug host and iOS Simulator with Korean
IME inputs flowing through and round_end payloads arriving in the
Flutter Riverpod store.

## Date

2026-04-25

## Last Verified

2026-04-25

## Decision Makers

Solo developer (project owner).

## Summary

Unity WebGL and the Flutter shell need to exchange two kinds of
information during a session: **round results flowing from Unity to
Flutter** (the player just finished a round, here is the event log
and eventual evaluator verdict) and **lifecycle commands flowing from
Flutter to Unity** (reset the round, restart the whole session, etc.).
The WebView sits between them with one-way channels in each direction:
Unity emits via emscripten `.jslib` → WebKit JavaScript bridge;
Flutter drives inbound via `WebViewController.runJavaScript`. This
ADR locks the wire format, transport, and error-handling contract.

**Decision**: a **single flat JSON envelope** with a `type`
discriminator field, serialised by Unity's `JsonUtility` and parsed
by Dart's `json.decode`. No polymorphism, no sub-schemas — every
field lives at the top level and is ignored by sides that don't need
it. The JavaScript channel Flutter injects is named
`FlutterBridge`; Unity's receiver GameObject is named
`BridgeReceiver`. Both names are load-bearing constants that must be
changed in Unity C#, `.jslib`, the HTML template, and Dart together.

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS |
| **Domain** | WebGL / Emscripten bindings, Flutter `webview_flutter` platform channel |
| **Knowledge Risk** | MEDIUM — emscripten `mergeInto(LibraryManager.library, …)` + `UTF8ToString` is stable across Unity 6. `webview_flutter` `JavaScriptChannel` API (4.10+) is recent but well documented |
| **References Consulted** | Unity 6 WebGL browser-script interaction docs; `webview_flutter` 4.13 README; GDD §6 (Bridge) and §7 Day 9 |
| **Post-Cutoff APIs Used** | `WebViewController.addJavaScriptChannel` + `runJavaScript` — 4.x API surface; verified against 4.13.1 in Day 8 |
| **Verification Required** | EditMode tests for `BridgeMessage.RoundEnd` round-trip JSON shape. Play-mode round in the Flutter shell shows a `round_end` arriving in `gameResultsProvider` and the reset button driving Unity back to a freshly regenerated kitchen |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-0003 (Gemini Call Architecture) — defines the upstream response that feeds `round_end.eventLogJson`. ADR-0004 (Action Executor Model) — defines the event log shape carried by this bridge |
| **Enables** | Day 11 evaluator — once call #2 lands, `round_end` gains real `success` + `reason` fields with no wire-format change |
| **Ordering Note** | Ships Day 9 alongside the Unity `Bridge/` plugin code and the Flutter `FlutterBridge` class. The wire schema is versioned implicitly by field-layout — see Consequences for the upgrade plan |

## Context

### Problem Statement

A Unity WebGL build rendered inside a Flutter WebView has no direct
shared memory with Dart. Communication must cross two process-style
boundaries: Unity C# ↔ emscripten JS, then emscripten JS ↔ the host
WebView. The bridge has to carry:

- **round_end** (Unity → Flutter): the player just completed a round.
  Payload includes the order, the player's instruction, the opaque
  event log the Day 11 evaluator will later read, and — eventually —
  the evaluator's success/fail verdict.
- **session_end** (Unity → Flutter): the 5-round session finished.
  Payload includes per-round tallies.
- **reset_round / restart_session** (Flutter → Unity): the player
  tapped a control on the Flutter shell. No payload; the receiver
  simply reinitialises game state.

Day 5's Gemini call uses the same envelope-less flat JSON approach;
matching that pattern keeps the whole project's data shapes
consistent for a solo developer.

### Forces

- **Solo maintenance**: a single data struct on each side is easier
  to grep than a polymorphic hierarchy. Wire size matters less than
  reviewability.
- **JsonUtility constraints**: Unity's built-in serialiser does not
  support polymorphism, `System.Object`, or generics without custom
  glue. It *does* handle flat `[Serializable]` structs with public
  fields flawlessly.
- **Platform fidelity**: `webview_flutter` injects one JavaScript
  channel per name; multiple channels would duplicate JS plumbing.
  One `FlutterBridge` channel + a `type` discriminator is minimal.
- **Forward evolution**: the Day 11 evaluator will fill
  `round_end.success` + `round_end.reason`. No format change needed —
  Day 9 just emits `success=false, reason="pending evaluator"`.
- **Failure tolerance**: a malformed payload must not crash Flutter.
  A missing `type` must not crash Unity. Both sides log and drop.

### Constraints

- emscripten `.jslib` runs in the single WebGL worker thread — so all
  `BridgeSendToFlutter` calls are effectively synchronous to Unity,
  but delivery to Flutter is async (goes through WKWebView's message
  queue). Flutter must not assume 1:1 ordering with specific Unity
  frames.
- `webview_flutter` message delivery is best-effort; it is *not*
  a durable queue. Lost messages stay lost — the Day 11 evaluator
  architecture will keep authoritative state on Unity's side and let
  Flutter re-query rather than depending on every `round_end`
  landing.
- Flutter → Unity payloads go through `SendMessage(gameObject, method,
  payload)`. The payload is a single string. Any structured data
  needs to be JSON-encoded and escaped for single-quote wrapping in
  JS — the current `FlutterBridge._sendToUnity` only escapes `'`.

## Decision

### Outbound transport (Unity → Flutter)

```
 [C# GameRound]
    └─ UnityBridge.Send(BridgeMessage)          // JsonUtility.ToJson
        └─ BridgeOutgoing.jslib                 // BridgeSendToFlutter
            └─ FlutterBridge.postMessage(json)  // webview_flutter channel
                └─ _UnityHostPageState._bridge   // registered channel
                    └─ FlutterBridge._onIncoming(json)
                        └─ json.decode → BridgeMessage
                            └─ switch(type) → Riverpod providers
```

### Inbound transport (Flutter → Unity)

```
 [Dart FlutterBridge.sendResetRound]
    └─ controller.runJavaScript("unityInstance.SendMessage(
         'BridgeReceiver', 'ResetRound', ''
       )")
        └─ unityInstance.SendMessage              // emscripten shim
            └─ Unity dispatch                     // calls C# method by name
                └─ BridgeIncoming.ResetRound()
                    └─ static event OnResetRoundRequested
                        └─ GameRound.HandleResetRoundFromBridge
```

### Wire format

One JSON object, single flat layer:

```json
{
  "type": "round_end",
  "orderId": "egg-steamed-01",
  "orderTitle": "계란찜",
  "roundIndex": 3,
  "totalRounds": 5,
  "instruction": "계란 깨고 물 넣고 찜",
  "success": false,
  "reason": "pending evaluator (Day 11)",
  "eventLogJson": "{\"entries\":[{\"verb\":\"crack\", …}]}",
  "successCount": 0,
  "failCount": 0
}
```

- `type`: `"round_end"` or `"session_end"`. Unknown values → dropped
  with a warning on both sides.
- `eventLogJson`: the **serialised JSON string** of the Day 6 event
  log, not a nested object. Keeping it stringly-typed avoids the
  JsonUtility inability to nest Serializable children by default.
  Flutter treats the field as opaque until Day 11.
- Missing fields default (Dart `String ?? ''`, `bool ?? false`,
  `int ?? 0`). Sides never crash on field absence.

### Named constants (change all four together)

| Role | Constant | Defined in |
|------|---------|------------|
| JS channel name (Flutter side) | `FlutterBridge` | `app/lib/src/shell/flutter_bridge.dart` |
| JS channel name (Unity/.jslib side) | `FlutterBridge` | `game/Assets/Plugins/WebGL/BridgeOutgoing.jslib` |
| Unity receiver GameObject | `BridgeReceiver` | `game/Assets/Editor/MainKitchenSetup.cs` + `app/lib/src/shell/flutter_bridge.dart` |
| Global Unity instance handle | `window.unityInstance` | `game/Assets/WebGLTemplates/DayOneChef/index.html` |

## Alternatives Considered

### Alternative 1 — Polymorphic envelope with message-type subclasses

A base `BridgeMessage` class with concrete `RoundEndMessage`,
`SessionEndMessage`, etc., discriminated by `type` and dispatched to
per-type handlers.

**Rejected**: JsonUtility does not support polymorphism. Switching to
Newtonsoft on both sides (Dart has no Newtonsoft, would mean
`freezed` + `json_serializable`) would add a build step and two
dependencies to solve a problem that only exists at ~3 message
types. The flat-envelope cost is one unused-field line per type.

### Alternative 2 — Multiple named JavaScript channels

One channel per message type (`FlutterBridgeRoundEnd`,
`FlutterBridgeSessionEnd`).

**Rejected**: `webview_flutter` channel registration is per-channel
boilerplate, and the `.jslib` side would need to pick a channel by
type — duplicating the discriminator logic at the JS boundary. Not
worth it for ~3 types.

### Alternative 3 — MessagePack / binary framing

Smaller wire size, stricter typing.

**Rejected**: Adds dependencies on both sides, no runtime size
concern at this cadence (one message per round, 500 ms–2 s apart),
and debugging in Chrome DevTools / Flutter logs becomes painful.

## Consequences

### Positive

- Single grepable wire format — finding every use of `round_end`
  reads as three matches, not a class hierarchy.
- Day 11 evaluator slots into `round_end.success` + `.reason`
  without a wire-format change.
- Editor-build parity via `UnityBridge`'s `#if UNITY_WEBGL` guard:
  calling code is identical across platforms; the editor stub logs
  the payload to Debug so Day 9-era tests exercise the serialisation.

### Negative

- Growing message variety eventually bloats the flat envelope with
  mostly-empty fields. The breakpoint is around ~8 message types; we
  expect to ship ~3 for the 14-day scope.
- `.jslib` + `mergeInto` setup is opaque to new contributors; a
  one-paragraph onboarding note in `docs/architecture/` is
  warranted if team size grows.

### Neutral

- Flutter → Unity payloads are unencoded strings; fine for Day 9's
  payload-less commands, will need proper JSON-escape helpers when
  we add parameterised messages (e.g., "skip to order X").

## Rollback / Versioning

No explicit schema version in the envelope yet — fields are additive
and tolerant of unknown keys (both sides ignore them). When the
schema requires a breaking change:

1. Add `"schemaVersion": 2` to new messages.
2. Have Flutter branch on `schemaVersion` before field access.
3. Deprecate the v1 fields once no released builds still emit them.

If the protocol produces an unrecoverable parse error in production,
the fallback is that the Flutter UI shows a stale or zero state —
gameplay in Unity is unaffected because the event log is
authoritative in Unity and can be re-requested on the Day 11
evaluator call.

## GDD Requirements Addressed

- §6 — Bridge architecture (Unity ↔ JS ↔ Flutter)
- §7 Day 9 — 양방향 브릿지 + round_end + reset
- §14 — resilience to malformed payloads (edge cases)
