# Day One Chef

> 오늘 첫 출근한 초보 AI 요리사에게 **한 줄 한국어 지시**로 요리 절차를 가르치는 미니게임. 지시의 정밀도에 따라 성공/실패가 갈린다.

*A mini-game where you teach a rookie AI chef to cook with a single line of Korean instruction per order. The chef — confident but clueless — executes exactly what you say, no more, no less.*

---

## 🎮 한 줄 게임 소개

플레이어는 "치즈버거"라는 주문명만 보고, 냉장고에 놓인 원시 재료들을 어떻게 조리해서 완성할지 셰프에게 한 줄로 가르친다. 셰프는 지시를 문자 그대로 수행하며 누락된 절차는 생략한다 — 계란찜을 시킬 때 "계란 먼저, 그 다음 물"이라고 안 하면 셰프는 당당하게 물부터 섞는다.

**Core loop:** 주문 등장 → 한국어 지시 입력 (≤80자) → Gemini가 지시를 행동 JSON으로 변환 → Unity 애니메이션으로 시연 → Gemini가 결과 평가 → 다음 주문.

---

## 🏗 아키텍처

```
┌─────────────────────────────────────────────────────────────┐
│                       Mobile (iOS)                           │
│  ┌───────────────────────────────────────────────────────┐  │
│  │        Flutter Shell (Riverpod)                        │  │
│  │  ┌─────────────────────────────────────────────────┐  │  │
│  │  │   webview_flutter (WKWebView)                   │  │  │
│  │  │                ◄──── JSChannel ────►            │  │  │
│  │  │   ┌───────────────────────────────────────────┐ │  │  │
│  │  │   │     Unity WebGL Build                     │ │  │  │
│  │  │   │   ┌──────────────┐    ┌────────────────┐  │ │  │  │
│  │  │   │   │  C# Game     │    │  HTML input    │  │ │  │  │
│  │  │   │   │  (URP 2D)    │◄──►│  overlay       │  │ │  │  │
│  │  │   │   │              │    │  (Korean IME)  │  │ │  │  │
│  │  │   │   └──────┬───────┘    └────────────────┘  │ │  │  │
│  │  │   │          │ .jslib bridge                  │ │  │  │
│  │  │   │          ▼                                │ │  │  │
│  │  │   │   UnityWebRequest ──► Gemini 2.5 Flash API│ │  │  │
│  │  │   └───────────────────────────────────────────┘ │  │  │
│  │  └─────────────────────────────────────────────────┘  │  │
│  └───────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘

Public deploy (Vercel):  Unity WebGL  ◄──JS──►  (browser only, no Flutter shell)
```

**핵심 설계 원칙:** Flutter ↔ Unity WebView 양방향 브릿지를 통한 깔끔한 메시지 교환이 이 프로젝트의 1차 설계 면. 편의를 위해 브릿지를 우회하지 않는다.

---

## 🧱 기술 스택

| 레이어 | 기술 |
|---|---|
| 게임 엔진 | **Unity 6.3 LTS** + C# (.NET Standard 2.1, IL2CPP) |
| 렌더링 | URP (Universal Render Pipeline) 2D |
| 앱 셸 | **Flutter** + Riverpod |
| WebView | `webview_flutter` (iOS WKWebView) |
| AI | **Gemini 2.5 Flash API** (무료 티어) |
| 한글 입력 | HTML `<input>` overlay + `.jslib` 브릿지 (Unity WebGL IME 우회) |
| JSON | Newtonsoft.Json for Unity |
| 배포 | Unity WebGL → Vercel |

---

## 🕹 플레이

- 🌐 **웹 빌드**: *(배포 예정 — Vercel URL)*
- 🎬 **시연 영상**: *(제작 예정 — 성공 모먼트 + 순서 실수 허세 모먼트 + 모바일 브릿지 동작)*

---

## 📂 프로젝트 구조

```
day-one-chef/
├── CLAUDE.md                    # 프로젝트 설정 (엔진, 표준, 조정 규칙)
├── design/gdd/
│   └── game-concept.md          # 게임 스펙 (전체 설계 문서)
├── docs/
│   ├── architecture/            # ADR (예정)
│   └── engine-reference/unity/  # Unity 6.3 LTS 레퍼런스 스냅샷
├── src/                         # Unity 게임 소스 (예정)
├── flutter_shell/               # Flutter 셸 (예정)
├── assets/                      # 게임 에셋 (예정)
├── tests/                       # EditMode / PlayMode 테스트 (예정)
└── docs/                        # 기술 문서
```

---

## 🔑 핵심 코드 파일 (구현 예정)

브릿지 구현이 이 프로젝트의 핵심 볼거리이므로 README에서 직접 링크한다:

- **Unity → JS 메시지 전송**: `src/Bridge/UnityBridge.cs` *(TODO)*
- **JSLib 브릿지**: `src/Bridge/WebGLBridge.jslib` *(TODO)*
- **Flutter JSChannel 리시버**: `flutter_shell/lib/bridge/unity_channel.dart` *(TODO)*
- **Korean IME 오버레이**: `src/Bridge/KoreanImeOverlay.jslib` *(TODO)*
- **Gemini 호출 레이어**: `src/AI/GeminiClient.cs` *(TODO)*

---

## 📋 설계 문서

- 전체 스펙: [design/gdd/game-concept.md](design/gdd/game-concept.md)
- Unity 6.3 LTS 레퍼런스: [docs/engine-reference/unity/VERSION.md](docs/engine-reference/unity/VERSION.md)
- 기술 선호도: [.claude/docs/technical-preferences.md](.claude/docs/technical-preferences.md)
- ADR (예정): `docs/architecture/` — 5건 예약됨 (IME 전략, 브릿지 프로토콜, Gemini 호출 아키텍처, 액션 실행기 모델, Evaluator 계약)

---

## 🚦 현재 상태

**Pre-production** — 게임 스펙 (§1~16) 확정, 엔진/기술 스택 확정, ADR 및 구현 착수 대기.

**14일 풀타임 + 2일 버퍼** 일정으로 Week 1 기반 / Week 2 브릿지 순서 진행 예정. Korean IME on Unity WebGL이 가장 큰 기술 리스크 — Day 1~2에 조기 프로토타이핑.

전체 일정은 [game-concept.md §7](design/gdd/game-concept.md) 참조.

---

## 🛠 개발 환경

이 리포는 [Claude Code Game Studios](https://github.com/Donchitos/Claude-Code-Game-Studios) 템플릿 기반으로 관리된다. 49개 전문 에이전트 + 72개 스킬을 Claude Code 세션에 구조로 주입해, 디자인/아키텍처/구현/QA를 역할 분리된 상태로 진행한다.

주요 워크플로우:
- `/setup-engine` — 엔진 핀 고정 (완료: Unity 6.3 LTS)
- `/map-systems` — 게임 콘셉트를 시스템 단위로 분해 *(예정)*
- `/architecture-decision` — ADR 저술 *(예정)*
- `/create-epics` → `/create-stories` → `/dev-story` — 구현 진행 *(예정)*

---

## 📜 License

MIT. 템플릿 저작권은 [Claude Code Game Studios](https://github.com/Donchitos/Claude-Code-Game-Studios) 프로젝트에 귀속.
