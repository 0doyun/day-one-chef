# ADR-0003: Gemini Call Architecture

## Status

Proposed (2026-04-24, Day 5). Becomes Accepted once Day 8–9 ships the
Flutter proxy implementation and the direct-from-Unity path is retired
from production builds.

## Date

2026-04-24

## Last Verified

2026-04-24

## Decision Makers

Solo developer (project owner).

## Summary

Gemini 2.5 Flash (call #1 action generator and call #2 evaluator) must
be reachable from the game. Two architectural options exist: the game
calls Gemini directly from Unity WebGL with the API key bundled into
the client, or it proxies every call through the Flutter shell, which
holds the key server-side to the WebView. The direct path is fast to
stand up but leaks the key to anyone who opens devtools on the shipped
build; the proxy path is safe but only materialises once the Flutter
shell and JS bridge exist (Day 8–9). **Decision: ship the direct path
on Day 5 behind an `IGeminiClient` interface; swap to a Flutter-proxy
implementation on Day 8–9 without touching any gameplay code.** The
direct-from-Unity client is explicitly prototype-only and must not
service a public URL.

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS |
| **Domain** | Networking (`UnityWebRequest`) + Gameplay (state → prompt) |
| **Knowledge Risk** | LOW for the `UnityWebRequest` path (stable since 2019 LTS). MEDIUM on the Gemini REST surface — `generateContent` has evolved since the LLM cutoff (structured output, response schemas) but the minimal envelope (`candidates[].content.parts[].text`) used here is stable |
| **References Consulted** | Google AI for Developers — `generateContent` REST reference; Unity Manual — `UnityWebRequest` with POST + JSON |
| **Post-Cutoff APIs Used** | None on the Unity side. Gemini's `responseMimeType: application/json` is used for convenience but the parser does not require structured output to function — any JSON-in-text will parse |
| **Verification Required** | Editor Play mode posts a test instruction, receives a parsed `ChefActionResponse` with at least one entry in `actions` and a non-empty `monologue`. WebGL build verification is deferred to Day 7 once compression + heap tuning are frozen |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-0002 (Bridge message protocol — the proxy path flows through the same Unity ↔ JS ↔ Flutter channel; ADR-0002 is not yet authored, so the proxy lands alongside it on Day 8–9) |
| **Enables** | Day 6 action executor, Day 11 evaluator (call #2 reuses the same interface + transport) |
| **Blocks** | Any public deployment (Vercel, TestFlight, Play Store). Public builds must swap to the Flutter proxy implementation first |
| **Ordering Note** | Direct path ships Day 5 for iteration velocity; proxy path ships Day 8–9 and the ADR promotes to Accepted then |

## Context

### Problem Statement

The core loop requires two Gemini calls per round: one to translate the
player's 지시 into an `actions[]` array plus a `monologue` (§4.1), and
one to judge the final kitchen state against the order (§4.2). Both
calls take an API key; Google's free tier is rate-limited per key and
the keys are revocable.

The Unity WebGL binary is served to the browser. Anything bundled into
it — including a string constant or a ScriptableObject field — is
readable by an attacker with a file inspector or devtools Network tab.

The Flutter shell (Day 8) will own the cross-boundary bridge to the
WebView. It is the natural point to centralise the API key: the shell
can hold it in an `--dart-define`-backed `String.fromEnvironment`, make
the HTTPS call itself, and hand Unity only the response.

### Forces

- **Iteration velocity**: Day 5's goal is "call #1 working end-to-end,
  with structured actions, so Day 6's executor has something to
  execute". Waiting for the Flutter shell (Day 8) to exist would
  compress gameplay work into the last 6 days of the schedule.
- **Security**: Production builds must not leak the key.
- **Testability**: The evaluator (Day 11) should be able to run in
  Play mode against a mock client for unit-testable failure-mode coverage.
- **Prototype posture**: This is a 14-day prototype. An expected-to-be-
  rotated free-tier key tolerated for a week of dev iteration is very
  different from a forever-live production key.

### Constraints

- Unity 6.3 LTS, IL2CPP WebGL target.
- Newtonsoft.Json NOT currently installed; Day 5 uses built-in
  `JsonUtility`. ADR-0001's bridge work already established that
  pulling new Package Manager deps mid-prototype is expensive.
- GDD §13: `GEMINI_TIMEOUT = 8s`, `GEMINI_RETRY = 1`. Client must honor.

## Decision

### Primary approach — direct from Unity, behind an interface

Day 5 ships `Assets/Scripts/Gameplay/AI/GeminiClient.cs` implementing
`IGeminiClient`. The client:

1. Reads the API key from `EditorPrefs` (Editor) / `PlayerPrefs`
   (builds) via `GeminiCredentials`. The key is **never** stored in a
   ScriptableObject, scene, or any committed asset.
2. Posts `generateContent` via `UnityWebRequest` with a
   `GeminiConfig`-owned model name, endpoint URL, temperature, timeout,
   and retry count (all committable tuning values).
3. Parses the envelope → extracts `candidates[0].content.parts[0].text`
   → parses that as `ChefActionResponse`.
4. Emits `GeminiCallException` on HTTP failure, empty envelope, or
   inner JSON parse failure. `GameRound` catches the exception and
   invalidates the round (GDD §4.3).

`IGeminiClient` is the only thing gameplay code references. `GameRound`
constructs a `GeminiClient` lazily if no other implementation is
injected.

### Day 8–9 migration — Flutter proxy

When the Flutter shell ships, a second implementation lands:

- `FlutterProxyGeminiClient : IGeminiClient` — calls into the JS bridge
  (SendToWebView with type=`gemini_request`), Flutter receives the
  request, calls the Gemini REST endpoint with its key, returns the
  response via `unityInstance.SendMessage("GameRoot", "OnGeminiResponse", ...)`.
- `MainKitchenSetup` (or the Flutter-aware equivalent) picks which
  implementation to inject based on a scene / build-time flag.
- `GeminiClient` (direct) is kept for Editor iteration and standalone
  web deploys that go through Flutter anyway.

Tests continue to pass without changes because they depend on
`IGeminiClient` only.

## Alternatives Considered

### Alternative 1 — Flutter proxy from Day 1

Sequence the schedule so Flutter + bridge land before any Gemini call.

**Rejected**: blocks 6 days of gameplay work behind the bridge ADR
that isn't authored yet. Accepts worse schedule risk than key exposure
risk for a prototype.

### Alternative 2 — Third-party API gateway (e.g., Vercel serverless function)

Deploy a stateless function that proxies Gemini calls and rejects
requests without a shared-secret header the Unity build embeds.

**Rejected**: adds a deployment surface we don't otherwise need,
still embeds a secret in the client (the shared-secret), and duplicates
the work the Flutter shell does. Flutter wins on simplicity once it
exists; until then, direct with rotatable key is cheaper.

### Alternative 3 — Keyless / signed-request with Google Identity

Use short-lived credentials (OAuth device flow, etc.) so no long-lived
key is embedded.

**Rejected**: Gemini Developer API (free tier) does not offer this
authentication mode — it's API-key-only. Vertex AI Gemini supports
service accounts and signed requests but requires a billed Google
Cloud project, adding cost + setup scope well beyond prototype.

## Consequences

### Positive

- Day 5 ships a working call #1 end-to-end. Day 6 executor can consume
  real Gemini output immediately.
- `IGeminiClient` seam makes the Day 8 swap mechanical.
- API key never touches the repo, never touches a committed asset.
  Git history stays clean; key rotation is a single EditorPrefs write.
- Tests use canned envelope strings (`ChefActionParsingTests`), no
  network dependency in CI.

### Negative

- Any WebGL build produced between Day 5 and Day 8–9 contains the
  API key. These builds are for local dev only — **do not** push to
  Vercel or share the URL publicly.
- The `EditorPrefs`/`PlayerPrefs` key storage is per-machine; a fresh
  clone on another machine needs `Tools → Day One Chef → Set Gemini
  API Key…` run again. Acceptable for solo dev.
- Client-side JSON parsing uses `JsonUtility`, which silently ignores
  unknown fields and defaults missing fields. Fine for the simple call
  #1 schema; call #2 evaluator (Day 11) may need Newtonsoft for
  better error surfacing.

### Neutral

- Retries on HTTP failure: 1 by default (per GDD §13). Configurable
  via `GeminiConfig` without code changes.

## Rollback Plan

If the direct-from-Unity path fails before Day 8 (e.g., Google rotates
the free-tier API without notice, CORS becomes a problem, or
`UnityWebRequest` can't reach `generativelanguage.googleapis.com` from
WebGL):

1. Stand up a minimal Vercel/serverless proxy (Alternative 2) as an
   interim — 1-day effort, holds us over to Day 8.
2. If that also fails: fall back to a **rule-based executor** that
   parses the player 지시 with simple keyword matching on the 8 verbs.
   Loses the LLM-driven monologue but keeps the core loop playable
   for the demo. Documented as the §4.3 "free-tier exhausted" fallback.

## Security Notes

- `GeminiCredentials` keeps the key out of the repo and the scene.
- Anyone with network access to the machine running the build can
  still sniff the key from the HTTPS request metadata. That's
  acceptable for local dev, not for public URLs. Enforce via the
  "don't publish builds made before Day 9" rule in `CLAUDE.md`-scope
  deploy checklists.
- Key rotation: if a direct-path build leaks, regenerate the key in
  Google AI Studio and run `Tools → Day One Chef → Set Gemini API Key…`
  again. No code changes required.

## GDD Requirements Addressed

- §1.2 core loop step 3 (Gemini call #1 action generation)
- §4.1 action schema — 8 verbs, `actions[]` + `monologue` JSON contract
- §4.3 failure modes — timeout / parse failure / invalid verb flag
- §13 `GEMINI_TIMEOUT`, `GEMINI_RETRY` fixed constants
- §16 `GEMINI_TEMPERATURE` tuning knob (0.7 for call #1)
