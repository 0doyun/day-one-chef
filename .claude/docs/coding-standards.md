# Coding Standards

- All game code must include doc comments on public APIs
- Every system must have a corresponding architecture decision record in `docs/architecture/`
- Gameplay values must be data-driven (external config), never hardcoded
- All public methods must be unit-testable (dependency injection over singletons)
- Commits must follow the **Conventional Commits** spec (see below) and reference the relevant design document or task ID in the body when applicable

# Commit Message Convention

This project uses [Conventional Commits](https://www.conventionalcommits.org/) v1.0.

## Format

```
<type>(<scope>): <short summary>

[optional body — wrap at 72 chars]

[optional footer — BREAKING CHANGE / Refs / Closes]
```

- **Subject line**: ≤ 72 chars, imperative mood ("add" not "added"), no trailing period
  - **Describe the change, not the schedule**: do NOT put sprint-day
    markers like `Day 6 —`, `Week 2:`, `Sprint 3:` in the subject.
    Schedule context belongs in the PR description and rots in git
    history (future reader has no "day 6" context). Bad:
    `feat(game): Day 6 — action executor + event log`. Good:
    `feat(game): add chef action executor with 0.6s tick and event log`.
- **Body**: explain *why*, not *what* (the diff shows what). Reference GDD sections, ADRs, or task IDs when applicable
- **Footer**: `BREAKING CHANGE: ...` for breaking changes, `Refs: design/gdd/foo.md` or `Closes: #123` for links

## Types

| Type | Use for |
|---|---|
| `feat` | New user-facing feature or gameplay mechanic |
| `fix` | Bug fix |
| `docs` | Documentation only (README, GDD, ADR, comments) |
| `style` | Formatting, whitespace, no logic change |
| `refactor` | Code restructure without behavior change |
| `perf` | Performance improvement |
| `test` | Adding or fixing tests |
| `build` | Build system, dependencies, Unity packages, Flutter pubspec |
| `ci` | CI/CD config (GitHub Actions, hooks) |
| `chore` | Project config, template setup, tooling that isn't build/ci |
| `revert` | Revert a previous commit |

## Scopes (suggested — not enforced)

Pick the most specific layer the change touches. Omit the scope if it spans multiple layers.

- `engine` — Unity engine reference, version pins
- `bridge` — Unity ↔ JS ↔ Flutter bridge code
- `ime` — Korean IME overlay / input handling
- `ai` — Gemini client, prompt engineering, evaluator
- `game` — core gameplay (action executor, state machine, orders)
- `ui` — HUD, menus, screens (Unity UI Toolkit / UGUI)
- `shell` — Flutter app shell, Riverpod providers
- `asset` — sprites, audio, config data
- `spec` — GDD, game-concept, design documents
- `adr` — architecture decision records
- `deploy` — Vercel config, APK build pipeline

## Examples

```
feat(bridge): implement Unity→JS round-end message channel

Wires UnityBridge.cs SendToWebView into WebGLBridge.jslib and forwards
to the Flutter JavaScriptChannel. Enables Riverpod result log updates.

Refs: design/gdd/game-concept.md §6.1
```

```
docs(spec): add Day One Chef game concept v1

Covers core loop, 5 orders, bridge architecture, 14-day timeline.

Refs: design/gdd/game-concept.md
```

```
fix(ime): prevent hangul composition loss when focus returns to canvas

Refs: ADR-0001 Korean IME strategy
```

```
chore: configure Unity 6.3 LTS and project context

Pins engine, populates technical preferences, repoints @ imports.
```

## Breaking changes

Add `!` after type/scope and/or `BREAKING CHANGE:` footer:

```
refactor(bridge)!: change round_end payload schema

BREAKING CHANGE: payload.success is now payload.result.success.
Flutter-side Riverpod notifier must be updated.
```

## Language Policy

- **Commit messages**: English. Conventional Commits `type(scope):` keywords,
  `BREAKING CHANGE`, `Refs:`, `Closes:` footers are all English by spec.
  Keeps the log greppable and tool-friendly (`/changelog`, release notes,
  third-party parsers).
- **Pull request title and body**: Korean (한국어). PRs are human-readable
  context for review discussions and stay within this repo — use the team's
  working language. Section headings may stay English (`## Summary`, `## Test plan`)
  for tool compatibility, but content under them is Korean.
- **Code comments, identifiers, file names**: English. Already covered by
  naming conventions in `technical-preferences.md`.
- **Design docs, GDD, README**: Korean primary, short English tagline allowed
  at top for discoverability.

### PR template example

```markdown
## Summary
- 변경사항 요약 1
- 변경사항 요약 2

## 배경 / 맥락
왜 이 변경이 필요했는지, 관련 GDD/ADR 섹션 링크

## Test plan
- [ ] 검증 항목 1
- [ ] 검증 항목 2

## 관련 문서
- design/gdd/...
- docs/architecture/ADR-XXXX-...
```
- **Verification-driven development**: Write tests first when adding gameplay systems.
  For UI changes, verify with screenshots. Compare expected output to actual output
  before marking work complete. Every implementation should have a way to prove it works.

# Design Document Standards

- All design docs use Markdown
- Each mechanic has a dedicated document in `design/gdd/`
- Documents must include these 8 required sections:
  1. **Overview** -- one-paragraph summary
  2. **Player Fantasy** -- intended feeling and experience
  3. **Detailed Rules** -- unambiguous mechanics
  4. **Formulas** -- all math defined with variables
  5. **Edge Cases** -- unusual situations handled
  6. **Dependencies** -- other systems listed
  7. **Tuning Knobs** -- configurable values identified
  8. **Acceptance Criteria** -- testable success conditions
- Balance values must link to their source formula or rationale

# Testing Standards

## Test Evidence by Story Type

All stories must have appropriate test evidence before they can be marked Done:

| Story Type | Required Evidence | Location | Gate Level |
|---|---|---|---|
| **Logic** (formulas, AI, state machines) | Automated unit test — must pass | `tests/unit/[system]/` | BLOCKING |
| **Integration** (multi-system) | Integration test OR documented playtest | `tests/integration/[system]/` | BLOCKING |
| **Visual/Feel** (animation, VFX, feel) | Screenshot + lead sign-off | `production/qa/evidence/` | ADVISORY |
| **UI** (menus, HUD, screens) | Manual walkthrough doc OR interaction test | `production/qa/evidence/` | ADVISORY |
| **Config/Data** (balance tuning) | Smoke check pass | `production/qa/smoke-[date].md` | ADVISORY |

## Automated Test Rules

- **Naming**: `[system]_[feature]_test.[ext]` for files; `test_[scenario]_[expected]` for functions
- **Determinism**: Tests must produce the same result every run — no random seeds, no time-dependent assertions
- **Isolation**: Each test sets up and tears down its own state; tests must not depend on execution order
- **No hardcoded data**: Test fixtures use constant files or factory functions, not inline magic numbers
  (exception: boundary value tests where the exact number IS the point)
- **Independence**: Unit tests do not call external APIs, databases, or file I/O — use dependency injection

## What NOT to Automate

- Visual fidelity (shader output, VFX appearance, animation curves)
- "Feel" qualities (input responsiveness, perceived weight, timing)
- Platform-specific rendering (test on target hardware, not headlessly)
- Full gameplay sessions (covered by playtesting, not automation)

## CI/CD Rules

- Automated test suite runs on every push to main and every PR
- No merge if tests fail — tests are a blocking gate in CI
- Never disable or skip failing tests to make CI pass — fix the underlying issue
- Engine-specific CI commands:
  - **Godot**: `godot --headless --script tests/gdunit4_runner.gd`
  - **Unity**: `game-ci/unity-test-runner@v4` (GitHub Actions)
  - **Unreal**: headless runner with `-nullrhi` flag
