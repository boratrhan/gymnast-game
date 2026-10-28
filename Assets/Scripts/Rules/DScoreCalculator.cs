using System;
using System.Collections.Generic;

namespace GymnastGame.Rules
{
    /// <summary>
    /// Calculates the D-score of a routine from the element catalogue and the composition rules.
    /// Steps: repetition, flight limit, family limit, best counting elements, group bonuses,
    /// connection values, stick bonus and short exercise deduction.
    /// </summary>
    public class DScoreCalculator
    {
        private const string ReleaseKind = "release";

        private readonly ElementCatalogue catalogue;
        private readonly CompositionRules rules;

        public DScoreCalculator(ElementCatalogue catalogue, CompositionRules rules)
        {
            this.catalogue = catalogue;
            this.rules = rules;
        }

        public DScoreResult Calculate(Routine routine)
        {
            var performed = routine.Elements;
            int count = performed.Count;

            var elements = new ElementDefinition[count];
            for (int i = 0; i < count; i++)
                elements[i] = catalogue.Get(performed[i].ElementId);

            // null = still eligible for value
            var status = new ElementStatus?[count];

            int dismountIndex = -1;
            for (int i = 0; i < count; i++)
            {
                if (elements[i].Group != rules.DismountGroup) continue;
                if (i == count - 1) dismountIndex = i;
                else status[i] = ElementStatus.NotADismount;
            }

            MarkRepetitions(elements, status);
            MarkFlightLimit(performed, elements, status);
            MarkFamilyLimits(elements, status);

            var candidates = new List<int>();
            for (int i = 0; i < count; i++)
            {
                if (status[i] == null && i != dismountIndex)
                    candidates.Add(i);
            }

            var best = SelectCounting(candidates, elements, performed, dismountIndex);

            var results = new ElementResult[count];
            for (int i = 0; i < count; i++)
            {
                ElementStatus finalStatus;
                if (i == dismountIndex) finalStatus = ElementStatus.Dismount;
                else if (status[i] != null) finalStatus = status[i].Value;
                else if (best.Counted[i]) finalStatus = ElementStatus.Counted;
                else finalStatus = ElementStatus.NotInBestCounting;
                results[i] = new ElementResult(i, elements[i], finalStatus);
            }

            decimal difficulty = best.Difficulty;
            decimal groupBonus = best.GroupBonus;
            decimal stickBonus = 0m;
            if (dismountIndex >= 0)
            {
                decimal dismountValue = catalogue.ValueOf(elements[dismountIndex]);
                difficulty += dismountValue;
                groupBonus += dismountValue;  // dismount group value equals the dismount value
                if (routine.DismountStuck && dismountValue >= catalogue.PointsOf(rules.StickBonus.MinValue))
                    stickBonus = rules.StickBonus.Points;
            }

            int countedElements = best.Size + (dismountIndex >= 0 ? 1 : 0);
            return new DScoreResult(difficulty, groupBonus, best.Connection, stickBonus,
                rules.ShortExerciseDeduction(countedElements), countedElements, results);
        }

        // The same id gets value only the first time it is performed.
        private static void MarkRepetitions(ElementDefinition[] elements, ElementStatus?[] status)
        {
            var seen = new HashSet<string>();
            for (int i = 0; i < elements.Length; i++)
            {
                if (status[i] == null && !seen.Add(elements[i].Id))
                    status[i] = ElementStatus.Repetition;
            }
        }

        // Flights after the free ones (in performed order) get no value, unless two flights
        // of at least the unlock value were directly connected somewhere in the routine.
        private void MarkFlightLimit(IReadOnlyList<PerformedElement> performed, ElementDefinition[] elements,
            ElementStatus?[] status)
        {
            decimal unlockValue = catalogue.PointsOf(rules.FlightLimit.UnlockMinValue);
            for (int i = 1; i < elements.Length; i++)
            {
                if (performed[i].ConnectedToPrevious
                    && IsFlight(elements[i - 1]) && IsFlight(elements[i])
                    && catalogue.ValueOf(elements[i - 1]) >= unlockValue
                    && catalogue.ValueOf(elements[i]) >= unlockValue)
                    return;
            }

            int flights = 0;
            for (int i = 0; i < elements.Length; i++)
            {
                if (!IsFlight(elements[i])) continue;
                flights++;
                if (flights > rules.FlightLimit.FreeFlights && status[i] == null)
                    status[i] = ElementStatus.FlightLimit;
            }
        }

        // Within a family only the highest valued elements keep their value (earlier one wins a tie).
        private void MarkFamilyLimits(ElementDefinition[] elements, ElementStatus?[] status)
        {
            var byFamily = new Dictionary<string, List<int>>();
            for (int i = 0; i < elements.Length; i++)
            {
                string family = elements[i].Family;
                if (status[i] != null || string.IsNullOrEmpty(family)) continue;
                if (!byFamily.TryGetValue(family, out var members))
                    byFamily[family] = members = new List<int>();
                members.Add(i);
            }

            foreach (var pair in byFamily)
            {
                var members = pair.Value;
                int limit = catalogue.FamilyLimit(pair.Key);
                if (members.Count <= limit) continue;

                // Sort by value, highest first; the index keeps ties in performed order.
                members.Sort((a, b) =>
                {
                    int byValue = catalogue.ValueOf(elements[b]).CompareTo(catalogue.ValueOf(elements[a]));
                    return byValue != 0 ? byValue : a.CompareTo(b);
                });
                for (int k = limit; k < members.Count; k++)
                    status[members[k]] = ElementStatus.FamilyLimit;
            }
        }

        /// <summary>
        /// Tries every allowed selection of counting elements (at most CountingElements, at most
        /// MaxPerGroup per group) and keeps the best one by the tie-break order of the rules.
        /// A routine has few candidates, so trying all selections is fast and easy to verify.
        /// </summary>
        private Selection SelectCounting(List<int> candidates, ElementDefinition[] elements,
            IReadOnlyList<PerformedElement> performed, int dismountIndex)
        {
            var chosen = new List<int>();
            var perGroup = new Dictionary<int, int>();
            Selection best = Evaluate(chosen, elements, performed, dismountIndex);

            void Search(int start)
            {
                var current = Evaluate(chosen, elements, performed, dismountIndex);
                if (IsBetter(current, best)) best = current;
                if (chosen.Count == rules.CountingElements) return;

                for (int k = start; k < candidates.Count; k++)
                {
                    int index = candidates[k];
                    int group = elements[index].Group;
                    perGroup.TryGetValue(group, out int used);
                    if (used == rules.MaxPerGroup) continue;

                    chosen.Add(index);
                    perGroup[group] = used + 1;
                    Search(k + 1);
                    chosen.RemoveAt(chosen.Count - 1);
                    perGroup[group] = used;
                }
            }

            Search(0);
            return best;
        }

        private Selection Evaluate(List<int> chosen, ElementDefinition[] elements,
            IReadOnlyList<PerformedElement> performed, int dismountIndex)
        {
            var counted = new bool[elements.Length];
            decimal difficulty = 0m;
            foreach (int index in chosen)
            {
                counted[index] = true;
                difficulty += catalogue.ValueOf(elements[index]);
            }

            decimal groupBonus = 0m;
            foreach (var rule in rules.GroupBonuses)
            {
                decimal bestInGroup = -1m;
                foreach (int index in chosen)
                {
                    if (elements[index].Group == rule.Group)
                        bestInGroup = Math.Max(bestInGroup, catalogue.ValueOf(elements[index]));
                }
                if (bestInGroup < 0m) continue;
                groupBonus += bestInGroup >= catalogue.PointsOf(rule.MinValueForHigh) ? rule.High : rule.Low;
            }

            // Connections count only between counting elements; the dismount counts too.
            if (dismountIndex >= 0) counted[dismountIndex] = true;
            decimal connection = 0m;
            for (int i = 1; i < elements.Length; i++)
            {
                if (performed[i].ConnectedToPrevious && counted[i - 1] && counted[i])
                    connection += ConnectionValue(elements[i - 1], elements[i]);
            }
            if (dismountIndex >= 0) counted[dismountIndex] = false;

            return new Selection(counted, chosen.Count, difficulty, groupBonus, connection);
        }

        // The highest matching rule applies; 0 if none matches.
        private decimal ConnectionValue(ElementDefinition first, ElementDefinition second)
        {
            decimal firstValue = catalogue.ValueOf(first);
            decimal secondValue = catalogue.ValueOf(second);
            decimal best = 0m;

            foreach (var rule in rules.Connections)
            {
                if (rule.First != first.Kind || rule.Second != second.Kind) continue;

                decimal minFirst = catalogue.PointsOf(rule.MinFirst);
                decimal minSecond = catalogue.PointsOf(rule.MinSecond);
                bool matches = (firstValue >= minFirst && secondValue >= minSecond)
                    || (rule.Symmetric && firstValue >= minSecond && secondValue >= minFirst);
                if (matches) best = Math.Max(best, rule.Points);
            }
            return best;
        }

        // Compares step by step in the tie-break order; only a strictly better selection wins.
        private bool IsBetter(Selection candidate, Selection current)
        {
            foreach (var step in rules.TieBreak)
            {
                int comparison = candidate.ScoreFor(step.Step).CompareTo(current.ScoreFor(step.Step));
                if (comparison != 0) return comparison > 0;
            }
            return false;
        }

        private static bool IsFlight(ElementDefinition element) => element.Kind == ReleaseKind;

        private readonly struct Selection
        {
            public bool[] Counted { get; }      // by routine index, dismount excluded
            public int Size { get; }
            public decimal Difficulty { get; }
            public decimal GroupBonus { get; }
            public decimal Connection { get; }

            public Selection(bool[] counted, int size, decimal difficulty, decimal groupBonus, decimal connection)
            {
                Counted = counted;
                Size = size;
                Difficulty = difficulty;
                GroupBonus = groupBonus;
                Connection = connection;
            }

            public decimal ScoreFor(string step)
            {
                switch (step)
                {
                    case TieBreakStep.Difficulty: return Difficulty;
                    case TieBreakStep.GroupBonus: return GroupBonus;
                    case TieBreakStep.Connection: return Connection;
                    default: throw new ArgumentException($"Unknown tie-break step '{step}'.");
                }
            }
        }
    }
}
