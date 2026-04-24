# ADR-0004: Chef Action Executor Model

## Status

Proposed (2026-04-24, Day 6). Promotes to Accepted after the Day 11
evaluator consumes the shipped event log and validates the 계란찜
order-sensitive failure reason end-to-end.

## Date

2026-04-24

## Last Verified

2026-04-24

## Decision Makers

Solo developer (project owner).

## Summary

Gemini call #1 returns an `actions[]` array the chef must execute in
order, at GDD §13's fixed `ACTION_TICK = 0.6s` cadence, while recording
a ground-truth event log for Day 11's evaluator (call #2). Three
implementation models were considered: a monolithic interpreter
(single big `switch`), a state-machine with per-verb handlers, and a
full Command pattern (one `ICommand` per verb). **Decision: state-
machine with per-verb handlers.** For 8 locked verbs on a 14-day
prototype the dispatch-table approach gives the best
readability / testability / effort ratio — each verb is a named method,
the executor is trivially unit-testable, and adding a ninth verb later
is a 4-line change rather than a new class.

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS |
| **Domain** | Gameplay (action dispatch + state transitions + event log) |
| **Knowledge Risk** | LOW — async/await + `Task.Delay` + `Time.time` are stable across Unity 6. No post-cutoff APIs |
| **References Consulted** | GDD §1.2 step 4, §4.1 (verb table), §4.2 (event log contract), §13 (`ACTION_TICK`, `GEMINI_TIMEOUT`), §14 (edge cases) |
| **Post-Cutoff APIs Used** | `FindObjectsByType` (Unity 6.0+) in `GameRound.RebuildKitchen`. Documented in engine-reference |
| **Verification Required** | EditMode tests for happy path, unknown verb skip, unknown ingredient skip, disallowed transition, 계란찜 order-sensitive sequence preservation. Play-mode round fires a real Gemini response through the executor at 0.6s ticks |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-0003 (Gemini Call Architecture — supplies the `ChefActionResponse` the executor consumes) |
| **Enables** | ADR-0005 (pending) Evaluator ground-truth contract — event log JSON schema stabilises here |
| **Blocks** | Day 11 evaluator work (call #2 needs the event log shape) |
| **Ordering Note** | Ships Day 6 alongside the action executor implementation. Event log wire schema frozen; the Day 11 evaluator prompt-builder serializes the same struct |

## Context

### Problem Statement

The round execution phase is:

1. Gemini returns `actions[]` (up to ~10 items).
2. Unity replays them one at a time, 0.6 s apart, so the player sees
   the chef perform them in sequence (GDD §13).
3. Each verb maps to a kitchen state mutation (ingredient
   Raw → Cooked, egg Shell → Cracked, etc.) that Day 11's evaluator
   reads to judge success / failure.
4. For order-sensitive orders (§2 #5, 계란찜), the evaluator needs the
   *order of execution*, not just the final state — "the chef put water
   before cracking the egg" must be recoverable.

So the executor has two simultaneous duties:

- **Drive presentation** — 0.6 s tick, visible motion (Day 7+ polish).
- **Record truth** — serialisable event log the evaluator will read.

### Forces

- **8 verbs, no more**: The verb set is locked in the system prompt
  (ADR-0003) and in GDD §4.1. Any new verb would require an ADR plus
  a prompt change. Extensibility beyond 8 is not a near-term concern.
- **Testable without scenes**: EditMode tests should be able to run
  the executor against a hand-built `ChefActionResponse` and assert
  event-log shape + final kitchen state. MonoBehaviour-heavy
  solutions fight this.
- **Evaluator stability**: the event log JSON is a contract between
  the executor (Day 6) and the evaluator (Day 11). Changing its shape
  is a cross-ADR ripple. Pin the struct now.
- **14-day budget**: avoid architecture tourism. The first shape that
  passes the tests wins.

### Constraints

- Unity 6 async/await via `UnityMainThreadScheduler` — safe to
  `await Task.Delay(600)` and keep main-thread affinity.
- Ingredient state transitions go through `IngredientDefinition.Allows`
  (Day 4) so "cook cheese" is silently skipped rather than corrupting
  the run.
- No allocation in hot paths: the executor runs at 0.6 s tick so
  allocations for the event log entries are acceptable.

## Decision

### Primary approach — state-machine with per-verb handlers

`ActionExecutor` owns a `KitchenState` and pumps `ChefActionResponse.
actions[]` through a dispatch table:

```csharp
switch (verb)
{
    case ChefVerb.Pickup:   ApplyPickup(entry);   break;
    case ChefVerb.Cook:     ApplyCook(entry);     break;
    case ChefVerb.Chop:     ApplyChop(entry);     break;
    case ChefVerb.Crack:    ApplyCrack(entry);    break;
    case ChefVerb.Mix:      ApplyMix(entry);      break;
    case ChefVerb.Assemble: ApplyAssemble(entry); break;
    case ChefVerb.Serve:    ApplyServe(entry);    break;
    case ChefVerb.Move:     ApplyMove(entry);     break;
}
```

Each `Apply*` method resolves the target (via `KitchenState.
TryResolveIngredient`/`TryResolveStation`), runs a single state
transition, and writes `resolvedType`/`resolvedState` onto the log
entry. Unknown verbs / targets / disallowed transitions set `skipped=
true` with a `reason` string — per GDD §4.3 "준비되지 않은 행동".

Between entries, `await Task.Delay(600, ct)` gates the next tick.
`CancellationToken` support lets `GameRound` abort a round mid-flight
if the user hits reset (Day 8+ bridge work).

### Event log shape

Each entry is a `EventLogEntry` POCO:

```json
{
  "verb":           "crack",
  "target":         "계란",
  "param":          "",
  "t":              0.0,
  "skipped":        false,
  "reason":         null,
  "resolvedType":   "Egg",
  "resolvedState":  "Cracked"
}
```

Wrapped under `{ "entries": [...] }` for the evaluator prompt. The
"resolvedType" + "resolvedState" fields are redundant for success
judgement but invaluable for explaining WHY a round failed — the Day
11 evaluator can say "the egg was Beaten, not Cooked" rather than a
vague "something went wrong".

## Alternatives Considered

### Alternative 1 — Monolithic interpreter

A single method with nested `if/switch` covering every verb/target
combination.

**Rejected**: fine at 2 verbs, unreadable at 8, untestable in parts.
The per-verb methods are short enough that splitting costs nothing.

### Alternative 2 — Command pattern with `IVerbCommand`

One concrete class per verb implementing a common interface:

```csharp
interface IVerbCommand { Task ApplyAsync(EventLogEntry entry); }
class PickupCommand : IVerbCommand { ... }
```

**Rejected**: real extensibility gain only if verbs came from outside
the codebase (plugins, modding). They don't — they're locked by ADR-
0003. The indirection hurts grep-ability for a solo developer
scanning the log → code path.

### Alternative 3 — Scriptable-object per verb

`VerbSO` asset with inspector-configurable state transitions, loaded
by a `VerbRegistry` at runtime.

**Rejected**: transfers code into ScriptableObject YAML, which is
worse to version-control and worse to step through in the debugger.
Pretend extensibility for zero real return.

## Consequences

### Positive

- Day 6 EditMode tests exercise every verb branch without Unity
  lifecycle. Tests stay fast (< 1 s for the suite).
- Adding a 9th verb (e.g. a pending `taste` / `plate` variant) is one
  enum value + one `Apply*` method + one system-prompt update.
- `KitchenState` is the single source of ground-truth; the evaluator
  reads the same structure the executor mutates.

### Negative

- Per-verb handlers live in `ActionExecutor.cs`. At ~8 handlers the
  file is ~250 lines — still reviewable, but if the verb table
  doubles we should partial-class-split.
- No built-in retry of a single failed verb. A skipped verb is
  skipped permanently within its round. This matches the "chef
  executes literally" tone from GDD §1.4, and retry logic adds
  reason-for-failure ambiguity that hurts the evaluator.

### Neutral

- `Task.Delay` uses Unity's synchronisation context so continuations
  are main-thread safe. If WebGL's single-threaded JS runtime proves
  problematic during Day 7 build testing, we'll swap to
  `UnityMainThreadScheduler.Schedule` or coroutines. Either fits
  the per-verb shape.

## Rollback Plan

If the executor proves insufficient for a future Day (e.g. the Day 11
evaluator needs per-verb retry metrics, or Day 13 animation needs
richer per-verb enter/exit hooks):

1. Split the dispatch table into a `IVerbHandler` registry (Alternative 2)
   without changing the event log shape. Callers of `ActionExecutor`
   are unaffected.
2. If the event log shape itself grows (parallel actions, sub-steps),
   version it: add `schemaVersion: 2` to the JSON wrapper and have
   the evaluator handle both.

## GDD Requirements Addressed

- §1.2 step 4 — "Unity가 JSON actions 배열을 0.6초 간격으로 순차 애니메이션
  실행하며 event_log에 기록"
- §4.1 — 8 verbs, param values (`grill`/`fry`/`steam`)
- §4.2 — event log with timestamped actions for order-sensitive eval
- §4.3 — unknown verb / non-existent ingredient → skip + flag in log
- §13 — `ACTION_TICK = 0.6s`
- §14 — empty actions array, invalid verb, disallowed transition
