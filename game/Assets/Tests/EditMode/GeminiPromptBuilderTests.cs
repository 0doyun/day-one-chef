// EditMode tests for the Day 5 Gemini prompt builder. Keeps the
// contract stable even as the system prompt evolves for tone/feel —
// things that change (monologue tone lines) are NOT tested here;
// things that the action executor depends on (verb set, JSON shape
// instructions) ARE asserted.

using System.Collections.Generic;
using DayOneChef.Gameplay.AI;
using DayOneChef.Gameplay.Data;
using NUnit.Framework;

namespace DayOneChef.Tests
{
    public class GeminiPromptBuilderTests
    {
        [Test]
        public void SystemPrompt_AdvertisesAllEightVerbs()
        {
            var prompt = GeminiPromptBuilder.BuildSystemPrompt();
            foreach (var verb in System.Enum.GetNames(typeof(ChefVerb)))
            {
                Assert.That(
                    prompt.ToLowerInvariant(),
                    Does.Contain(verb.ToLowerInvariant()),
                    $"System prompt missing verb: {verb}");
            }
        }

        [Test]
        public void SystemPrompt_InstructsJsonOnly()
        {
            var prompt = GeminiPromptBuilder.BuildSystemPrompt();
            Assert.That(prompt, Does.Contain("JSON"));
            Assert.That(prompt, Does.Contain("actions"));
            Assert.That(prompt, Does.Contain("monologue"));
        }

        [Test]
        public void UserPrompt_IncludesOrderAndInstruction()
        {
            var state = new GameStateSnapshot(
                "플레인 토스트",
                "빵 화구에 올려서 구워줘",
                new List<string> { "빵", "패티" },
                new List<string> { "냉장고", "화구" });

            var prompt = GeminiPromptBuilder.BuildUserPrompt(state, "빵 구워줘");

            Assert.That(prompt, Does.Contain("플레인 토스트"));
            Assert.That(prompt, Does.Contain("빵 구워줘"));
            Assert.That(prompt, Does.Contain("화구"));
        }

        [TestCase("pickup",   ChefVerb.Pickup)]
        [TestCase("COOK",     ChefVerb.Cook)]
        [TestCase(" chop ",   ChefVerb.Chop)]
        [TestCase("crack",    ChefVerb.Crack)]
        [TestCase("mix",      ChefVerb.Mix)]
        [TestCase("assemble", ChefVerb.Assemble)]
        [TestCase("serve",    ChefVerb.Serve)]
        [TestCase("move",     ChefVerb.Move)]
        public void TryParseVerb_AcceptsKnownTokensCaseInsensitive(string raw, ChefVerb expected)
        {
            Assert.IsTrue(GeminiPromptBuilder.TryParseVerb(raw, out var parsed));
            Assert.AreEqual(expected, parsed);
        }

        [TestCase("")]
        [TestCase(null)]
        [TestCase("bake")]
        [TestCase("pickup_up")]
        public void TryParseVerb_RejectsUnknownTokens(string raw)
        {
            Assert.IsFalse(GeminiPromptBuilder.TryParseVerb(raw, out _));
        }
    }
}
