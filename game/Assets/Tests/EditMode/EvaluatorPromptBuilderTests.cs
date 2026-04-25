// Tests for the Day 11 evaluator prompt + response shape.
//
// EvaluatorPromptBuilder constructs Korean prose prompts that Gemini
// reads to issue a verdict; the tests below assert the structural
// elements (order details, event log, final state) that the system
// prompt instructs the model to weigh. Drift in field rendering
// would silently degrade evaluator quality, so we lock the layout
// here rather than only checking by output JSON.

using System.Collections.Generic;
using DayOneChef.Gameplay;
using DayOneChef.Gameplay.AI;
using DayOneChef.Gameplay.Data;
using NUnit.Framework;
using UnityEngine;

namespace DayOneChef.Tests
{
    public class EvaluatorPromptBuilderTests
    {
        private static Order MakeOrder(string id, string display, RecipeComponent[] components, bool orderSensitive)
        {
            var recipe = ScriptableObject.CreateInstance<Recipe>();
            recipe.Configure(display, components, orderSensitive);
            var order = ScriptableObject.CreateInstance<Order>();
            order.Configure(id, recipe, CustomerMood.Waiting, "");
            return order;
        }

        [Test]
        public void SystemPrompt_DeclaresJsonOnlyOutput()
        {
            var sys = EvaluatorPromptBuilder.BuildSystemPrompt();
            Assert.That(sys, Does.Contain("\"success\""));
            Assert.That(sys, Does.Contain("\"reason\""));
            Assert.That(sys, Does.Contain("JSON"));
            // Drift guard: no markdown / code-block hints leaking into the prompt.
            Assert.That(sys, Does.Not.Contain("```"));
        }

        [Test]
        public void SystemPrompt_NamesOrderSensitiveExample()
        {
            // 계란찜 is the canonical order-sensitive recipe (GDD §2 #5);
            // the evaluator's docstring should call it out explicitly so
            // the model knows the rule applies to it.
            var sys = EvaluatorPromptBuilder.BuildSystemPrompt();
            Assert.That(sys, Does.Contain("계란찜"));
            Assert.That(sys, Does.Contain("crack"));
            Assert.That(sys, Does.Contain("mix"));
            Assert.That(sys, Does.Contain("cook"));
        }

        [Test]
        public void UserPrompt_RendersOrderRecipeAndInstruction()
        {
            var order = MakeOrder(
                "토스트-01", "플레인 토스트",
                new[] { new RecipeComponent { Type = IngredientType.Bread, RequiredState = IngredientState.Cooked } },
                orderSensitive: false);

            var ctx = new EvaluationContext
            {
                Order = order,
                PlayerInstruction = "빵 화구에 올려서 구워줘",
                EventLog = new EventLog(),
                FinalState = new Dictionary<IngredientType, IngredientState>
                {
                    { IngredientType.Bread, IngredientState.Cooked },
                },
            };

            var user = EvaluatorPromptBuilder.BuildUserPrompt(ctx);
            Assert.That(user, Does.Contain("플레인 토스트"));
            Assert.That(user, Does.Contain("토스트-01"));
            Assert.That(user, Does.Contain("Bread → Cooked"));
            Assert.That(user, Does.Contain("순서 민감: false"));
            Assert.That(user, Does.Contain("빵 화구에 올려서 구워줘"));
            Assert.That(user, Does.Contain("Bread = Cooked"));
        }

        [Test]
        public void UserPrompt_RendersEventLogTimestampsAndSkippedFlags()
        {
            // Order-sensitive verification depends on the evaluator
            // seeing the verbs in time order — and skipped events
            // should be marked so the model doesn't penalise an
            // intentional no-op.
            var log = new EventLog();
            log.Append(new EventLogEntry { verb = "crack", target = "계란", t = 0.0f, skipped = false, resolvedType = "Egg", resolvedState = "Cracked" });
            log.Append(new EventLogEntry { verb = "bake", target = "계란", t = 0.6f, skipped = true, reason = "unknown verb" });
            log.Append(new EventLogEntry { verb = "cook", target = "계란", param = "steam", t = 1.2f, skipped = false, resolvedType = "Egg", resolvedState = "Cooked" });

            var ctx = new EvaluationContext
            {
                Order = MakeOrder("계란찜-01", "계란찜",
                    new[] { new RecipeComponent { Type = IngredientType.Egg, RequiredState = IngredientState.Cooked } },
                    orderSensitive: true),
                PlayerInstruction = "계란 깨고 익혀줘",
                EventLog = log,
                FinalState = new Dictionary<IngredientType, IngredientState> { { IngredientType.Egg, IngredientState.Cooked } },
            };

            var user = EvaluatorPromptBuilder.BuildUserPrompt(ctx);
            Assert.That(user, Does.Contain("순서 민감: true"));
            Assert.That(user, Does.Contain("(t=0.00s) crack 계란"));
            Assert.That(user, Does.Contain("[SKIPPED: unknown verb]"));
            Assert.That(user, Does.Contain("(t=1.20s) cook 계란 (steam)"));
            // Time order should be preserved in render order.
            var crackIdx = user.IndexOf("crack 계란", System.StringComparison.Ordinal);
            var cookIdx = user.IndexOf("cook 계란", System.StringComparison.Ordinal);
            Assert.Less(crackIdx, cookIdx);
        }

        [Test]
        public void ParseEvaluation_AcceptsValidJsonResponse()
        {
            // The Gemini envelope wraps the inner verdict JSON as text in
            // candidates[0].content.parts[0].text — same shape as call #1.
            var envelope = "{\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"{\\\"success\\\":true,\\\"reason\\\":\\\"빵을 잘 구웠어요\\\"}\"}]}}]}";
            var verdict = GeminiRoundEvaluator.ParseEvaluation(envelope);
            Assert.IsTrue(verdict.success);
            Assert.AreEqual("빵을 잘 구웠어요", verdict.reason);
        }

        [Test]
        public void ParseEvaluation_FillsMissingReasonWithFallback()
        {
            var envelope = "{\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"{\\\"success\\\":false}\"}]}}]}";
            var verdict = GeminiRoundEvaluator.ParseEvaluation(envelope);
            Assert.IsFalse(verdict.success);
            Assert.That(verdict.reason, Does.Contain("미명시"));
        }
    }
}
