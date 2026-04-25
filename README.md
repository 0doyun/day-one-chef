# Day One Chef

> 오늘 첫 출근한 신입 인턴 요리사에게 **한 줄 한국어 지시**로 요리를 가르치는 미니게임. 인턴은 사수의 말을 문자 그대로 받아쳐서 — 빠진 단계는 그냥 빠뜨린 채 — 우렁차게 실행한다.

*A mini-game where you teach an over-eager rookie AI chef to cook with a single line of Korean instruction. The chef shouts "넵!" and executes exactly what you said — no inferred steps, no skipped literalness — which is where the comedy comes from.*

---

## 한 줄 게임 소개

손님이 메뉴를 외치면(`햄버거 주세요!`) 플레이어는 한국어 한 줄로 인턴에게 조리법을 가르친다. 인턴은 지시를 verb 단위 행동으로 분해해서 주방에서 시연하고, 손님은 결과를 보고 한 줄 평을 던진다.

**Core loop:** 주문 등장 → 한국어 지시 (≤80자) → Gemini call #1이 행동 JSON 생성 → Unity가 시연 + 이벤트 로그 기록 → Gemini call #2가 손님 시점에서 판정 → Flutter 우측 패널에 결과 누적 → 5라운드 후 세션 결산.

**5라운드 코스:** 토스트 → 계란찜 → 샐러드 → 감자튀김 → 햄버거. 사람이라면 자연스럽게 빠뜨리지 않을 단계들 — "씻기", "껍질 깨기", "썰기" — 을 인턴은 사수가 말 안 하면 그냥 생략한다.

---

## 아키텍처

```
┌─────────────────────────────────────────────────────────────┐
│                       Mobile (Android APK / iOS)            │
│  ┌───────────────────────────────────────────────────────┐  │
│  │        Flutter Shell (Riverpod)                       │  │
│  │  ┌─────────────────────────────────────────────────┐  │  │
│  │  │   webview_flutter                               │  │  │
│  │  │                ◄──── JSChannel ────►            │  │  │
│  │  │   ┌───────────────────────────────────────────┐ │  │  │
│  │  │   │     Unity 6.3 LTS WebGL Build             │ │  │  │
│  │  │   │   ┌──────────────┐    ┌────────────────┐  │ │  │  │
│  │  │   │   │  C# Game     │    │  HTML input    │  │ │  │  │
│  │  │   │   │  (URP 2D)    │◄──►│  overlay       │  │ │  │  │
│  │  │   │   │              │    │  (Korean IME)  │  │ │  │  │
│  │  │   │   └──────┬───────┘    └────────────────┘  │ │  │  │
│  │  │   │          │ .jslib bridge                  │ │  │  │
│  │  │   │          ▼                                │ │  │  │
│  │  │   │   FlutterBridge JS channel ──┐            │ │  │  │
│  │  │   └───────────────────────────────┼──────────┘ │  │  │
│  │  └─────────────────────────────────────┼─────────────┘  │  │
│  │                                        ▼                │  │
│  │                            Gemini 2.5 Flash             │  │
│  │                            (Flutter shelf 프록시)        │  │
│  └───────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘

웹 빌드(Vercel):  Unity WebGL  ◄──JS──►  브라우저 단독 (Flutter 셸 없음)
```

**핵심 설계 원칙:** Flutter ↔ Unity WebView 양방향 브릿지가 이 프로젝트의 1차 설계 면. 라운드 결과 / 세션 종료 / 리셋 명령 모두 단일 `FlutterBridge` JS 채널 + `BridgeReceiver` Unity GameObject 한 쌍을 통해서만 흐른다 (ADR-0002).

---

## 기술 스택

| 레이어 | 기술 |
|---|---|
| 게임 엔진 | **Unity 6.3 LTS** + C# (.NET Standard 2.1, IL2CPP) |
| 렌더링 | URP 2D Renderer, ortho camera |
| 앱 셸 | **Flutter** + Riverpod (`flutter_riverpod`) |
| WebView | `webview_flutter` (iOS WKWebView / Android WebView) |
| AI | **Gemini 2.5 Flash** (`thinkingBudget=0`), 호출 2회 (action / evaluator) |
| 로컬 프록시 | Flutter `shelf` 서버가 Gemini 호출 + Unity 빌드 정적 호스팅 |
| 한글 입력 | `kou-yeung/WebGLInput` 어댑터 + HTML `<input>` 오버레이 (ADR-0001) |
| JSON | Newtonsoft.Json for Unity |
| 배포 | Unity WebGL → Vercel (Brotli 정적), Flutter → Android APK |

---

## 리포 구조

```
day-one-chef/
├── CLAUDE.md                       # 프로젝트 설정 (엔진 핀, 표준, 조정 규칙)
├── README.md
├── game/                           # Unity 6.3 LTS 프로젝트 (URP 2D)
│   └── Assets/
│       ├── Scripts/Gameplay/
│       │   ├── AI/                 # Gemini 클라이언트 + 프롬프트 빌더 + 평가자
│       │   ├── Bridge/             # Unity ↔ JS 브릿지
│       │   ├── Data/               # ScriptableObject 게임 데이터
│       │   ├── UI/                 # 주방 HUD
│       │   ├── ActionExecutor.cs   # verb 단위 액션 실행기 + 이벤트 로그
│       │   ├── GameRound.cs        # 라운드 상태머신
│       │   ├── KitchenState.cs     # 재료/스테이션 슬롯 상태
│       │   └── ChefAnimator.cs     # 셰프 이동·생각·들고있는 재료
│       └── Editor/                 # 씬 자동 구성 + 데이터 생성기
├── app/                            # Flutter 셸 (Riverpod)
│   └── lib/src/
│       ├── shell/
│       │   ├── flutter_bridge.dart       # JSChannel 양방향 코덱
│       │   ├── unity_server.dart         # 로컬 shelf 서버 (Unity 정적 호스팅)
│       │   ├── gemini_proxy.dart         # /gemini → Google AI 프록시
│       │   ├── unity_host_page.dart      # WebView + 우측 패널 레이아웃
│       │   ├── recipe_panel.dart         # 현재 주문 카드
│       │   ├── result_log_panel.dart     # 라운드별 손님 한 마디
│       │   ├── punchline_flash.dart      # 결과 직후 토스트 플래시
│       │   └── session_end_dialog.dart   # 5라운드 후 결산 모달
│       └── game/                         # 브릿지 메시지 모델 + Riverpod providers
├── design/gdd/game-concept.md      # 전체 GDD (§1~16)
├── docs/
│   ├── architecture/               # ADR-0001 ~ 0005
│   └── engine-reference/unity/     # Unity 6.3 LTS 레퍼런스 스냅샷
└── scripts/sync-unity-to-app.sh    # Unity 빌드 → Flutter 에셋 동기화
```

**Unity Hub에서 열기:** "Add project from disk" → `day-one-chef/game/` 선택 (리포 루트 아님).

---

## 핵심 코드 파일

브릿지와 AI 호출 분기가 이 프로젝트의 1차 볼거리이므로 직접 링크한다.

**브릿지 (Unity ↔ Flutter):**
- `game/Assets/Scripts/Gameplay/Bridge/UnityBridge.cs` — Unity → JS 송신
- `game/Assets/Scripts/Gameplay/Bridge/BridgeIncoming.cs` — JS → Unity 수신 게이트웨이
- `game/Assets/Scripts/Gameplay/Bridge/BridgeMessage.cs` — 평탄한 JSON 엔벨로프 정의
- `app/lib/src/shell/flutter_bridge.dart` — Flutter 측 JSChannel 코덱

**AI 호출 (Gemini 2회):**
- `game/Assets/Scripts/Gameplay/AI/GeminiPromptBuilder.cs` — call #1 시스템 프롬프트 (신입 인턴 톤)
- `game/Assets/Scripts/Gameplay/AI/EvaluatorPromptBuilder.cs` — call #2 손님 시점 판정 프롬프트
- `game/Assets/Scripts/Gameplay/AI/GeminiClient.cs` / `GeminiRoundEvaluator.cs` — UnityWebRequest 클라이언트
- `app/lib/src/shell/gemini_proxy.dart` — API 키를 셸에 격리하기 위한 로컬 프록시

**게임 코어:**
- `game/Assets/Scripts/Gameplay/ActionExecutor.cs` — verb (pickup/cook/chop/crack/mix/assemble/serve/move) 실행 + 시간순 이벤트 로그
- `game/Assets/Scripts/Gameplay/GameRound.cs` — 라운드 상태머신
- `game/Assets/Scripts/Gameplay/Data/Recipe.cs` — `ProcedureNotes` 가 evaluator 의 핵심 단서

---

## 설계 문서

- 전체 스펙: [design/gdd/game-concept.md](design/gdd/game-concept.md)
- ADR-0001 — Korean IME Strategy ([docs/architecture/ADR-0001-korean-ime-strategy.md](docs/architecture/ADR-0001-korean-ime-strategy.md))
- ADR-0002 — Bridge Message Protocol ([docs/architecture/ADR-0002-bridge-message-protocol.md](docs/architecture/ADR-0002-bridge-message-protocol.md))
- ADR-0003 — Gemini Call Architecture ([docs/architecture/ADR-0003-gemini-call-architecture.md](docs/architecture/ADR-0003-gemini-call-architecture.md))
- ADR-0004 — Action Executor Model ([docs/architecture/ADR-0004-action-executor-model.md](docs/architecture/ADR-0004-action-executor-model.md))
- ADR-0005 — Evaluator Ground-Truth Contract ([docs/architecture/ADR-0005-evaluator-ground-truth-contract.md](docs/architecture/ADR-0005-evaluator-ground-truth-contract.md))
- Unity 6.3 LTS 레퍼런스: [docs/engine-reference/unity/VERSION.md](docs/engine-reference/unity/VERSION.md)
- 기술 선호도: [.claude/docs/technical-preferences.md](.claude/docs/technical-preferences.md)

---

## 현재 상태

**핵심 루프 완료, 폴리시 단계.** 5라운드 코스가 끝까지 돌고, Gemini 2회 호출 + 브릿지 + 결산 모달까지 연결됨.

이미 들어간 것:
- Unity ↔ Flutter `FlutterBridge` 양방향 브릿지 (`round_end` / `session_end` / `order_present` / `console_log`)
- Gemini call #1 (action) + call #2 (evaluator) — 인턴 페르소나 + 손님 시점 코미디 톤
- 액션 실행기 (8 verb, 0.6초 틱, 이벤트 로그)
- 5라운드 코스 + `Recipe.ProcedureNotes` 로 오믈렛/계란찜류 구분
- Flutter 우측 패널: 현재 주문 / 라운드 결과 로그 / 펀치라인 플래시 / 세션 결산 모달
- Brotli 정적 WebGL 빌드 + 커스텀 WebGL 템플릿
- 픽셀 아트 셰프 / 손님 / 스테이션, 셰프 이동·생각 모션, 손님 말풍선

남은 폴리시 영역: 에셋 / 사운드 / 마지막 톤 다듬기 / 모바일 IME 검증.

---

## 빌드 / 실행

**Unity WebGL 로컬 미리보기:**
```bash
# Unity Editor에서 game/ 프로젝트 열고 Build Settings → WebGL → Build
# 또는 CI 빌드 산출물을 app/assets/unity_build/ 에 복사 (scripts/sync-unity-to-app.sh)
```

**Flutter 셸 (Android):**
```bash
cd app
flutter pub get
flutter run -d <android_device>
```
셸은 부팅 시 로컬 `shelf` 서버를 띄워 Unity WebGL 빌드를 정적 서빙하고, `/gemini` 경로로 Google AI API를 프록시한다. API 키는 `--dart-define=GEMINI_API_KEY=...` 로 주입.

**Vercel 웹 빌드:**
Unity WebGL 산출물 (Brotli 압축) 만 배포. 셸 없이 동작하지만 IME 폴백 + 브릿지 stub 포함.

---

## License

MIT. 템플릿 저작권은 [Claude Code Game Studios](https://github.com/Donchitos/Claude-Code-Game-Studios) 프로젝트에 귀속.
