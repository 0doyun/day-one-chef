# ADR-0005: Evaluator Ground-Truth Contract

## Status

Proposed (2026-04-25, Day 11). Promotes to Accepted after the next
end-to-end run with all five rounds (toast → salad → cheeseburger →
egg-fried-rice → 계란찜) returning consistent `success` + Korean
`reason` strings, including a deliberate failure injection on 계란찜
to verify order-sensitivity.

## Date

2026-04-25

## Last Verified

2026-04-25

## Decision Makers

Solo developer (project owner).

## Summary

Day 6 produced the **event log** — every chef action, in time order,
plus per-ingredient state transitions. Day 11 needs to **judge** that
log: did the player's instruction produce the customer's order? This
ADR locks the *contract* between the executor's ground truth (input)
and Gemini call #2's verdict (output) so the same shape carries
through `BridgeMessage.RoundEnd` (ADR-0002) into Flutter's
`gameResultsProvider` and, on Day 13, the visible result log.

**Decision**: a **single Gemini call per round**, low temperature
(0.2), system prompt in Korean, structured JSON response
`{success: bool, reason: string}`. The user prompt is
hand-rendered Korean prose (not JSON) so the model can read order
details, the player's literal Korean instruction, the time-ordered
event log, and the final kitchen state in one pass.

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS |
| **Domain** | Gameplay AI / round evaluation |
| **Knowledge Risk** | LOW — Gemini API endpoints + JsonUtility are stable across Unity 6 |
| **References Consulted** | GDD §2 (5 orders, 계란찜 order-sensitive flag), §4.2 (event log), §13 (timeouts), §16 (call #2 temperature ≈ 0.2) |
| **Post-Cutoff APIs Used** | Reuses ADR-0003 Phase B proxy path on WebGL builds |
| **Verification Required** | EditMode tests on the prompt builder + envelope parser; play-mode round shows the verdict on the round_end path with sensible Korean reason |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-0003 (Gemini Call Architecture) — same proxy / direct switch. ADR-0004 (Action Executor Model) — event log shape consumed verbatim. ADR-0002 (Bridge Message Protocol) — `round_end.success` + `.reason` are the destination |
| **Enables** | Day 12 result log UI (Flutter), Day 14 polish + balancing |
| **Ordering Note** | Ships Day 11. Wire format already in place since Day 9 — Day 11 only changes *who fills* the success/reason fields |

## Context

### Problem Statement

Each of the 5 orders has multiple legitimate ways to succeed and many
ways to fail. The classic case is 계란찜 (steamed egg, GDD §2 #5):
the player must crack the egg *before* adding water and *before*
cooking. A pure final-state comparison (egg = Cooked) cannot tell
"crack → mix water → cook" (success) from "mix water → crack → cook"
(failure: shell is now in the steamed egg). The order-sensitivity
flag on `Recipe` (Day 4) signals which orders need this verification,
but the actual judgement requires either:

- A hand-coded rule engine — one ruleset per order — that grows
  combinatorially with order count and duplicates the LLM's general
  reasoning ability.
- A second Gemini call that takes the event log + final state +
  natural-language order spec and returns a verdict.

The latter aligns with the project's "AI as judge" tone (per the GDD
brief) and stays within free-tier budget at one extra call per round.

### Forces

- **One free-tier call per round**: 계란찜 is the rare order-sensitive
  one; the others can be judged on final state alone, but routing
  them through the same evaluator simplifies the engine path. Two
  Gemini calls × 5 rounds ≈ 10 calls per session — well within the
  free quota.
- **Low temperature**: a verdict should not vary across re-runs of
  the same evidence. 0.2 is conservative; 0.0 deterministic mode
  exists but Gemini's free-tier rejected it intermittently in Day 5
  spike testing.
- **Korean reason strings**: the player just typed Korean and is
  reading Korean UI; an English reason breaks the loop tone.
- **Hand-rendered prose prompt**: the model performs better with
  natural-language framing of structured data than with raw JSON
  inputs. The user prompt is Korean prose; the response is JSON.
- **No streaming**: the verdict is a single sentence. Streaming adds
  complexity for ~80 tokens of payload.
- **Failure tolerance**: a 503 / parse error / timeout must not
  hang the round. Treat as `success=false, reason="evaluator
  error: …"` so the round still completes and the next one starts.

### Constraints

- Total round budget: ≤ 10 s per round (call #1 + executor + call
  #2). Call #2 is the smaller call (<200 input tokens, <50 output
  tokens) and lands in ~1.5–3 s on the free tier.
- Same proxy / direct switch as call #1 (ADR-0003) — WebGL builds
  use `/api/gemini/{model}:generateContent`, editor builds use the
  direct Google URL with EditorPrefs key.

## Decision

### Wire format

**Input (Unity → Gemini, hand-rendered Korean prose)**:

```
[주문 정보]
이름: 플레인 토스트
ID:   토스트-01
요구 재료/상태:
  - Bread → Cooked
순서 민감: false

[플레이어 입력 지시]
"빵 화구에 올려서 구워줘"

[셰프가 실제 한 행동 — 시간순 이벤트 로그]
  1. (t=0.00s) pickup 빵 → Bread Raw
  2. (t=0.60s) cook 빵 (grill) → Bread Cooked

[조리 후 주방 상태]
  Bread = Cooked
  Patty = Raw
  ...

위 정보로 판정. JSON만 응답.
```

**Output (Gemini → Unity, JSON)**:

```json
{
  "success": true,
  "reason": "빵을 화구에서 잘 구워 토스트가 완성되었습니다."
}
```

### Code surfaces

| Type | Role |
|------|------|
| `IRoundEvaluator` | Single-method interface — `EvaluateAsync(EvaluationContext, ct) → RoundEvaluation` |
| `EvaluationContext` | POCO bundle: `Order`, player instruction, `EventLog`, `IReadOnlyDictionary<IngredientType, IngredientState>` final state |
| `RoundEvaluation` | `{ bool success; string reason; }`. JsonUtility-friendly |
| `EvaluatorPromptBuilder` | Static — `BuildSystemPrompt()` + `BuildUserPrompt(EvaluationContext)`. Pure functions, fully unit-testable |
| `GeminiRoundEvaluator : IRoundEvaluator` | Sends the request via the same UnityWebRequest path as `GeminiClient`. Proxy / direct switch by `#if UNITY_WEBGL && !UNITY_EDITOR`. Temperature pinned to 0.2 |

### Failure modes

- **HTTP error / timeout**: `RoundEvaluation.Failure("evaluator error: …")`. Round still emits `round_end` with `success=false`.
- **Empty / malformed envelope**: same — log + downgrade.
- **Inner JSON missing `reason`**: filled with a fallback ("성공" / "사유 미명시") so the Flutter UI never shows an empty bubble.
- **Cancellation**: propagates as `OperationCanceledException` — the `BridgeIncoming` reset path will abort the in-flight call.

## Alternatives Considered

### Alternative 1 — Hand-coded rule engine per order

A `IRoundEvaluator` implementation per order with hard-coded checks:
"if order is 계란찜, look for a `crack` event before any `mix` event;
if order is 치즈버거, every component must be in correct state".

**Rejected**: combinatorial growth as new orders ship; duplicates
reasoning the LLM already does; loses the natural-language reason
strings that the GDD's tone leans on.

### Alternative 2 — Single Gemini call doing both action and evaluation

Have call #1 emit `actions[] + monologue + future_verdict_template`,
then locally check the executor's outcome against that template.

**Rejected**: the action generator is already at the limit of what
fits in a low-latency single call; mixing in a self-judging step
makes the model less reliable. Two calls keep concerns clean.

### Alternative 3 — Streaming verdict

Use Gemini's streaming endpoint so partial verdicts appear as they
generate.

**Rejected**: payload is ~50 tokens; streaming complexity isn't
worth it for ~1 s of latency hidden by the 0.6 s ACTION_TICK pacing.

## Consequences

### Positive

- New orders need only an `Order.asset` + `Recipe.asset` + (if
  order-sensitive) the system-prompt clause that already exists.
  No new code per order.
- Korean reason strings flow through to Flutter unchanged — the
  bridge message and result UI never need to be in the language
  business.
- The evaluator is independently testable: prompt builder is pure
  functions; envelope parser is a static method with hand-built
  envelope strings in the test suite (see
  `EvaluatorPromptBuilderTests`).

### Negative

- Doubles per-round Gemini API calls. At free-tier limits this
  matters during heavy testing — the 8 s timeout (GDD §13) plus
  retry policy can stretch a 5-round session to ~50 s of Gemini
  wait time worst-case.
- The judge IS the LLM. Aborting from the LLM's reasoning is
  hard once a misjudgement lands; tuning is via prompt tweaks
  rather than code, so prompt drift can degrade judgement
  silently. Mitigate by snapshotting the prompt builder output
  in EditMode tests so changes show as diffs.

### Neutral

- Same proxy infrastructure as call #1 — no additional Flutter
  work for shipped builds.
- Reuses `GeminiRequestBody` / `GeminiResponseEnvelope` shapes —
  if a third Gemini call appears later we'll extract those into a
  shared `GeminiEnvelope` helper.

## Rollback Plan

If evaluator quality proves unworkable in playtesting:

1. Inject a `RuleBasedRoundEvaluator : IRoundEvaluator` (Alternative
   1) for the specific order(s) that misbehave. `GameRound` already
   accepts an injected evaluator, so the swap is one line.
2. Keep the Gemini path for general-purpose orders.
3. If the entire approach fails, downgrade to "match Recipe.Components
   against KitchenState.State" and ship `success=true/false` only
   (no Korean reason) — drops a polish element but keeps the loop.

## GDD Requirements Addressed

- §1.2 step 5–6 — "Gemini 평가 → 결과(success/reason) 표시"
- §2 #5 — 계란찜 order-sensitive constraint
- §4.2 — event log feeds into evaluator
- §13 — `GEMINI_TIMEOUT = 8s` shared with call #1 timeout config
- §16 — call #2 temperature 0.2
