using System.Collections.Generic;

namespace GymnastGame.Rules
{
    /// <summary>One E deduction with a short label for the score card.</summary>
    public readonly struct DeductionItem
    {
        public decimal Points { get; }
        public string Label { get; }

        public DeductionItem(decimal points, string label)
        {
            Points = points;
            Label = label;
        }
    }

    /// <summary>
    /// Judges a catch of the bar after a flight from its measurements, with the rules in deductions.json:
    /// hands still away from the bar (regraspReach), tuck or pike not opened (regraspNotOpened), arms not
    /// straight overhead (bentArmsRegrasp) and a twist not square to a half twist (incompleteTwist).
    /// Plain values in, so the rules do not depend on the simulation.
    /// </summary>
    public class RegraspJudge
    {
        private readonly DeductionRule reach, notOpened, bentArms, incompleteTwist;

        public RegraspJudge(DeductionTable table)
        {
            reach = table.Get("regraspReach");
            notOpened = table.Get("regraspNotOpened");
            bentArms = table.Get("bentArmsRegrasp");
            incompleteTwist = table.Get("incompleteTwist");
        }

        /// <summary>Every deduction for this catch, one item per rule that applies.</summary>
        public IReadOnlyList<DeductionItem> Deductions(float reachMetres, float legBendDegrees, float armDeviationDegrees,
            float twistErrorDegrees)
        {
            var items = new List<DeductionItem>();
            Add(items, reach.Evaluate(reachMetres, "m"), $"hands {reachMetres:0.00} m from the bar");
            Add(items, notOpened.Evaluate(legBendDegrees, "deg"), $"not opened ({legBendDegrees:0}°)");
            Add(items, bentArms.Evaluate(armDeviationDegrees, "deg"), $"arms not up ({armDeviationDegrees:0}°)");
            Add(items, incompleteTwist.Evaluate(twistErrorDegrees, "deg"), $"twist {twistErrorDegrees:0}° off");
            return items;
        }

        /// <summary>True when no rule deducts anything. Allocates nothing, so it can run every frame.</summary>
        public bool IsClean(float reachMetres, float legBendDegrees, float armDeviationDegrees, float twistErrorDegrees) =>
            reach.Evaluate(reachMetres, "m").Points == 0m
            && notOpened.Evaluate(legBendDegrees, "deg").Points == 0m
            && bentArms.Evaluate(armDeviationDegrees, "deg").Points == 0m
            && incompleteTwist.Evaluate(twistErrorDegrees, "deg").Points == 0m;

        private static void Add(List<DeductionItem> items, DeductionOutcome outcome, string label)
        {
            if (outcome.Points > 0m)
                items.Add(new DeductionItem(outcome.Points, label));
        }
    }
}
