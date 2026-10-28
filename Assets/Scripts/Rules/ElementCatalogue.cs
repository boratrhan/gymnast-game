using System;
using System.Collections.Generic;
using UnityEngine;

namespace GymnastGame.Rules
{
    /// <summary>
    /// All elements of one apparatus, looked up by id, plus the value scale (A = 0.1 ...)
    /// and the special repetition families. Built once from elements.json.
    /// </summary>
    public class ElementCatalogue
    {
        private readonly Dictionary<string, ElementDefinition> elementsById = new Dictionary<string, ElementDefinition>();
        private readonly Dictionary<string, decimal> pointsByLetter = new Dictionary<string, decimal>();
        private readonly Dictionary<string, int> familyLimits = new Dictionary<string, int>();

        public IReadOnlyCollection<ElementDefinition> Elements => elementsById.Values;

        public static ElementCatalogue FromJson(string json)
        {
            var data = JsonUtility.FromJson<CatalogueFile>(json);
            var catalogue = new ElementCatalogue();

            foreach (var entry in data.valueScale)
                catalogue.pointsByLetter[entry.letter] = Points.FromFloat(entry.points);

            foreach (var family in data.families)
                catalogue.familyLimits[family.family] = family.maxPerExercise;

            foreach (var element in data.elements)
                catalogue.elementsById.Add(element.Id, element);

            return catalogue;
        }

        public ElementDefinition Get(string id)
        {
            if (!elementsById.TryGetValue(id, out var element))
                throw new KeyNotFoundException($"Unknown element id '{id}'.");
            return element;
        }

        public decimal PointsOf(string letter)
        {
            if (!pointsByLetter.TryGetValue(letter, out var points))
                throw new ArgumentException($"Unknown difficulty letter '{letter}'.");
            return points;
        }

        public decimal ValueOf(ElementDefinition element) => PointsOf(element.Value);

        /// <summary>How many elements of this family may receive value; int.MaxValue if no limit.</summary>
        public int FamilyLimit(string family)
        {
            if (string.IsNullOrEmpty(family) || !familyLimits.TryGetValue(family, out var limit))
                return int.MaxValue;
            return limit;
        }

        // Shapes of the JSON file. Only used while loading, so plain public fields are fine here.
        [Serializable]
        private class CatalogueFile
        {
            public ValueScaleEntry[] valueScale;
            public FamilyEntry[] families;
            public ElementDefinition[] elements;
        }

        [Serializable]
        private class ValueScaleEntry
        {
            public string letter;
            public float points;
        }

        [Serializable]
        private class FamilyEntry
        {
            public string family;
            public int maxPerExercise;
        }
    }

    /// <summary>Scores use decimal so that 0.1 + 0.2 is exactly 0.3.</summary>
    public static class Points
    {
        public static decimal FromFloat(float value) => Math.Round((decimal)value, 2);
    }
}
