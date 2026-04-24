// EditMode tests for the Day 6 event log — serialisation shape is the
// Day 11 evaluator contract, so lock it down here.

using DayOneChef.Gameplay;
using DayOneChef.Gameplay.Data;
using NUnit.Framework;

namespace DayOneChef.Tests
{
    public class EventLogTests
    {
        [Test]
        public void ToJson_WrapsEntriesUnderEntriesArray()
        {
            var log = new EventLog();
            log.Append(new EventLogEntry { verb = "pickup", target = "빵", t = 0f });
            log.Append(new EventLogEntry { verb = "cook", target = "빵", param = "grill", t = 0.6f });

            var json = log.ToJson();

            Assert.That(json, Does.Contain("\"entries\""));
            Assert.That(json, Does.Contain("pickup"));
            Assert.That(json, Does.Contain("cook"));
            Assert.That(json, Does.Contain("grill"));
        }

        [Test]
        public void Append_NullEntryIsIgnored()
        {
            var log = new EventLog();
            log.Append(null);
            Assert.AreEqual(0, log.Count);
        }

        [Test]
        public void EntriesArePreservedInInsertionOrder()
        {
            // Day 11 evaluator reads entries[i].verb in index order — any
            // reordering here would silently change 계란찜 pass/fail
            // judgment.
            var log = new EventLog();
            log.Append(new EventLogEntry { verb = "crack" });
            log.Append(new EventLogEntry { verb = "mix" });
            log.Append(new EventLogEntry { verb = "cook" });

            Assert.AreEqual("crack", log.Entries[0].verb);
            Assert.AreEqual("mix",   log.Entries[1].verb);
            Assert.AreEqual("cook",  log.Entries[2].verb);
        }
    }
}
