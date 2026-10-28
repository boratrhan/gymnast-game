using System.Collections.Generic;

namespace GymnastGame.Rules
{
    /// <summary>Why an element did or did not contribute to the D-score.</summary>
    public enum ElementStatus
    {
        Counted,            // one of the best counting elements
        Dismount,           // the final dismount, always counted
        NotInBestCounting,  // valid, but outside the best counting elements
        Repetition,         // same id performed earlier
        FamilyLimit,        // over the special repetition limit of its family
        FlightLimit,        // fifth or later flight without two connected flights
        NotADismount        // dismount group element that is not the last element
    }

    public readonly struct ElementResult
    {
        public int Index { get; }               // position in the routine
        public ElementDefinition Element { get; }
        public ElementStatus Status { get; }

        public ElementResult(int index, ElementDefinition element, ElementStatus status)
        {
            Index = index;
            Element = element;
            Status = status;
        }
    }

    /// <summary>
    /// Breakdown of a D-score. The short exercise deduction is kept separate because it is
    /// taken from the final score, not from D.
    /// </summary>
    public class DScoreResult
    {
        public decimal Difficulty { get; }      // values of the counting elements and the dismount
        public decimal GroupBonus { get; }      // element group requirements, dismount group included
        public decimal Connection { get; }
        public decimal StickBonus { get; }
        public decimal NeutralDeduction { get; }
        public int CountedElements { get; }     // counting elements plus the dismount
        public IReadOnlyList<ElementResult> Elements { get; }

        public decimal Total => Difficulty + GroupBonus + Connection + StickBonus;

        public DScoreResult(decimal difficulty, decimal groupBonus, decimal connection, decimal stickBonus,
            decimal neutralDeduction, int countedElements, IReadOnlyList<ElementResult> elements)
        {
            Difficulty = difficulty;
            GroupBonus = groupBonus;
            Connection = connection;
            StickBonus = stickBonus;
            NeutralDeduction = neutralDeduction;
            CountedElements = countedElements;
            Elements = elements;
        }
    }
}
