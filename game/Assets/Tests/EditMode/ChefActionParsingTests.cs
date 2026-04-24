// EditMode tests for GeminiClient.ParseResponse — walks the Gemini REST
// envelope and surfaces parsed ChefActionResponse content, handling the
// schema edge cases in design/gdd/game-concept.md §4.3.

using DayOneChef.Gameplay.AI;
using NUnit.Framework;

namespace DayOneChef.Tests
{
    public class ChefActionParsingTests
    {
        private const string HappyPathEnvelope = @"
        {
            ""candidates"": [{
                ""content"": {
                    ""role"": ""model"",
                    ""parts"": [{
                        ""text"": ""{\""actions\"":[{\""verb\"":\""pickup\"",\""target\"":\""빵\"",\""param\"":\""\""},{\""verb\"":\""cook\"",\""target\"":\""빵\"",\""param\"":\""grill\""}],\""monologue\"":\""빵 구워드립니다!\""}""
                    }]
                },
                ""finishReason"": ""STOP""
            }]
        }";

        [Test]
        public void ParseResponse_HappyPath_ReturnsPopulatedResponse()
        {
            var response = GeminiClient.ParseResponse(HappyPathEnvelope);

            Assert.IsNotNull(response);
            Assert.IsNotNull(response.actions);
            Assert.AreEqual(2, response.actions.Length);
            Assert.AreEqual("pickup", response.actions[0].verb);
            Assert.AreEqual("빵", response.actions[0].target);
            Assert.AreEqual("cook", response.actions[1].verb);
            Assert.AreEqual("grill", response.actions[1].param);
            Assert.AreEqual("빵 구워드립니다!", response.monologue);
        }

        [Test]
        public void ParseResponse_EmptyBody_ThrowsGeminiCallException()
        {
            Assert.Throws<GeminiCallException>(() => GeminiClient.ParseResponse(string.Empty));
            Assert.Throws<GeminiCallException>(() => GeminiClient.ParseResponse("   "));
        }

        [Test]
        public void ParseResponse_MissingCandidates_ThrowsGeminiCallException()
        {
            var envelope = @"{ ""candidates"": [] }";
            Assert.Throws<GeminiCallException>(() => GeminiClient.ParseResponse(envelope));
        }

        [Test]
        public void ParseResponse_MalformedInnerJson_ThrowsGeminiCallException()
        {
            var envelope = @"
            {
                ""candidates"": [{
                    ""content"": {
                        ""role"": ""model"",
                        ""parts"": [{ ""text"": ""{not-valid-json"" }]
                    }
                }]
            }";
            Assert.Throws<GeminiCallException>(() => GeminiClient.ParseResponse(envelope));
        }

        [Test]
        public void ParseResponse_InnerMissingActions_DefaultsToEmptyArray()
        {
            // GDD §14 edge case: empty actions array is legal — the round
            // fails with a confused monologue but parsing must not.
            var envelope = @"
            {
                ""candidates"": [{
                    ""content"": {
                        ""parts"": [{ ""text"": ""{\""monologue\"":\""어... 음?\""}"" }]
                    }
                }]
            }";
            var response = GeminiClient.ParseResponse(envelope);
            Assert.IsNotNull(response.actions);
            Assert.AreEqual(0, response.actions.Length);
            Assert.AreEqual("어... 음?", response.monologue);
        }

        [Test]
        public void BuildRequestJson_ContainsSystemAndUserParts()
        {
            var json = GeminiClient.BuildRequestJson("SYS prompt", "USER 지시");
            Assert.That(json, Does.Contain("SYS prompt"));
            Assert.That(json, Does.Contain("USER 지시"));
            Assert.That(json, Does.Contain("systemInstruction"));
            Assert.That(json, Does.Contain("generationConfig"));
        }
    }
}
