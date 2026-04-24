# Phase 1 IME Evaluation Protocol

Practical test script for ADR-0001 Phase 1 (OSS-first). Follow this in order. Time budget: **~2 hours**.

> Before starting, confirm the branch containing the `kou-yeung/WebGLInput` dependency (`game/Packages/manifest.json` entry `com.github.kou-yeung`) is checked out and Unity is closed.

---

## Prerequisites

- [ ] Unity 6.3 LTS (`6000.4.3f1`) installed via Unity Hub (confirmed in `docs/engine-reference/unity/VERSION.md`)
- [ ] Active internet connection (Unity will download the UPM Git dependency on first open)
- [ ] Chrome, Safari, Firefox installed on macOS for the desktop browser matrix
- [ ] Xcode + iOS Simulator installed for the iOS WKWebView leg (`xcrun simctl list devices` shows at least one iOS 17+ simulator)
- [ ] Korean 2-Set (두벌식) IME available in macOS System Settings → Keyboard → Input Sources
- [ ] (For Simulator) Korean keyboard will be added to the simulated iOS device in Step 4 below

---

## Step 1 — UPM import

1. Open Unity Hub → **Add project from disk** → select `day-one-chef/game/`.
2. Unity opens and begins importing. The Package Manager downloads `kou-yeung/WebGLInput` into `Packages/` (UPM cache, not under `Assets/`). First-time import takes 30–60 seconds.
3. Verify import: **Window → Package Manager → Packages: In Project** should list **WebGLInput** (`com.github.kou-yeung`, version 1.4.2 or later).
4. Verify `game/Packages/packages-lock.json` gained a new entry pinning the resolved commit hash. Commit this file change to the branch — it's how we guarantee reproducible builds later.

**Fail fast:** If Unity reports "Failed to resolve dependency" → check internet / GitHub reachability / manifest.json syntax. Do not continue.

---

## Step 2 — Probe scene setup

1. In the Unity Project window, right-click `Assets/Scenes/` → **Create → Scene** → name it **`OSS_IME_Probe.unity`**. Open it.
2. In the Hierarchy, right-click → **UI → Input Field – TextMeshPro**. Unity will prompt to import TMP Essentials — accept.
3. Select the created **InputField (TMP)** GameObject. In the Inspector, click **Add Component** → type **`WebGLInput`** → add it.
4. In the same Inspector, configure the TMP_InputField:
   - **Character Limit**: `80`
   - **Content Type**: `Standard`
   - **Line Type**: `Single Line`
   - **Placeholder**: set text to `"예: 그릇에 계란 먼저 깨고 그 다음 물을 섞어서 쪄줘"`
5. Save the scene. File → Build Settings → **Add Open Scenes**. Make `OSS_IME_Probe` the first scene in the build list.

---

## Step 3 — Desktop browser matrix

Switch Build Target to **WebGL**: File → Build Settings → WebGL → **Switch Platform** (takes ~1 minute on first switch).

Build: **Build Settings → Build** → output directory `game/Build/webgl-ime-probe/`.

Serve locally:
```bash
cd game/Build/webgl-ime-probe
python3 -m http.server 8080
```

Open `http://localhost:8080/` in each browser and type the full test string below. Verify zero dropped or duplicated jamo in the input field.

**Test string:** `안녕하세요 계란찜 먼저 풀어서 물 넣고 쪄줘`

| Browser | Typed | Rendered | Dropped jamo? | Focus loss on click-outside? | PASS / FAIL |
|---------|-------|----------|---------------|------------------------------|-------------|
| Chrome (macOS) | | | | | |
| Safari (macOS) | | | | | |
| Firefox (macOS) | | | | | |

Also verify:

- [ ] **Tab/Shift+Tab** moves focus (expected default; only breaks if `WEBGLINPUT_TAB` is defined in Scripting Defines)
- [ ] **Enter key** triggers `onSubmit` (watch the Console for TMP_InputField default log, or temporarily attach a debug listener)
- [ ] **Character limit**: attempting to paste > 80 chars clips to 80
- [ ] **Backspace during composition** removes the in-progress jamo correctly

---

## Step 4 — iOS WKWebView matrix

This is the highest-risk test because `kou-yeung/WebGLInput`'s mobile support is marked "Experimental".

1. Open Xcode → **Open Developer Tool → Simulator**. Boot an iPhone 15 (or newer) with iOS 17+.
2. In the simulated iOS:
   - Settings → General → Keyboard → **Hardware Keyboard** → turn OFF (otherwise Mac keyboard bypasses IME)
   - Settings → General → Keyboard → Keyboards → **Add New Keyboard** → **Korean → 2 Set**
3. Open Safari **inside the Simulator** (not macOS Safari). Navigate to `http://localhost:8080/` (Simulator uses the host's network).
4. Tap the input field → soft keyboard appears → switch to Korean 2 Set by pressing the globe icon → type the test string.
5. Record the result.

| Target | Typed | Rendered | Soft keyboard shows? | Dropped jamo? | PASS / FAIL |
|--------|-------|----------|----------------------|---------------|-------------|
| iOS Simulator + Safari (standalone) | | | | | |

**Note:** Testing inside `webview_flutter` specifically (our actual delivery target) requires a Flutter shell that does not exist yet. For Phase 1 purposes, iOS Simulator Safari WKWebView is a *reasonable proxy* — both use the same WKWebView engine. If Phase 1 passes here, the Flutter shell integration is treated as a low-risk follow-up rather than a blocker. Full Flutter shell coverage moves to Day 8–9 with the bridge implementation.

---

## Step 5 — Verdict

Count PASS rows.

- **All 4 PASS** → Phase 1 succeeds.
  1. Update ADR-0001 `Status: Proposed` → `Status: Accepted`
  2. Update ADR `Last Verified` to today's date
  3. Write `Phase1WebGLInputAdapter.cs` under `game/Assets/Scripts/Bridge/` that implements `IKoreanImeBridge` by wrapping the probe's TMP_InputField
  4. Delete the throwaway probe scene
  5. Proceed to Day 3 — Unity prototype scene work

- **3 PASS / 1 FAIL** → Note which platform failed. Common fixes:
  - Safari-specific composition event ordering → open an issue upstream and decide: ship with Chrome/Firefox fallback, or escalate to Phase 2
  - iOS Simulator hardware keyboard interference → re-run after confirming hardware keyboard is OFF
  - If the fix exists and takes < 30 min → fix it, re-run. Otherwise treat as 2 FAIL.

- **2+ FAIL** → Before escalating to Phase 2, try the next Phase 1 fallback candidate from ADR Related section:
  1. `unity3d-jp/WebGLNativeInputField` (switch TMP → UGUI InputField for the probe, 30 min)
  2. `decentraland/webgl-ime-input` (30 min)
  If none of the three pass, **escalate to Phase 2**:
  - Check out a new branch `feat/ime-phase-2-diy`
  - Follow ADR-0001 Migration Plan §Phase 2 steps 7–11
  - Budget for one Day-2 rebuild + add one day to the 2-day spec buffer

---

## Evidence to capture

Save the following under `production/qa/evidence/phase-1-ime/`:

- [ ] Screen recording of the test string being typed in each browser (3 files)
- [ ] Screen recording of iOS Simulator test
- [ ] Filled-out test matrix tables from Steps 3 and 4
- [ ] A one-paragraph verdict note: "Phase 1 PASSED / FAILED because …"

The evidence block feeds the eventual ADR `Accepted` status update and doubles as content for the Day 14 demo reel.
