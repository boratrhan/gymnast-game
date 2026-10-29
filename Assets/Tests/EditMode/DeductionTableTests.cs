using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace GymnastGame.Rules.Tests
{
    /// <summary>Deduction bands read from deductions.json, checked at and around each threshold.</summary>
    public class DeductionTableTests
    {
        private DeductionTable table;

        [OneTimeSetUp]
        public void LoadData()
        {
            string path = Path.Combine(Application.dataPath, "Data", "HighBar", "deductions.json");
            table = DeductionTable.FromJson(File.ReadAllText(path));
        }

        // Up to 5° is our tolerance; above it the Code of Points bands: small up to 30°, medium up to 60°,
        // large up to 90°, and large with non-recognition beyond 90°.
        [TestCase(4f, 0.0, false)]
        [TestCase(5f, 0.0, false)]
        [TestCase(6f, 0.1, false)]
        [TestCase(30f, 0.1, false)]
        [TestCase(31f, 0.3, false)]
        [TestCase(60f, 0.3, false)]
        [TestCase(61f, 0.5, false)]
        [TestCase(90f, 0.5, false)]
        [TestCase(95f, 0.5, true)]
        public void IncompleteTwist_UsesTheBands(float degrees, double points, bool notRecognized)
        {
            var outcome = table.Get("incompleteTwist").Evaluate(degrees, "deg");

            Assert.AreEqual((decimal)points, outcome.Points);
            Assert.AreEqual(notRecognized, outcome.NotRecognized);
        }

        [Test]
        public void DisabledRule_DeductsNothing()
        {
            var rule = table.Get("lackOfExtensionBeforeLanding");

            Assert.IsFalse(rule.Enabled);
            Assert.AreEqual(0m, rule.Evaluate(100f, "deg").Points);
        }

        [Test]
        public void OnlyBandsInTheGivenUnitCount()
        {
            // landingFeetApart mixes centimetres and shoulder widths.
            var rule = table.Get("landingFeetApart");

            Assert.AreEqual(0.1m, rule.Evaluate(15f, "cm").Points);
            Assert.AreEqual(0.3m, rule.Evaluate(1.2f, "shoulderWidth").Points);
        }
    }
}
