// Builds the call #2 system + user prompt. Mirrors the design of
// GeminiPromptBuilder (call #1) but for the evaluator role: input is
// the round's ground truth (order + instruction + event log + final
// kitchen state), output is a single-sentence Korean verdict.
//
// Kept transport-agnostic (no Gemini envelope here) so the prompts
// can be unit-tested without an HTTP layer — see
// EvaluatorPromptBuilderTests.

using System.Collections.Generic;
using System.Text;
using DayOneChef.Gameplay.Data;

namespace DayOneChef.Gameplay.AI
{
    public static class EvaluatorPromptBuilder
    {
        public static string BuildSystemPrompt()
        {
            return string.Join("\n", new[]
            {
                "당신은 엄격한 요리 심판입니다. 손님의 주문, 플레이어가 입력한 한국어 지시, 셰프가 실제로 한 행동(이벤트 로그)과 최종 주방 상태를 받습니다.",
                "이 정보로 라운드의 성공/실패를 판정하세요.",
                "",
                "판정 규칙:",
                "1. 주문(Recipe)에 명시된 모든 재료가 요구 상태로 완성됐는지 확인.",
                "2. 조리법 메모(procedureNotes)에 명시된 기법을 이벤트 로그에서 반드시 확인. 메모는 그 요리를 비슷한 다른 요리와 구분하는 핵심 단서다.",
                "   예: 오믈렛은 'mix 단계 사용 금지', 계란찜은 'mix 단계 필수' — 최종 상태(Cooked)는 같지만 mix verb 유무로 구분된다.",
                "   이벤트 로그에 mix verb가 있는데 메모가 mix 금지라면 실패. 메모가 mix 필수인데 mix verb가 없으면 실패.",
                "3. 순서 민감(orderSensitive=true) 주문이면 행동 순서도 검증. 예: 계란찜은 crack → mix(물 첨가) → cook 순서를 지켜야 성공.",
                "4. 의도와 다른 재료가 추가되거나, 잘못 조리됐거나, 누락이 있으면 실패.",
                "5. 부분 성공도 실패. 요리는 통째로 완성되어야 함.",
                "6. skipped=true 행동(unknown verb / 잘못된 재료 등)은 평가에서 제외하지만, 그로 인해 결과 상태가 미완성이면 실패 사유로 인용.",
                "",
                "응답은 반드시 다음 JSON 형식으로만:",
                "{\"success\": true 또는 false, \"reason\": \"한 문장으로 구체적인 사유\"}",
                "",
                "추가 텍스트, 마크다운, 코드 블록은 절대 사용하지 말 것.",
                "",
                "[reason 작성 규칙 — 매우 중요]",
                "reason은 손님 시점에서 셰프의 결과를 보고 한 마디 툭 던지는 톤. 한국어 한 문장.",
                "성공: 셰프가 한 행동의 핵심을 짧게 칭찬. 예) \"빵을 노릇하게 잘 구웠습니다.\", \"계란을 깨서 노른자 살려 잘 부쳤네요.\"",
                "실패: 단순한 누락 서술보다 *눈에 보이는 결과*나 *손님이 입에 넣었을 때의 사고*를 코미디로 묘사. 셰프의 멍청함이 어떻게 음식에 드러났는지 한 문장.",
                "  좋은 예) \"감자를 씻지 않아 흙이 그대로 묻어 나왔습니다.\"",
                "         \"채소를 씻지 않아서 벌레가 한 마리 기어 나왔습니다.\"",
                "         \"계란 껍질을 안 깨고 그대로 익혀서 익은 달걀이 통째로 굴러 나왔습니다.\"",
                "         \"통감자가 그대로 튀겨져 나와 손님이 입에 안 들어갑니다.\"",
                "         \"치즈를 안 썰고 통째로 올려서 햄버거 윗빵이 들리지도 않습니다.\"",
                "  나쁜 예) \"셰프가 씻기 단계를 누락했습니다.\" (밋밋함)",
                "         \"조리법대로 진행하지 않았습니다.\" (메타 노출)",
                "절대 금지 표현: \"조리법 메모\", \"지시서\", \"명시되어\", \"event_log\", \"verb\", \"crack\", \"beat\", \"mix\", \"cook\", \"chop\" 같은 내부 용어. 한국어 동사 (\"깨다\", \"풀다\", \"섞다\", \"굽다\", \"썰다\", \"씻다\")로 풀어서 쓸 것.",
                "이유 한 줄 안에 영어 단어가 들어가면 실패. 한국어로만.",
            });
        }

        public static string BuildUserPrompt(EvaluationContext ctx)
        {
            var sb = new StringBuilder();

            sb.AppendLine("[주문 정보]");
            var order = ctx?.Order;
            sb.Append("이름: ").AppendLine(order?.Recipe?.DisplayName ?? "(unknown)");
            sb.Append("ID:   ").AppendLine(order?.OrderId ?? "(unknown)");
            sb.AppendLine("요구 재료/상태:");
            if (order?.Recipe != null)
            {
                foreach (var c in order.Recipe.Components)
                {
                    sb.Append("  - ").Append(c.Type).Append(" → ").AppendLine(c.RequiredState.ToString());
                }
                sb.Append("순서 민감: ").AppendLine(order.Recipe.OrderSensitive ? "true (행동 순서 검증 필요)" : "false");
                if (!string.IsNullOrWhiteSpace(order.Recipe.ProcedureNotes))
                {
                    sb.Append("조리법 메모: ").AppendLine(order.Recipe.ProcedureNotes);
                }
            }

            sb.AppendLine();
            sb.AppendLine("[플레이어 입력 지시]");
            sb.Append('"').Append(ctx?.PlayerInstruction ?? string.Empty).AppendLine("\"");

            sb.AppendLine();
            sb.AppendLine("[셰프가 실제 한 행동 — 시간순 이벤트 로그]");
            var entries = ctx?.EventLog?.Entries;
            if (entries == null || entries.Count == 0)
            {
                sb.AppendLine("  (없음 — 셰프가 어떠한 행동도 하지 않음)");
            }
            else
            {
                for (var i = 0; i < entries.Count; i++)
                {
                    var e = entries[i];
                    sb.Append("  ").Append(i + 1).Append(". (t=").Append(e.t.ToString("F2"))
                      .Append("s) ").Append(e.verb).Append(' ').Append(e.target);
                    if (!string.IsNullOrEmpty(e.param)) sb.Append(' ').Append('(').Append(e.param).Append(')');
                    if (e.skipped) sb.Append(" [SKIPPED: ").Append(e.reason ?? string.Empty).Append(']');
                    else if (!string.IsNullOrEmpty(e.resolvedState))
                        sb.Append(" → ").Append(e.resolvedType).Append(' ').Append(e.resolvedState);
                    sb.AppendLine();
                }
            }

            sb.AppendLine();
            sb.AppendLine("[조리 후 주방 상태]");
            if (ctx?.FinalState == null || ctx.FinalState.Count == 0)
            {
                sb.AppendLine("  (없음)");
            }
            else
            {
                foreach (var kv in ctx.FinalState)
                {
                    sb.Append("  ").Append(kv.Key).Append(" = ").AppendLine(kv.Value.ToString());
                }
            }

            sb.AppendLine();
            sb.Append("위 정보로 판정. JSON만 응답.");
            return sb.ToString();
        }
    }
}
