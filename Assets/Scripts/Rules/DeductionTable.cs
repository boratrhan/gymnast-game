using System;
using System.Collections.Generic;
using UnityEngine;

namespace GymnastGame.Rules
{
    /// <summary>The E deduction for one measured error, and whether the element also loses its value.</summary>
    public readonly struct DeductionOutcome
    {
        public decimal Points { get; }          // 0 when nothing is deducted
        public bool NotRecognized { get; }

        public DeductionOutcome(decimal points, bool notRecognized)
        {
            Points = points;
            NotRecognized = notRecognized;
        }
    }

    /// <summary>
    /// Execution deduction rules from deductions.json. Each rule has bands sorted by their "above"
    /// threshold; the highest band whose threshold the measured value exceeds applies.
    /// </summary>
    public class DeductionTable
    {
        private readonly Dictionary<string, DeductionRule> rulesById = new Dictionary<string, DeductionRule>();

        public static DeductionTable FromJson(string json)
        {
            var data = JsonUtility.FromJson<DeductionFile>(json);
            var table = new DeductionTable();
            foreach (var rule in data.rules)
                table.rulesById.Add(rule.Id, rule);
            return table;
        }

        public DeductionRule Get(string id)
        {
            if (!rulesById.TryGetValue(id, out var rule))
                throw new KeyNotFoundException($"Unknown deduction rule '{id}'.");
            return rule;
        }

        [Serializable]
        private class DeductionFile
        {
            public DeductionRule[] rules;
        }
    }

    [Serializable]
    public class DeductionRule
    {
        [SerializeField] private string id;
        [SerializeField] private string source;
        [SerializeField] private string description;
        [SerializeField] private string measure;
        [SerializeField] private bool enabled;
        [SerializeField] private DeductionBand[] bands;

        public string Id => id;
        public string Source => source;
        public string Description => description;
        public string Measure => measure;
        public bool Enabled => enabled;

        /// <summary>
        /// The deduction for a measured value, using only the bands in the given unit
        /// (some rules mix units, e.g. centimetres and shoulder widths). A disabled rule deducts nothing.
        /// </summary>
        public DeductionOutcome Evaluate(float value, string unit)
        {
            DeductionBand applied = null;
            if (enabled)
            {
                foreach (var band in bands)
                {
                    if (band.Unit == unit && value > band.Above && (applied == null || band.Above > applied.Above))
                        applied = band;
                }
            }
            return applied == null
                ? new DeductionOutcome(0m, false)
                : new DeductionOutcome(Points.FromFloat(applied.Deduction), applied.NotRecognized);
        }
    }

    [Serializable]
    public class DeductionBand
    {
        [SerializeField] private float above;
        [SerializeField] private string unit;
        [SerializeField] private float deduction;
        [SerializeField] private bool notRecognized;
        [SerializeField] private string source;     // set when only this band is our own decision

        public float Above => above;
        public string Unit => unit;
        public float Deduction => deduction;
        public bool NotRecognized => notRecognized;
        public string Source => source;
    }
}
