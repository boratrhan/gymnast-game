using System.Collections.Generic;
using System.Linq;

namespace GymnastGame.Rules
{
    /// <summary>One element as it was performed in a routine.</summary>
    public readonly struct PerformedElement
    {
        public string ElementId { get; }
        public bool ConnectedToPrevious { get; }    // directly connected to the element before it

        public PerformedElement(string elementId, bool connectedToPrevious = false)
        {
            ElementId = elementId;
            ConnectedToPrevious = connectedToPrevious;
        }
    }

    /// <summary>The elements of an exercise in performed order, plus how the dismount was landed.</summary>
    public class Routine
    {
        public IReadOnlyList<PerformedElement> Elements { get; }
        public bool DismountStuck { get; }

        public Routine(IEnumerable<PerformedElement> elements, bool dismountStuck = false)
        {
            Elements = elements.ToList();
            DismountStuck = dismountStuck;
        }
    }
}
