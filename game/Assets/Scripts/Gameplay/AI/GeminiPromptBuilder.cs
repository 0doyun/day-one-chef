// Builds the system + user prompt for Gemini call #1.
// See design/gdd/game-concept.md §1.4 (캐릭터 설정), §4.1 (프롬프트 원칙).
//
// Pure string construction — no Unity dependencies — so the result
// is reproducible, diffable, and testable without spinning up Unity.

using System.Text;
using DayOneChef.Gameplay.Data;

namespace DayOneChef.Gameplay.AI
{
    public static class GeminiPromptBuilder
    {
        /// <summary>
        /// The "Day One Chef" system prompt. Treats the AI as a chef who
        /// executes the player's 지시 literally, never auto-推论-ing missing
        /// steps — that literalness is the source of the 허세 모먼트 per GDD §11.
        /// </summary>
        public static string BuildSystemPrompt()
        {
            var sb = new StringBuilder(1024);
            sb.AppendLine("너는 오늘 첫 출근한 초보 요리사 'Day One Chef'다.");
            sb.AppendLine("플레이어는 너의 사수이며 한 줄 한국어 지시로 너를 가르친다.");
            sb.AppendLine("너의 성격: 열정 과잉, 허세 강함, 모르는 것을 아는 척 실행함.");
            sb.AppendLine();
            sb.AppendLine("규칙:");
            sb.AppendLine("1. 플레이어 지시에 명시된 행동만 수행한다. 지시에 없는 절차는 절대 추론하지 않는다.");
            sb.AppendLine("2. 순서어('먼저', '그 다음', '이후')가 있으면 그 순서를 엄격히 지킨다.");
            sb.AppendLine("3. 순서어가 없으면 지시에 나열된 재료 순서대로 실행한다.");
            sb.AppendLine("4. 지시가 애매하거나 빠진 단계가 있어도 당황하지 말고 당당하게 그대로 실행한다.");
            sb.AppendLine();
            sb.AppendLine("출력은 반드시 다음 JSON 스키마를 따르는 단일 객체다. 여분의 텍스트 금지.");
            sb.AppendLine("{");
            sb.AppendLine("  \"actions\": [ { \"verb\": string, \"target\": string, \"param\": string } ],");
            sb.AppendLine("  \"monologue\": string");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("verb 허용값 (그 외 금지): pickup, cook, chop, crack, mix, assemble, serve, move");
            sb.AppendLine("- pickup: 재료/결과물 집기 (target = 재료명 또는 결과물)");
            sb.AppendLine("- cook: 화구에서 조리 (target = 재료, param = grill | fry | steam)");
            sb.AppendLine("- chop: 도마에서 썰기 (target = 재료)");
            sb.AppendLine("- crack: 계란 깨기 (target = 계란)");
            sb.AppendLine("- mix: 그릇에 재료 섞기 (target = 그릇 또는 재료)");
            sb.AppendLine("- assemble: 조립대에서 쌓기 (target = 조립할 결과물)");
            sb.AppendLine("- serve: 카운터로 서빙 (target = 결과물)");
            sb.AppendLine("- move: 위치 이동 (target = 스테이션명)");
            sb.AppendLine();
            sb.AppendLine("monologue: 셰프의 자신만만한 한마디 (1~2문장, 한국어).");
            sb.AppendLine("param: 해당하지 않으면 빈 문자열. 없는 필드를 만들지 말 것.");
            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// Builds the user-turn prompt: the current round context + the
        /// player's literal 지시. Kept short on purpose so the model spends
        /// token budget on the action list rather than context recitation.
        /// </summary>
        public static string BuildUserPrompt(GameStateSnapshot state, string playerInstruction)
        {
            var sb = new StringBuilder(512);
            sb.Append("현재 주문: ").AppendLine(state.OrderDisplayName);
            if (!string.IsNullOrWhiteSpace(state.OrderExampleInstruction))
            {
                sb.Append("지시 예시(참고용, 복사 금지): ").AppendLine(state.OrderExampleInstruction);
            }
            sb.Append("주방 재료: ").AppendLine(string.Join(", ", state.AvailableIngredients));
            sb.Append("주방 스테이션: ").AppendLine(string.Join(", ", state.AvailableStations));
            sb.AppendLine();
            sb.Append("플레이어 지시: ").AppendLine(playerInstruction ?? string.Empty);
            sb.AppendLine();
            sb.Append("위 지시를 문자 그대로 실행하는 actions 배열과 자신만만한 monologue를 JSON으로 반환해라.");
            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// Try to map a raw Gemini verb string to <see cref="ChefVerb"/>.
        /// Case-insensitive. Returns false on unknown tokens — the action
        /// executor will flag the ChefAction as skipped per GDD §4.3.
        /// </summary>
        public static bool TryParseVerb(string raw, out ChefVerb verb)
        {
            verb = default;
            if (string.IsNullOrWhiteSpace(raw)) return false;
            switch (raw.Trim().ToLowerInvariant())
            {
                case "pickup":   verb = ChefVerb.Pickup;   return true;
                case "cook":     verb = ChefVerb.Cook;     return true;
                case "chop":     verb = ChefVerb.Chop;     return true;
                case "crack":    verb = ChefVerb.Crack;    return true;
                case "mix":      verb = ChefVerb.Mix;      return true;
                case "assemble": verb = ChefVerb.Assemble; return true;
                case "serve":    verb = ChefVerb.Serve;    return true;
                case "move":     verb = ChefVerb.Move;     return true;
                default: return false;
            }
        }
    }
}
