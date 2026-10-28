using System;
using UnityEngine;

namespace GymnastGame.Rules
{
    /// <summary>
    /// How a D-score is put together on one apparatus: counting elements, group bonuses,
    /// connection values, stick bonus, flight limit and short exercise deductions.
    /// Loaded from composition.json so the rules stay data, not code.
    /// </summary>
    [Serializable]
    public class CompositionRules
    {
        [SerializeField] private int countingElements;
        [SerializeField] private int maxPerGroup;
        [SerializeField] private int dismountGroup;
        [SerializeField] private GroupBonusRule[] groupBonuses;
        [SerializeField] private ConnectionRule[] connections;
        [SerializeField] private StickBonusRule stickBonus;
        [SerializeField] private FlightLimitRule flightLimit;
        [SerializeField] private TieBreakStep[] tieBreak;
        [SerializeField] private ShortExerciseEntry[] shortExercise;

        public int CountingElements => countingElements;
        public int MaxPerGroup => maxPerGroup;
        public int DismountGroup => dismountGroup;
        public GroupBonusRule[] GroupBonuses => groupBonuses;
        public ConnectionRule[] Connections => connections;
        public StickBonusRule StickBonus => stickBonus;
        public FlightLimitRule FlightLimit => flightLimit;
        public TieBreakStep[] TieBreak => tieBreak;
        public ShortExerciseEntry[] ShortExercise => shortExercise;

        public static CompositionRules FromJson(string json) => JsonUtility.FromJson<CompositionRules>(json);

        /// <summary>The entry with the largest element count not above <paramref name="elementCount"/> applies.</summary>
        public decimal ShortExerciseDeduction(int elementCount)
        {
            ShortExerciseEntry match = null;
            foreach (var entry in shortExercise)
            {
                if (entry.Elements <= elementCount && (match == null || entry.Elements > match.Elements))
                    match = entry;
            }
            return match == null ? 0m : Points.FromFloat(match.Deduction);
        }
    }

    [Serializable]
    public class GroupBonusRule
    {
        [SerializeField] private int group;
        [SerializeField] private string minValueForHigh;
        [SerializeField] private float high;
        [SerializeField] private float low;

        public int Group => group;
        public string MinValueForHigh => minValueForHigh;
        public decimal High => Points.FromFloat(high);
        public decimal Low => Points.FromFloat(low);
    }

    [Serializable]
    public class ConnectionRule
    {
        [SerializeField] private string first;
        [SerializeField] private string second;
        [SerializeField] private string minFirst;
        [SerializeField] private string minSecond;
        [SerializeField] private bool symmetric;
        [SerializeField] private float points;
        [SerializeField] private string source;

        public string First => first;           // element kind performed first
        public string Second => second;         // element kind performed second
        public string MinFirst => minFirst;
        public string MinSecond => minSecond;
        public bool Symmetric => symmetric;     // the two minimum values may be swapped
        public decimal Points => GymnastGame.Rules.Points.FromFloat(points);
        public string Source => source;
    }

    [Serializable]
    public class StickBonusRule
    {
        [SerializeField] private string minValue;
        [SerializeField] private float points;

        public string MinValue => minValue;
        public decimal Points => GymnastGame.Rules.Points.FromFloat(points);
    }

    [Serializable]
    public class FlightLimitRule
    {
        [SerializeField] private int freeFlights;
        [SerializeField] private string unlockMinValue;

        public int FreeFlights => freeFlights;
        public string UnlockMinValue => unlockMinValue;
    }

    [Serializable]
    public class TieBreakStep
    {
        public const string Difficulty = "difficulty";
        public const string GroupBonus = "groupBonus";
        public const string Connection = "connection";

        [SerializeField] private string step;
        [SerializeField] private string source;

        public string Step => step;
        public string Source => source;
    }

    [Serializable]
    public class ShortExerciseEntry
    {
        [SerializeField] private int elements;
        [SerializeField] private float deduction;

        public int Elements => elements;
        public float Deduction => deduction;
    }
}
