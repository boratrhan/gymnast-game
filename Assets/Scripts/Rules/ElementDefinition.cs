using System;
using UnityEngine;

namespace GymnastGame.Rules
{
    /// <summary>
    /// One element of the high bar difficulty tables, as stored in elements.json.
    /// Read-only after loading; JsonUtility fills the private fields.
    /// </summary>
    [Serializable]
    public class ElementDefinition
    {
        [SerializeField] private string id;
        [SerializeField] private int group;
        [SerializeField] private int box;
        [SerializeField] private string value;
        [SerializeField] private string name;
        [SerializeField] private string description;
        [SerializeField] private string kind;
        [SerializeField] private string family;
        [SerializeField] private float saltos;
        [SerializeField] private float twists;
        [SerializeField] private string shape;
        [SerializeField] private string direction;
        [SerializeField] private bool overBar;
        [SerializeField] private bool handsLeaveBar;
        [SerializeField] private string handstandCategory;
        [SerializeField] private bool needsReview;
        [SerializeField] private string reviewNote;
        [SerializeField] private ElementVariant[] variants;

        public string Id => id;                 // e.g. "II.88"
        public int Group => group;              // 1-4 (I-IV)
        public int Box => box;
        public string Value => value;           // difficulty letter, e.g. "D"
        public string Name => name;             // eponym, may be empty
        public string Description => description;
        public string Kind => kind;             // swing | release | inBar | dismount
        public string Family => family;         // special repetition family, may be empty
        public float Saltos => saltos;
        public float Twists => twists;
        public string Shape => shape;
        public string Direction => direction;
        public bool OverBar => overBar;
        public bool HandsLeaveBar => handsLeaveBar;
        public string HandstandCategory => handstandCategory;
        public bool NeedsReview => needsReview;
        public string ReviewNote => reviewNote;
        public ElementVariant[] Variants => variants;

        public string DisplayName => string.IsNullOrEmpty(name) ? description : name;
    }

    /// <summary>A variant shares the id and value of its element (e.g. Tkatchev / Lynch).</summary>
    [Serializable]
    public class ElementVariant
    {
        [SerializeField] private string name;
        [SerializeField] private float saltos;
        [SerializeField] private float twists;
        [SerializeField] private string shape;
        [SerializeField] private string direction;
        [SerializeField] private bool overBar;
        [SerializeField] private bool handsLeaveBar;

        public string Name => name;
        public float Saltos => saltos;
        public float Twists => twists;
        public string Shape => shape;
        public string Direction => direction;
        public bool OverBar => overBar;
        public bool HandsLeaveBar => handsLeaveBar;
    }
}
