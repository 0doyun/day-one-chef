# Technical Preferences

<!-- Populated by /setup-engine. Updated as the user makes decisions throughout development. -->
<!-- All agents reference this file for project-specific standards and conventions. -->

## Engine & Language

- **Engine**: Unity 6.3 LTS
- **Language**: C# (.NET Standard 2.1, Unity's IL2CPP for WebGL)
- **Rendering**: URP (Universal Render Pipeline) — 2D renderer, WebGL-friendly
- **Physics**: Unity built-in (2D physics — gameplay is top-down kitchen, no 3D physics needed)

## Input & Platform

<!-- Written by /setup-engine. Read by /ux-design, /ux-review, /test-setup, /team-ui, and /dev-story -->
<!-- to scope interaction specs, test helpers, and implementation to the correct input methods. -->

- **Target Platforms**: Web (Vercel) + Mobile (Flutter `webview_flutter`, iOS WKWebView)
- **Input Methods**: Keyboard (Korean text input, primary), Touch (mobile soft keyboard)
- **Primary Input**: Keyboard — single-line Korean text instruction (≤80 chars per round)
- **Gamepad Support**: None
- **Touch Support**: Full (mobile)
- **Platform Notes**:
  - **Korean IME on Unity WebGL is a known-hard problem** — Unity's `TMP_InputField` drops in-composition hangul. Must use HTML `<input>` overlay + `.jslib` bridge to route native browser IME into Unity.
  - Bidirectional bridge (Unity ↔ JS ↔ Flutter Riverpod) is a primary architectural surface — all cross-boundary state should flow through it rather than shortcuts.
  - Unity WebGL is not officially supported on mobile browsers by Unity — mobile path is via Flutter `webview_flutter` + WKWebView, not bare mobile browser.
  - *Dev note*: iOS Simulator Korean IME testing requires disabling "I/O → Keyboard → Connect Hardware Keyboard" and adding the Korean keyboard in the simulated iOS Settings; the Mac hardware keyboard otherwise bypasses the IME and hides composition bugs.

## Naming Conventions

- **Classes**: PascalCase (e.g., `ChefActionExecutor`, `OrderEvaluator`)
- **Public fields/properties**: PascalCase (e.g., `CurrentOrder`, `MoveSpeed`)
- **Private fields**: `_camelCase` (e.g., `_currentHealth`, `_isGrounded`)
- **Methods**: PascalCase (e.g., `ExecuteAction()`, `EvaluateResult()`)
- **Files**: PascalCase matching class (e.g., `ChefActionExecutor.cs`)
- **Scenes**: PascalCase (e.g., `MainKitchen.unity`)
- **Prefabs**: PascalCase (e.g., `Chef.prefab`, `Customer.prefab`)
- **Constants**: PascalCase for C# const, UPPER_SNAKE_CASE for static readonly config keys
- **ScriptableObjects**: PascalCase ending in `SO` or `Config` (e.g., `IngredientSO`, `OrderConfig`)

## Performance Budgets

- **Target Framerate**: 60fps (desktop web), 30fps floor (mobile WKWebView)
- **Frame Budget**: 16.6ms desktop, 33.3ms mobile fallback
- **Draw Calls**: ≤ 100 per frame (WebGL + 2D — mostly sprite batching via SRP Batcher)
- **Memory Ceiling**: Unity WebGL build heap ≤ 256MB (browser tab friendly); mobile WebView process ≤ 512MB total
- **Build Size**: WebGL initial download ≤ 15MB gzipped (Brotli preferred)
- **Gemini Latency Budget**: p95 ≤ 4s per call end-to-end (show streaming monologue to hide it); 8s hard timeout with retry-once + fallback UI

## Testing

- **Framework**: Unity Test Framework (NUnit-based) — EditMode for pure C# logic, PlayMode for scene/bridge integration
- **Minimum Coverage**: Not enforced as a number — focus on critical-path tests for action executor, bridge message codec, and Gemini JSON parser
- **Required Tests**:
  - Action sequence executor (pickup → cook → assemble state transitions)
  - Bridge message codec (Unity ↔ JS JSON round-trip)
  - Gemini response JSON schema validation + graceful degradation on malformed output
  - Order evaluation (ground-truth event log construction)

## Forbidden Patterns

<!-- Add patterns that should never appear in this project's codebase -->
- **No `Find()` / `FindObjectOfType()` / `SendMessage()`** in runtime code — inject dependencies or use events
- **No `GetComponent<>()` inside `Update()`** — cache in `Awake()`
- **No `Resources.Load()`** — use Addressables
- **No legacy `Input.GetKey()`** — use the new Input System package
- **No direct `public` serialized fields** — always `[SerializeField] private` with property accessors when needed
- **No allocation in hot paths** (Update, action-tick loops) — no LINQ, no string concat, no new List<>
- **No bypassing the Flutter ↔ Unity bridge** — all cross-boundary data (round results, reset commands) must flow through the documented bridge channels
- **No hardcoded Korean strings in code** — all user-facing text in `StringTable` ScriptableObjects for future i18n sanity

## Allowed Libraries / Addons

<!-- Add approved third-party dependencies here. Only add when actively integrating. -->
- **Unity Input System** (`com.unity.inputsystem`) — new input system required
- **Addressables** (`com.unity.addressables`) — asset loading
- **TextMesh Pro** (built-in) — UI text rendering
- **UnityWebRequest** (built-in) — Gemini API calls
- **Newtonsoft.Json for Unity** (`com.unity.nuget.newtonsoft-json`) — robust JSON handling for Gemini structured output
- *(Flutter side, tracked separately in `pubspec.yaml`)*: `webview_flutter`, `flutter_riverpod`

## Architecture Decisions Log

<!-- Quick reference linking to full ADRs in docs/architecture/ -->
- [ADR-0001](../../docs/architecture/ADR-0001-korean-ime-strategy.md) — Korean IME Strategy for Unity WebGL (Accepted 2026-04-24) — kou-yeung/WebGLInput adopted after Phase 1 macOS Chrome + Safari PASS
- [ADR-0003](../../docs/architecture/ADR-0003-gemini-call-architecture.md) — Gemini Call Architecture (Proposed 2026-04-24) — direct-from-Unity client behind `IGeminiClient` for Day 5, Flutter-proxy implementation swaps in on Day 8–9
- [ADR-0004](../../docs/architecture/ADR-0004-action-executor-model.md) — Chef Action Executor Model (Proposed 2026-04-24) — state-machine with per-verb handlers; event log shape locked as Day 11 evaluator contract
- [ADR-0002](../../docs/architecture/ADR-0002-bridge-message-protocol.md) — Bridge Message Protocol (Proposed 2026-04-25) — flat JSON envelope via a single `FlutterBridge` JS channel + `BridgeReceiver` Unity GameObject

**Remaining ADRs to author:**
5. **Evaluator ground-truth contract** — what data Unity collects per round for Gemini evaluation (includes event log for order-sensitive recipes like 계란찜)

## Engine Specialists

<!-- Written by /setup-engine when engine is configured. -->
<!-- Read by /code-review, /architecture-decision, /architecture-review, and team skills -->
<!-- to know which specialist to spawn for engine-specific validation. -->

- **Primary**: unity-specialist
- **Language/Code Specialist**: unity-specialist (C# review — primary covers it)
- **Shader Specialist**: unity-shader-specialist (Shader Graph, HLSL, URP/HDRP materials)
- **UI Specialist**: unity-ui-specialist (UI Toolkit UXML/USS, UGUI Canvas, runtime UI)
- **Additional Specialists**: unity-dots-specialist (ECS, Jobs system, Burst compiler — unlikely needed for this scope), unity-addressables-specialist (asset loading, memory management, content catalogs)
- **Routing Notes**: Invoke primary for architecture and general C# code review. Invoke UI specialist for all HUD/order display/input field implementation. Invoke Addressables specialist for asset loading strategy. DOTS specialist likely unused — scope is small.

### File Extension Routing

<!-- Skills use this table to select the right specialist per file type. -->
<!-- If a row says [TO BE CONFIGURED], fall back to Primary for that file type. -->

| File Extension / Type | Specialist to Spawn |
|-----------------------|---------------------|
| Game code (.cs files) | unity-specialist |
| Shader / material files (.shader, .shadergraph, .mat) | unity-shader-specialist |
| UI / screen files (.uxml, .uss, Canvas prefabs) | unity-ui-specialist |
| Scene / prefab / level files (.unity, .prefab) | unity-specialist |
| Native extension / plugin files (.dll, native plugins, .jslib) | unity-specialist |
| General architecture review | unity-specialist |
