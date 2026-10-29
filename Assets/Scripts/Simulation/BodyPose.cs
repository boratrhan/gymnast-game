using UnityEngine;

namespace GymnastGame.Simulation
{
    /// <summary>Body shapes the player can choose in the air.</summary>
    public enum BodyShape
    {
        Straight,
        Pike,
        Tuck
    }

    /// <summary>
    /// Joint angles of the planar figure, in degrees. Left and right are the same for now.
    /// Shoulder: 0 = arms down, 180 = overhead. Elbow, knee: 0 = straight.
    /// Hip: 0 = legs straight down, positive = legs forward (towards the chest).
    /// </summary>
    public readonly struct BodyPose
    {
        public float Shoulder { get; }
        public float Elbow { get; }
        public float Hip { get; }
        public float Knee { get; }

        public BodyPose(float shoulder, float elbow, float hip, float knee)
        {
            Shoulder = shoulder;
            Elbow = elbow;
            Hip = hip;
            Knee = knee;
        }

        /// <summary>Moves every joint towards the target by at most maxDegrees.</summary>
        public BodyPose MoveTowards(BodyPose target, float maxDegrees) => new BodyPose(
            Mathf.MoveTowards(Shoulder, target.Shoulder, maxDegrees),
            Mathf.MoveTowards(Elbow, target.Elbow, maxDegrees),
            Mathf.MoveTowards(Hip, target.Hip, maxDegrees),
            Mathf.MoveTowards(Knee, target.Knee, maxDegrees));

        public static BodyPose Lerp(BodyPose from, BodyPose to, float t) => new BodyPose(
            Mathf.Lerp(from.Shoulder, to.Shoulder, t),
            Mathf.Lerp(from.Elbow, to.Elbow, t),
            Mathf.Lerp(from.Hip, to.Hip, t),
            Mathf.Lerp(from.Knee, to.Knee, t));

        /// <summary>The largest joint difference, in degrees.</summary>
        public static float MaxDifference(BodyPose a, BodyPose b) => Mathf.Max(
            Mathf.Max(Mathf.Abs(a.Shoulder - b.Shoulder), Mathf.Abs(a.Elbow - b.Elbow)),
            Mathf.Max(Mathf.Abs(a.Hip - b.Hip), Mathf.Abs(a.Knee - b.Knee)));
    }
}
