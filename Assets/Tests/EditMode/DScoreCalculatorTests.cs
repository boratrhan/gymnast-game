using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace GymnastGame.Rules.Tests
{
    /// <summary>
    /// D-score tests against the real high bar data files. Expected values are calculated
    /// by hand from the Code of Points (A = 0.1, B = 0.2, C = 0.3, D = 0.4, E = 0.5, F = 0.6, G = 0.7).
    /// </summary>
    public class DScoreCalculatorTests
    {
        private DScoreCalculator calculator;

        [OneTimeSetUp]
        public void LoadData()
        {
            string folder = Path.Combine(Application.dataPath, "Data", "HighBar");
            var catalogue = ElementCatalogue.FromJson(File.ReadAllText(Path.Combine(folder, "elements.json")));
            var rules = CompositionRules.FromJson(File.ReadAllText(Path.Combine(folder, "composition.json")));
            calculator = new DScoreCalculator(catalogue, rules);
        }

        // E = element on its own, C = connected to the element before it
        private static PerformedElement E(string id) => new PerformedElement(id);
        private static PerformedElement C(string id) => new PerformedElement(id, connectedToPrevious: true);

        private static ElementStatus StatusAt(DScoreResult result, int index) => result.Elements[index].Status;

        [Test]
        public void Routine1_BestSevenPlusDismount()
        {
            var routine = new Routine(new[]
            {
                E("I.51"), E("II.88"), E("I.69"), E("III.63"), E("II.15"),
                E("III.38"), E("I.13"), E("II.3"), E("III.8"), E("IV.39")
            });

            var result = calculator.Calculate(routine);

            // D + 5 x C + B = 2.1, plus dismount C = 0.3
            Assert.AreEqual(2.4m, result.Difficulty);
            // I 0.5 + II 0.5 (Kovacs D) + III 0.3 (best is C) + dismount 0.3
            Assert.AreEqual(1.6m, result.GroupBonus);
            Assert.AreEqual(0m, result.Connection);
            Assert.AreEqual(0m, result.StickBonus);
            Assert.AreEqual(4.0m, result.Total);
            Assert.AreEqual(0m, result.NeutralDeduction);
            Assert.AreEqual(ElementStatus.NotInBestCounting, StatusAt(result, 6));  // I.13 giant swing A
            Assert.AreEqual(ElementStatus.Dismount, StatusAt(result, 9));
        }

        [Test]
        public void Routine2_FlightLimitFamilyLimitRepetitionAndStick()
        {
            var routine = new Routine(new[]
            {
                E("II.15"), E("II.16"), E("II.29"), E("II.88"), E("II.95"), E("II.65"), E("II.41"),
                E("I.45"), E("I.45"), E("III.38"), E("III.33"), E("I.31"), E("IV.42")
            }, dismountStuck: true);

            var result = calculator.Calculate(routine);

            Assert.AreEqual(ElementStatus.FamilyLimit, StatusAt(result, 0));   // 3rd Tkatchev family element, lowest value
            Assert.AreEqual(ElementStatus.FlightLimit, StatusAt(result, 4));   // 5th flight, no connected flights
            Assert.AreEqual(ElementStatus.FlightLimit, StatusAt(result, 5));
            Assert.AreEqual(ElementStatus.FlightLimit, StatusAt(result, 6));
            Assert.AreEqual(ElementStatus.Repetition, StatusAt(result, 8));    // second Quast
            Assert.AreEqual(ElementStatus.Counted, StatusAt(result, 11));      // I.31 is 7th, nothing else is left

            // E + D + D + C + C + B + A = 2.2, plus dismount F = 0.6
            Assert.AreEqual(2.8m, result.Difficulty);
            // I 0.5 + II 0.5 + III 0.3 + dismount 0.6
            Assert.AreEqual(1.9m, result.GroupBonus);
            Assert.AreEqual(0.1m, result.StickBonus);
            Assert.AreEqual(4.8m, result.Total);
            Assert.AreEqual(0m, result.NeutralDeduction);
        }

        [Test]
        public void Routine3_ConnectionsChainsAndTieBreak()
        {
            var routine = new Routine(new[]
            {
                E("I.31"), E("III.64"), C("II.16"), C("II.18"), E("I.51"),
                E("II.3"), C("II.9"), C("II.88"), E("III.38"), E("IV.58")
            });

            var result = calculator.Calculate(routine);

            // Five flights but two of them are connected, so the flight limit does not apply.
            // EG II allows 4: II.3 and II.9 tie on value; II.9 brings connection value, so it counts.
            Assert.AreEqual(ElementStatus.NotInBestCounting, StatusAt(result, 5));  // II.3
            Assert.AreEqual(ElementStatus.Counted, StatusAt(result, 6));            // II.9
            Assert.AreEqual(ElementStatus.NotInBestCounting, StatusAt(result, 0));  // I.31 A

            // G + D + D + C + D + C + B = 2.7, plus dismount D = 0.4
            Assert.AreEqual(3.1m, result.Difficulty);
            // 0.5 + 0.5 + 0.5 + dismount 0.4
            Assert.AreEqual(1.9m, result.GroupBonus);
            // III.64-II.16 (D+D) 0.1, II.16-II.18 (D+G) 0.2, II.9-II.88 (C+D) 0.1
            Assert.AreEqual(0.4m, result.Connection);
            Assert.AreEqual(5.4m, result.Total);
        }

        [Test]
        public void Routine4_ShortExerciseAndNoStickBonusBelowC()
        {
            var routine = new Routine(new[]
            {
                E("I.31"), E("II.15"), E("II.15"), E("III.38"), E("IV.25")
            }, dismountStuck: true);

            var result = calculator.Calculate(routine);

            Assert.AreEqual(ElementStatus.Repetition, StatusAt(result, 2));
            // A + C + B = 0.6, plus dismount A = 0.1
            Assert.AreEqual(0.7m, result.Difficulty);
            // I 0.5 + II 0.3 + III 0.3 + dismount 0.1
            Assert.AreEqual(1.2m, result.GroupBonus);
            Assert.AreEqual(0m, result.StickBonus);
            Assert.AreEqual(1.9m, result.Total);
            Assert.AreEqual(4, result.CountedElements);
            Assert.AreEqual(4.0m, result.NeutralDeduction);
        }

        [Test]
        public void FifthFlight_CountsWhenTwoFlightsAreConnected()
        {
            var result = calculator.Calculate(FiveFlightRoutine(connected: true));

            // Markelov-Yamawaki (C+C) unlocks the 5th flight but gives no connection value.
            Assert.AreEqual(ElementStatus.Counted, StatusAt(result, 4));   // II.65 Gaylord
            Assert.AreEqual(0m, result.Connection);
            // EG II limited to 4: E + D + C + C, then C + B = 2.0, plus dismount C = 0.3
            Assert.AreEqual(2.3m, result.Difficulty);
            Assert.AreEqual(3.9m, result.Total);
        }

        [Test]
        public void FifthFlight_GetsNoValueWithoutConnectedFlights()
        {
            var result = calculator.Calculate(FiveFlightRoutine(connected: false));

            Assert.AreEqual(ElementStatus.FlightLimit, StatusAt(result, 4));   // II.65 Gaylord
            // C + C + C + D + C + B = 1.8, plus dismount C = 0.3
            Assert.AreEqual(2.1m, result.Difficulty);
            Assert.AreEqual(3.7m, result.Total);
        }

        private static Routine FiveFlightRoutine(bool connected) => new Routine(new[]
        {
            E("II.3"), new PerformedElement("II.9", connected), E("II.15"), E("II.88"), E("II.65"),
            E("I.51"), E("III.38"), E("IV.39")
        });

        // Two directly connected elements, no dismount: only the connection table is tested.
        [TestCase("II.15", "II.88", 0.1, TestName = "Flight C + flight D")]
        [TestCase("II.88", "II.15", 0.1, TestName = "Flight D + flight C (vice versa)")]
        [TestCase("II.15", "II.3", 0.0, TestName = "Flight C + flight C")]
        [TestCase("II.88", "II.16", 0.1, TestName = "Flight D + flight D")]
        [TestCase("II.88", "II.29", 0.2, TestName = "Flight D + flight E")]
        [TestCase("II.29", "II.95", 0.2, TestName = "Flight E + flight E")]
        [TestCase("III.64", "II.16", 0.1, TestName = "In-bar D + flight D")]
        [TestCase("III.63", "II.16", 0.0, TestName = "In-bar C + flight D")]
        [TestCase("III.64", "II.29", 0.2, TestName = "In-bar D + flight E")]
        [TestCase("III.65", "II.88", 0.2, TestName = "In-bar E + flight D (vice versa)")]
        [TestCase("I.70", "II.88", 0.1, TestName = "Swing D + flight D")]
        [TestCase("II.16", "III.64", 0.1, TestName = "Flight D + in-bar D (reverse order)")]
        [TestCase("II.29", "III.64", 0.2, TestName = "Flight E + in-bar D (reverse order)")]
        [TestCase("II.88", "I.70", 0.1, TestName = "Flight D + swing D (reverse order)")]
        [TestCase("II.16", "III.63", 0.0, TestName = "Flight D + in-bar C (reverse order)")]
        public void ConnectionTable(string first, string second, double expected)
        {
            var result = calculator.Calculate(new Routine(new[] { E(first), C(second) }));

            Assert.AreEqual((decimal)expected, result.Connection);
        }

        [Test]
        public void NoDismount_NoDismountGroupValue()
        {
            var result = calculator.Calculate(new Routine(new[] { E("I.31") }));

            Assert.AreEqual(0.1m, result.Difficulty);
            Assert.AreEqual(0.5m, result.GroupBonus);  // EG I only
            Assert.AreEqual(1, result.CountedElements);
            Assert.AreEqual(7.0m, result.NeutralDeduction);
        }

        [Test]
        public void DismountBeforeTheEnd_GetsNoValue()
        {
            var result = calculator.Calculate(new Routine(new[] { E("IV.39"), E("I.31") }));

            Assert.AreEqual(ElementStatus.NotADismount, StatusAt(result, 0));
            Assert.AreEqual(0.1m, result.Difficulty);
        }

        [Test]
        public void UnknownElement_Throws()
        {
            Assert.Throws<KeyNotFoundException>(() => calculator.Calculate(new Routine(new[] { E("II.999") })));
        }

        [Test]
        public void Catalogue_HasAllHighBarElements()
        {
            string json = File.ReadAllText(Path.Combine(Application.dataPath, "Data", "HighBar", "elements.json"));
            var catalogue = ElementCatalogue.FromJson(json);

            Assert.AreEqual(131, catalogue.Elements.Count);
            Assert.AreEqual(4, catalogue.Elements.Select(e => e.Group).Distinct().Count());
            Assert.AreEqual(0.9m, catalogue.ValueOf(catalogue.Get("II.102")));  // Miyachi, I
        }
    }
}
