// EditMode tests for the Day 3 prototype gameplay surface.
// StationMarker is the cleanest unit boundary — pure C# with a trivial
// collider contract that doesn't need PlayMode or input-system plumbing.

using DayOneChef.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace DayOneChef.Tests
{
    public class StationMarkerTests
    {
        [Test]
        public void Configure_SetsTypeAndLabel()
        {
            var go = new GameObject("TestStation");
            go.AddComponent<BoxCollider2D>();
            var marker = go.AddComponent<StationMarker>();

            marker.Configure(StationType.Stove, "화구");

            Assert.AreEqual(StationType.Stove, marker.StationType);
            Assert.AreEqual("화구", marker.DisplayLabel);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void Configure_NullLabel_DefaultsToEmptyString()
        {
            var go = new GameObject("TestStation");
            go.AddComponent<BoxCollider2D>();
            var marker = go.AddComponent<StationMarker>();

            marker.Configure(StationType.Fridge, null);

            Assert.AreEqual(string.Empty, marker.DisplayLabel);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void AllStationTypes_ShippedForDay3()
        {
            // Day 3 scope locks exactly 5 stations. If this test fails, the
            // prototype scope has drifted — reconcile with GDD §3 before
            // changing the enum.
            Assert.AreEqual(5, System.Enum.GetValues(typeof(StationType)).Length);
            Assert.IsTrue(System.Enum.IsDefined(typeof(StationType), StationType.Fridge));
            Assert.IsTrue(System.Enum.IsDefined(typeof(StationType), StationType.CuttingBoard));
            Assert.IsTrue(System.Enum.IsDefined(typeof(StationType), StationType.Stove));
            Assert.IsTrue(System.Enum.IsDefined(typeof(StationType), StationType.Assembly));
            Assert.IsTrue(System.Enum.IsDefined(typeof(StationType), StationType.Counter));
        }
    }
}
