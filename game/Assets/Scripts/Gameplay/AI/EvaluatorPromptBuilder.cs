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
                "2. 순서 민감(orderSensitive=true) 주문이면 행동 순서도 검증. 예: 계란찜은 crack → mix(물 첨가) → cook 순서를 지켜야 성공.",
                "3. 의도와 다른 재료가 추가되거나, 잘못 조리됐거나, 누락이 있으면 실패.",
                "4. 부분 성공도 실패. 요리는 통째로 완성되어야 함.",
                "5. skipped=true 행동(unknown verb / 잘못된 재료 등)은 평가에서 제외하지만, 그로 인해 결과 상태가 미완성이면 실패 사유로 인용.",
                "",
                "응답은 반드시 다음 JSON 형식으로만:",
                "{\"success\": true 또는 false, \"reason\": \"한 문장으로 구체적인 사유\"}",
                "",
                "추가 텍스트, 마크다운, 코드 블록은 절대 사용하지 말 것.",
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
