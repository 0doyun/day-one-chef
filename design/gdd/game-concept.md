# Day One Chef — Game Spec

**한 줄 요약:** 오늘 첫 출근한 초보 AI 요리사에게 한 줄 한국어 지시로 요리 절차를 가르치는 Unity WebGL 미니게임. 지시의 정밀도에 따라 성공/실패가 갈린다.

---

## 1. 게임 개요

### 1.1 장르
서양 패스트푸드 / 쿠킹 시뮬레이션 / 프롬프트 기반 조작

### 1.2 Core Loop (1 라운드 = 주문 1건)

1. 손님 등장 + 주문 표시 (재료 상태는 "원시 상태"로 배치됨)
2. 플레이어가 한 줄 한국어 지시 입력 (~80자)
3. Gemini 호출 #1: 지시 → 행동 JSON 시퀀스 + 셰프 독백(monologue)
4. Unity가 JSON actions 배열을 0.6초 간격으로 순차 애니메이션 실행하며 event_log에 기록
5. Gemini 호출 #2: 주문 + 최종 결과물 + event_log → `{success, reason}` 판정
6. 결과 표시 + 누적 로그 업데이트 (Flutter Riverpod)
7. 다음 주문

### 1.3 게임 난이도 설계

**재료는 항상 "원시 상태"로 제공됨** — 안익은 패티, 통상추, 썰지 않은 토마토 등
**레시피는 최종 형태만 표시** — "치즈버거"라고만 써있음
**플레이어가 절차를 유추해서 셰프에게 가르쳐야 함**
초보 셰프는 절차 지식이 없으므로 플레이어의 지시가 곧 유일한 가이드.

### 1.4 캐릭터 설정

**Day One Chef** — 오늘 첫 출근한 초보 AI 요리사. 열정은 넘치지만 아는 게 없다. 자존심 강해서 모르는 걸 인정하지 않고 허세 부리며 실행한다. 지시가 애매하면 당황하지 않은 척 엉뚱한 일을 벌인다.

**플레이어** — 사수 역할. 텍스트 지시로 Day One Chef를 가르치며 주문을 처리한다.

---

## 2. 주문 템플릿 (5종)

### 주문 1. 플레인 토스트 (쉬움 · 튜토리얼)
- 절차: 빵 → 굽기
- 예시 지시: `"빵 화구에 올려서 구워줘"`
- 학습 목표: 기본 조작 이해 (pickup → cook → serve)

### 주문 2. 샐러드 (쉬움)
- 절차: 상추 → 토마토 썰기 → 섞기
- 예시 지시: `"상추랑 토마토 썰어서 접시에 담아줘"`
- 학습 목표: 다중 재료 처리

### 주문 3. 치즈버거 (중간)
- 절차: 패티 굽기 → 빵(아래) + 패티 + 치즈 + 상추 + 빵(위)
- 예시 지시: `"패티 구운 다음 아래 빵에 패티 올리고 치즈 상추 올려서 빵으로 덮어"`
- 학습 목표: 조립 순서 명시

### 주문 4. 오믈렛 (중간)
- 절차: 계란 깨기 → 풀기 → 소금 → 버터 팬 → 부침 → 접기
- 예시 지시: `"계란 풀어서 팬에 붓고 반으로 접어줘"`
- 학습 목표: 다단계 조리 생략 추론

### 주문 5. 계란찜 (어려움 · 게임 하이라이트)
- 절차: **계란 먼저 그릇에** → 물 → 소금 → 찜
- 조리 스테이션: 화구 + `cook` verb의 `param: "steam"` 변형 (별도 찜기 스테이션 추가 안 함)
- 예시 지시: `"그릇에 계란 먼저 깨고 그 다음에 물을 섞어서 쪄줘"`
- 학습 목표: **순서 민감 지시** — "먼저", "그 다음" 같은 순서어 필수
- 허세 모먼트: 지시에 순서 없으면 셰프가 물 먼저 넣고 계란 나중에 → 실패 (event_log로 순서 검증)

---

## 3. 게임 구성요소

### 3.1 주방 스테이션 (5개)
1. **냉장고** — 재료 보관 (패티, 빵, 계란, 치즈, 상추, 토마토)
2. **도마** — 썰기 작업 (상추, 토마토, 양파 등)
3. **화구** — 굽기/부침/찜 작업 (`cook` verb의 `param`: `grill` | `fry` | `steam`)
4. **조립대** — 재료 조립 (버거, 토스트)
5. **카운터** — 손님에게 서빙

> 찜기는 별도 스테이션으로 만들지 않고 화구 + `steam` 파라미터로 흡수. 재료 스프라이트가 화구 위에 놓일 때 param에 따라 VFX/애니메이션만 다르게 재생.

### 3.2 재료 6종 + 상태 시스템
- **패티** (안익음 → 익음 → 탐)
- **빵** (일반 → 구움 → 탐)
- **치즈** (통째 → 슬라이스)
- **상추** (통째 → 씻음 → 썬 상태)
- **토마토** (통째 → 썬 상태)
- **계란** (껍질 → 깬 상태 → 풀림 → 익음)

각 재료는 `state` 필드로 현재 상태 추적 → Gemini 평가 시 정답 판정에 사용.

### 3.3 손님 (스프라이트 3종)
- 단순 스프라이트 차이만 (심심해하는 모습, 기다리는 모습, 화난 모습)
- 현재 주문을 말풍선으로 표시

---

## 4. AI 연동 (Gemini 2.5 Flash)

### 4.1 호출 #1 — 행동 산출 (스트리밍)

**입력:** 플레이어 지시 텍스트 + 게임 상태(재료 목록·스테이션·현재 주문)

**출력 JSON 스키마:**
```json
{
  "actions": [
    {
      "verb": "pickup|cook|chop|crack|mix|assemble|serve|move",
      "target": "<station|ingredient>",
      "param": "..."
    }
  ],
  "monologue": "셰프의 자신만만한 한마디 (타이핑 스트리밍)"
}
```

**가용한 verb 세트 (8종, JSON enum과 동일):**
- `pickup` — 재료/결과물 집기
- `cook` — 화구에서 굽기/부침/찜 (`param`: `grill` | `fry` | `steam`)
- `chop` — 도마에서 썰기
- `crack` — 계란 깨기
- `mix` — 그릇에 재료 섞기
- `assemble` — 조립대에서 재료 쌓기
- `serve` — 카운터로 서빙
- `move` — 위치 이동

**프롬프트 설계 원칙:**
- 셰프는 주어진 지시를 "문자 그대로" 실행
- 지시에 없는 단계는 생략 (이게 허세/실패 핵심)
- 순서어("먼저", "다음") 없으면 기본값(재료 나열 순)으로 실행
- monologue는 "Day One Chef"의 자신만만한 첫날 신입 톤으로 생성

### 4.2 호출 #2 — 결과 평가

**입력:**
- 원본 주문명
- 최종 결과물 객체 (Unity가 추적한 ground-truth: 재료 구성 + 각 재료의 최종 state)
- **event_log**: Unity가 기록한 타임스탬프 순 action 배열 — `[{verb, target, param, t}, ...]`

**출력 JSON 스키마:**
```json
{
  "success": true | false,
  "reason": "성공 또는 실패한 구체적 이유 (한국어, 1~2문장)"
}
```

**평가 규칙 (프롬프트에 명시):**
- `success: true` — 주문명의 핵심 재료·상태·순서 요구사항 충족
- `success: false` — 필수 재료 누락 / 상태 오류 / 순서 오류
- 순서 민감 주문(계란찜)은 `event_log`의 action 순서까지 검증 대상 — "계란 `crack` 이전에 물 `mix`"가 기록되면 순서 오류
- `reason` — 실패의 경우 "왜 실패했는지" 구체적 설명 (단순 오답 아닌 학습 피드백)

### 4.3 실패 모드 & 폴백

- **Gemini 타임아웃 (>8s)** → 재시도 1회 → 실패 시 "셰프가 얼어붙음" 연출 + 라운드 무효화, 다음 주문 제공
- **JSON 파싱 실패 / 스키마 불일치** → monologue만 표시 + 라운드 무효화
- **Free tier rate limit 초과** → Flutter UI에 쿨다운 배지 표시, 로컬 룰베이스 fallback 평가기(순수 상태/재료 매칭 기반) 1종 준비
- **유효하지 않은 verb / 존재하지 않는 재료** → Unity 실행 엔진이 silently skip + event_log에 `"skipped": true` 플래그 기록, `reason`에 "준비되지 않은 행동" 반영
- **Unity WebView 자체 크래시** → Flutter Riverpod가 이벤트 타임아웃 감지 → 사용자에게 "리셋" 버튼 노출 (Flutter→JS→Unity 브릿지로 게임 상태 초기화)

---

## 5. 기술 스택

| 레이어 | 기술 | 역할 |
|---|---|---|
| 앱 컨테이너 | Flutter + Riverpod | UI 셸, 상태 관리, WebView 호스팅 |
| WebView | webview_flutter | Unity WebGL 로드 |
| 게임 엔진 | Unity 6.3 LTS + C# + URP 2D | 게임 렌더링, 상태 머신, 애니메이션 |
| AI | Gemini 2.5 Flash API (무료티어) | 행동 산출 + 결과 평가 |
| 통신 | Unity ↔ JS ↔ Flutter Bridge | 양방향 메시지 |
| Korean IME | HTML `<input>` overlay + `.jslib` bridge | WebGL 한글 입력 조합 처리 |
| JSON | Newtonsoft.Json for Unity | Gemini 구조화 출력 파싱 |
| 배포 (웹) | Unity WebGL → Vercel | 브라우저 플레이 |
| 모바일 | Flutter (webview_flutter, iOS WKWebView) | 모바일 빌드 |

---

## 6. 브릿지 구현

### 6.1 Unity → JS → Flutter (결과 이벤트 전달)

**Unity C#:**
```csharp
[DllImport("__Internal")]
private static extern void SendToWebView(string json);

// 라운드 종료 시
SendToWebView(JsonConvert.SerializeObject(new {
  type = "round_end",
  payload = new { orderId, success, reason }
}));
```

**Flutter JavaScriptChannel:**
```dart
JavaScriptChannel(
  name: 'Native',
  onMessageReceived: (JavaScriptMessage msg) {
    final data = jsonDecode(msg.message);
    ref.read(resultsProvider.notifier).update(data);
  },
)
```

### 6.2 Flutter → JS → Unity (리셋/일시정지 제어)

**Flutter:**
```dart
await controller.runJavaScript('unityInstance.SendMessage("GameManager", "Reset");');
```

**Unity GameManager:**
```csharp
public void Reset() { /* 게임 상태 초기화 */ }
public void Pause(string pausedJson) { /* 일시정지 */ }
```

### 6.3 Riverpod 상태
- `gameResultsProvider` — 누적 성공/실패 로그
- `sessionProvider` — 현재 세션 메타데이터
- Flutter UI가 Unity 게임과 독립적으로 Material 디자인 SnackBar 등으로 결과 표시

### 6.4 Korean IME 오버레이
- Unity WebGL canvas 위에 HTML `<input>`을 절대 위치로 겹치기
- `.jslib`에서 브라우저 `compositionend` / `input` 이벤트를 받아 Unity C#로 전달
- Flutter WebView에서도 동일한 경로 사용 — `webview_flutter`의 JavaScriptChannel이 DOM 이벤트를 투명하게 포워딩

---

## 7. 개발 일정 (풀타임 14일 + 버퍼 2일)

### Week 1 — 기반 + 핵심 루프
| Day | 작업 |
|---|---|
| 1 | Unity 환경 세팅 · 프로젝트 구조 · **Korean IME 프로토타입 착수 (HTML 오버레이 + .jslib)** — 최대 리스크 조기 검증 |
| 2 | IME 플랜 B (Flutter 입력 필드 → JSChannel → Unity) 프로토타입 · 두 경로 중 최종 선택 |
| 3 | Unity 프로토타입 씬 — 주방 · 캐릭터 · 스테이션 · 기본 이동 |
| 4 | 재료 상태 시스템 · 주문 생성 · 손님 등장 · 상태 머신 |
| 5 | Gemini 호출 #1 C# 구현 · 스트리밍 response · JSON 파싱 |
| 6 | 셰프 행동 실행 엔진 — actions 배열 → Unity 애니메이션 + event_log 기록 |

### Week 2 — 브릿지 + 완성
| Day | 작업 |
|---|---|
| 7 | Unity WebGL 빌드 설정 · 템플릿 커스터마이즈 · 사이즈 최적화 (Brotli) |
| 8 | Flutter 셸 + webview_flutter · Unity 빌드 로드 · Riverpod 구조 |
| 9 | 양방향 브릿지 구현 — Unity ↔ JS ↔ Flutter (round_end, reset, pause) |
| 10 | **버퍼일** — IME 초과 / 브릿지 디버깅 / Gemini 프롬프트 튜닝 |
| 11 | Gemini 호출 #2 (평가) · event_log 통합 · reason 피드백 UI · § 4.3 실패 모드 |
| 12 | 주문 5종 구현 · 재료 상태 튜닝 · 허세 모먼트 검증 (특히 계란찜 순서 실패) |
| 13 | Vercel 웹 배포 · 모바일 빌드 · 시연 영상 녹화 |
| 14 | 시연 영상 1~2분 · README · 아키텍처 다이어그램 |

**추가 리스크 버퍼: +2일** (IME/브릿지 디버깅 초과 대비)

---

## 8. Acceptance Criteria

- [ ] Unity WebGL 빌드가 Vercel에 배포되어 URL 하나로 즉시 플레이 가능
- [ ] Flutter 셸이 모바일(iOS WKWebView)에서 Unity WebGL을 로드하고 브릿지가 정상 동작
- [ ] **JS → Flutter 브릿지**: 라운드 결과가 Flutter로 전달되어 Riverpod 상태에 누적되고 Material UI로 표시
- [ ] **Flutter → JS → Unity 브릿지**: Flutter 측 리셋 버튼이 Unity 게임 상태를 초기화
- [ ] Gemini 호출 #1이 JSON 스키마로 구조화 출력, monologue가 타이핑 스트리밍됨
- [ ] Gemini 호출 #2가 `{success, reason}`을 일관되게 반환
- [ ] 한 줄 한국어 입력 → 셰프 행동 실행 → 성공/실패 판정까지 끊김 없이 동작
- [ ] **계란찜 평가가 event_log 순서를 근거로 실패 reason을 반환함** (순서 민감 훅 동작 증명)
- [ ] **Gemini API 타임아웃/파싱 실패 시 게임이 크래시 없이 라운드 무효화로 복구됨**
- [ ] **Korean IME 입력이 데스크톱 브라우저와 모바일 WKWebView 양쪽에서 조합 중 글자 손실 없이 동작**
- [ ] README에 architecture diagram 포함 (Flutter ↔ WebView ↔ JS Bridge ↔ Unity ↔ Gemini)
- [ ] 1~2분 시연 영상: 성공 모먼트 + **순서 실수로 인한 실패 모먼트** + 브릿지 동작 노출
- [ ] 브릿지 구현 코드 파일이 README에서 명시적으로 링크

---

## 9. Non-Goals (의도적 OUT-of-scope)

- 사운드 / BGM / SFX
- 모바일 스토어 제출 (App Store / Google Play)
- 타이머 / 실패 조건 (손님 인내심 등)
- 메인 메뉴 / 설정 / 일시정지 UI
- 다국어 (한국어 단일)
- 멀티플레이어 / 리더보드 / 계정
- 인앱결제 / 광고
- 레벨 진행도 / 잠금 해제
- 별도 찜기 스테이션 (화구에 흡수)

---

## 10. Ontology (Key Entities)

| Entity | Fields | Relationships |
|---|---|---|
| Player | inputBuffer, resultLog | issues Instruction |
| DayOneChef | position, holding, state | executes Action[] |
| Station | type(fridge/board/stove/assembly/counter), occupants | hosts Action |
| Ingredient | id, name, state(raw/cooked/chopped/mixed) | held by DayOneChef, used in Action |
| Order | id, recipe, customer, status | requested by Customer, judged by Evaluator |
| Customer | id, sprite, mood | gives Order, receives Result |
| Instruction | text(≤80자), timestamp | input by Player, parsed by Gemini |
| Action | verb, target, param, t | recorded to EventLog during execution |
| EventLog | Action[] (timestamp 순) | consumed by Evaluator for order-sensitive judgment |
| Result | success, reason | output by Gemini evaluator, displayed by Bridge |

---

## 11. Player Fantasy

플레이어는 **사수**가 된다. 오늘 첫 출근한 열정 과잉 신입에게 "이것도 모르냐?" 싶은 절차를 한 줄로 꾹꾹 눌러 가르치는 경험.

핵심 감정:
- **가르치는 재미** — 애매하게 말하면 실패한다는 자각. "내가 이 정도는 말해줘야 하는구나"의 깨달음.
- **허세 웃음** — 셰프가 모르는 걸 당당하게 엉뚱한 순서로 실행하는 걸 보고 웃음. 특히 계란찜을 물 먼저 넣어버릴 때.
- **프롬프트 기술 체득** — 끝나고 나면 "AI한테 지시할 때는 순서·구체성·빠진 단계를 챙겨야 하는구나"라는 일반화된 감각이 남음.

플레이어는 실패할수록 지시문이 길어지고 구체적이 되며, 결과적으로 **프롬프트 엔지니어링을 놀이로 체험**한다. 이게 이 게임이 주는 진짜 판타지다.

## 12. Detailed Rules

- **1라운드 = 1주문**. 주문은 튜토리얼 순서(토스트→샐러드→치즈버거→오믈렛→계란찜) 고정. 랜덤 없음.
- **지시 입력**: 한국어 1줄, ≤80자. 엔터로 제출. 제출 후 해당 라운드 동안 추가 입력 불가.
- **행동 실행**: Gemini가 반환한 `actions[]`을 0.6초 간격으로 순차 실행. 실행 중 플레이어는 관전만 가능.
- **셰프 자동화 원칙**: 지시에 없는 절차는 **절대 자동 추론하지 않는다**. 예: "빵 구워줘"라고만 하면 서빙까지 가지 않음 (주문 실패).
- **재료는 항상 원시 상태로 스폰**. 냉장고에서 꺼내는 시점의 state는 고정값 (`raw`/`통째`/`껍질`).
- **순서 민감 주문** (계란찜): event_log의 action 순서까지 평가 대상. 이외 주문은 최종 결과물만 평가.
- **실패 라운드**: 다음 주문으로 즉시 넘어감. 재시도 없음. 누적 로그에 실패로 기록.
- **게임 종료 조건**: 5주문 전부 처리하면 종료. 성공/실패 횟수만 표시하고 점수·랭킹 없음.

세부 규칙은 §2 (주문), §3 (구성요소), §4 (AI 연동) 참조.

## 13. Formulas

이 콘셉트 문서는 **밸런스 수치가 거의 없는** 개발 스펙이지만, 시스템 성립에 필요한 수치 상수는 아래 고정값으로 픽스한다.

| 상수 | 값 | 의미 |
|---|---|---|
| `ACTION_TICK` | 0.6초 | action 배열의 한 항목 실행 간격 |
| `INPUT_CHAR_CAP` | 80자 | 지시문 최대 길이 |
| `GEMINI_TIMEOUT` | 8초 | 호출 #1/#2 공통 타임아웃 |
| `GEMINI_RETRY` | 1회 | 타임아웃 시 재시도 횟수 |
| `ORDER_COUNT` | 5 | 1세션 주문 수 |
| `COOK_DURATION_GRILL` | 2.0초 | 패티/빵 굽기 애니메이션 길이 |
| `COOK_DURATION_FRY` | 1.5초 | 오믈렛 부침 애니메이션 길이 |
| `COOK_DURATION_STEAM` | 3.0초 | 계란찜 찜 애니메이션 길이 |

Gemini 구조화 출력은 수치보다 **스키마 준수**가 핵심이라 별도 공식 없음. 호출 #2의 성공/실패 판정은 룰베이스가 아닌 LLM 판단이므로 수식화 불가 — 프롬프트에 판정 기준을 명시할 뿐이다 (§4.2).

## 14. Edge Cases

§4.3 실패 모드를 베이스로 하여 구체 상황 정의:

- **지시문 공백 / 빈 문자열 제출** → 호출 자체를 막고 UI에 "한 줄만 적어주세요" 인라인 힌트
- **지시문 영어/이모지/특수문자 혼용** → Gemini가 알아서 처리 (한국어 강제하지 않음). 단 시연 영상은 한국어로 촬영.
- **actions 배열이 빈 배열** → 셰프가 "… 음?" monologue만 출력 + 라운드 실패 판정
- **Gemini가 존재하지 않는 재료명 반환** (예: "양파") → Unity가 skip하고 event_log에 `{verb, target, skipped: true}` 기록. 평가 단계에서 "준비되지 않은 행동"으로 reason에 반영.
- **동일 재료 중복 pickup** → 기존 holding 덮어쓰기. 원본 재료는 소멸 (버린 것으로 처리).
- **`serve` 전에 `assemble` 없이 원재료만 올림** → 그대로 서빙. 평가에서 "조립 안 함"으로 실패.
- **계란찜에서 순서어 없이 지시** → Gemini가 나열 순으로 해석 (보통 물→계란). event_log에 `mix water before crack egg`가 찍혀 순서 오류 판정 → **허세 모먼트 트리거**
- **WebGL 메모리 초과로 프리즈** → Flutter 측 heartbeat 타임아웃(15초) 감지 → 사용자에게 리셋 버튼 노출 (브릿지 리셋).
- **모바일 WebView에서 IME 포커스 잃음** → Flutter 측 입력 필드로 자동 fallback (§6.4). *개발 메모: iOS Simulator에서 테스트할 때 "I/O → Keyboard → Connect Hardware Keyboard"를 해제하고 시뮬레이션된 iOS에 한국어 키보드를 추가해야 실제 IME 경로가 타는다 — Mac 하드웨어 키보드는 IME를 우회한다.*

## 15. Dependencies

**업스트림 (이 게임이 의존):**
- Unity 6.3 LTS + C# (게임 엔진)
- Gemini 2.5 Flash API (행동 산출 + 평가 — API 없으면 동작 불가)
- Flutter `webview_flutter` + Riverpod (모바일 셸)
- Newtonsoft.Json for Unity (Gemini 응답 파싱)
- Korean IME overlay (`.jslib` + HTML `<input>`) (한글 입력 가능성의 전제)

**다운스트림 (이 게임이 만드는 산출물):**
- `design/gdd/game-concept.md` (이 문서)
- 향후 per-system GDD (`design/gdd/chef-action-system.md` 등 — `/map-systems` 실행 시 분해)
- 향후 ADR (§ technical-preferences에 5개 예약 — IME, 브릿지 프로토콜, Gemini 아키텍처, 액션 실행기, Evaluator 계약)

순환 의존은 없음. 시스템 분해는 `/map-systems` 스킬 실행 시점에 공식화.

## 16. Tuning Knobs

| Knob | 현재값 | 안전 범위 | 효과 |
|---|---|---|---|
| `ACTION_TICK` | 0.6s | 0.3 ~ 1.2s | 낮추면 시연 속도↑/호흡 부족, 높이면 긴장감↓/지루함 |
| `GEMINI_TEMPERATURE` (호출 #1) | 0.7 | 0.3 ~ 1.0 | 낮추면 지시대로 정직 실행, 높이면 창의/엉뚱 행동↑ (허세 모먼트 강화) |
| `GEMINI_TEMPERATURE` (호출 #2) | 0.2 | 0.0 ~ 0.4 | 낮게 유지 — 평가는 일관성이 생명 |
| `INPUT_CHAR_CAP` | 80 | 60 ~ 120 | 짧으면 모스부호, 길면 프롬프트 튜닝 게임이 됨 |
| `GEMINI_TIMEOUT` | 8s | 5 ~ 12s | Gemini Free tier 체감 레이턴시 고려 |
| monologue 타이핑 속도 | 30 char/s | 20 ~ 50 | 체감 반응성. 너무 빠르면 스트리밍 감상 사라짐 |
| 주문 순서 (튜토리얼) | 고정 | — | 초반 3주문은 난이도 순, 마지막에 계란찜(하이라이트) 고정 |

세부 튜닝은 Day 12 (주문 5종 구현·허세 모먼트 검증) 세션에서 확정.

---

*Day One Chef Spec · 2026.04*
