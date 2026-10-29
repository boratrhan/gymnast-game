using System;
using System.Collections.Generic;
using UnityEngine;

namespace GymnastGame.Simulation
{
    /// <summary>
    /// Key points of the body for one pose, in the body frame: origin at the hip joint,
    /// +y along the trunk towards the head, +x the way the chest faces.
    /// Inertia is the moment of inertia about the centre of mass per kilogram of body mass
    /// (mass cancels out in ω = L / I, so the simulation works per kilogram).
    /// </summary>
    public readonly struct BodyGeometry
    {
        public Vector2 CentreOfMass { get; }
        public float Inertia { get; }
        public Vector2 Shoulder { get; }
        public Vector2 HeadTop { get; }
        public Vector2 Elbow { get; }
        public Vector2 Grip { get; }        // where the hands hold the bar
        public Vector2 Knee { get; }
        public Vector2 Sole { get; }        // bottom of the feet

        public BodyGeometry(Vector2 centreOfMass, float inertia, Vector2 shoulder, Vector2 headTop,
            Vector2 elbow, Vector2 grip, Vector2 knee, Vector2 sole)
        {
            CentreOfMass = centreOfMass;
            Inertia = inertia;
            Shoulder = shoulder;
            HeadTop = headTop;
            Elbow = elbow;
            Grip = grip;
            Knee = knee;
            Sole = sole;
        }

        /// <summary>
        /// Lowest world height of any key point when the centre of mass is at comY
        /// and the body frame is rotated by bodyAngle (radians, counter-clockwise).
        /// </summary>
        public float LowestY(float comY, float bodyAngle)
        {
            float sin = Mathf.Sin(bodyAngle), cos = Mathf.Cos(bodyAngle);
            // A local function inside a struct cannot use the struct's own members, so copy them first.
            Vector2 centre = CentreOfMass;
            float WorldY(Vector2 p) => comY + (p.x - centre.x) * sin + (p.y - centre.y) * cos;

            float lowest = WorldY(Vector2.zero);    // hip joint
            lowest = Mathf.Min(lowest, WorldY(Shoulder));
            lowest = Mathf.Min(lowest, WorldY(HeadTop));
            lowest = Mathf.Min(lowest, WorldY(Elbow));
            lowest = Mathf.Min(lowest, WorldY(Grip));
            lowest = Mathf.Min(lowest, WorldY(Knee));
            lowest = Mathf.Min(lowest, WorldY(Sole));
            return lowest;
        }
    }

    /// <summary>
    /// Planar segment model of the gymnast (trunk, head, arms, legs) built from body.json.
    /// Turns joint angles into key points, centre of mass and moment of inertia.
    /// </summary>
    public class BodyModel
    {
        private readonly Segment trunk, head, upperArm, forearm, hand, thigh, shank, foot;
        private readonly Dictionary<string, BodyPose> poses = new Dictionary<string, BodyPose>();
        private readonly float gripAlongHand;

        public float Height { get; }
        public float ShapeChangeTime { get; }

        /// <summary>Angular speed of every joint while changing shape, in degrees per second.</summary>
        public float JointSpeed { get; }

        public float TrunkLength => trunk.Length;
        public float HeadLength => head.Length;
        public float UpperArmLength => upperArm.Length;
        public float ForearmLength => forearm.Length;
        public float HandLength => hand.Length;
        public float ThighLength => thigh.Length;
        public float ShankLength => shank.Length;
        public float FootLength => foot.Length;

        private BodyModel(BodyFile data)
        {
            Height = data.height;
            ShapeChangeTime = data.shapeChangeTime;
            gripAlongHand = data.gripAlongHand;

            var byName = new Dictionary<string, SegmentEntry>();
            foreach (var entry in data.segments) byName[entry.name] = entry;
            Segment Load(string name)
            {
                if (!byName.TryGetValue(name, out var entry))
                    throw new ArgumentException($"body.json has no segment '{name}'.");
                return new Segment(entry, data.height);
            }
            trunk = Load("trunk");
            head = Load("head");
            upperArm = Load("upperArm");
            forearm = Load("forearm");
            hand = Load("hand");
            thigh = Load("thigh");
            shank = Load("shank");
            foot = Load("foot");

            foreach (var pose in data.poses)
                poses[pose.name] = new BodyPose(pose.shoulder, pose.elbow, pose.hip, pose.knee);

            float range = BodyPose.MaxDifference(ShapePose(BodyShape.Straight), ShapePose(BodyShape.Tuck));
            JointSpeed = range / ShapeChangeTime;
        }

        public static BodyModel FromJson(string json) => new BodyModel(JsonUtility.FromJson<BodyFile>(json));

        public BodyPose Pose(string name)
        {
            if (!poses.TryGetValue(name, out var pose))
                throw new ArgumentException($"body.json has no pose '{name}'.");
            return pose;
        }

        public BodyPose ShapePose(BodyShape shape)
        {
            switch (shape)
            {
                case BodyShape.Pike: return Pose("pike");
                case BodyShape.Tuck: return Pose("tuck");
                default: return Pose("straight");
            }
        }

        public BodyGeometry Evaluate(BodyPose pose)
        {
            // A limb direction at angle a: 0 points down the body (-y), positive turns towards the chest (+x).
            Vector2 Direction(float degrees)
            {
                float a = degrees * Mathf.Deg2Rad;
                return new Vector2(Mathf.Sin(a), -Mathf.Cos(a));
            }

            var up = Vector2.up;
            Vector2 hip = Vector2.zero;
            Vector2 shoulder = hip + up * trunk.Length;
            Vector2 headTop = shoulder + up * head.Length;

            Vector2 upperArmDir = Direction(pose.Shoulder);
            Vector2 forearmDir = Direction(pose.Shoulder + pose.Elbow);
            Vector2 elbow = shoulder + upperArmDir * upperArm.Length;
            Vector2 wrist = elbow + forearmDir * forearm.Length;
            Vector2 grip = wrist + forearmDir * (hand.Length * gripAlongHand);

            Vector2 thighDir = Direction(pose.Hip);
            Vector2 shankDir = Direction(pose.Hip - pose.Knee);
            Vector2 knee = hip + thighDir * thigh.Length;
            Vector2 ankle = knee + shankDir * shank.Length;
            Vector2 sole = ankle + shankDir * foot.Length;

            // Centre of mass and moment of inertia: sum over segments of m (|p - c|² + k²),
            // where k is the segment's own radius of gyration.
            var accumulator = new InertiaAccumulator();
            accumulator.Add(trunk, hip, up);
            accumulator.Add(head, shoulder, up);
            accumulator.Add(upperArm, shoulder, upperArmDir);
            accumulator.Add(forearm, elbow, forearmDir);
            accumulator.Add(hand, wrist, forearmDir);
            accumulator.Add(thigh, hip, thighDir);
            accumulator.Add(shank, knee, shankDir);
            accumulator.Add(foot, ankle, shankDir);

            Vector2 centreOfMass = accumulator.CentreOfMass;
            return new BodyGeometry(centreOfMass, accumulator.InertiaAbout(centreOfMass),
                shoulder, headTop, elbow, grip, knee, sole);
        }

        private readonly struct Segment
        {
            public float Length { get; }
            public float Mass { get; }              // fraction of body mass, both sides for paired segments
            public float ComAlong { get; }
            public float OwnInertia { get; }        // m k², per kilogram of body mass

            public Segment(SegmentEntry entry, float height)
            {
                Length = entry.length * height;
                Mass = entry.mass * (entry.paired ? 2f : 1f);
                ComAlong = entry.com;
                float k = entry.gyration * Length;
                OwnInertia = Mass * k * k;
            }
        }

        // Collects segment masses so the centre of mass is known before the parallel-axis sum.
        private struct InertiaAccumulator
        {
            private Vector2 weightedSum;
            private float mass;
            private float ownInertia;
            private float secondMoment;     // Σ m |p|², about the hip

            public void Add(Segment segment, Vector2 start, Vector2 direction)
            {
                Vector2 p = start + direction * (segment.Length * segment.ComAlong);
                weightedSum += p * segment.Mass;
                mass += segment.Mass;
                ownInertia += segment.OwnInertia;
                secondMoment += segment.Mass * p.sqrMagnitude;
            }

            public Vector2 CentreOfMass => weightedSum / mass;

            // Σ m |p - c|² = Σ m |p|² - M |c|²   (parallel-axis theorem)
            public float InertiaAbout(Vector2 c) => ownInertia + secondMoment - mass * c.sqrMagnitude;
        }

        // Shapes of body.json, only used while loading.
        [Serializable]
        private class BodyFile
        {
            public float height;
            public float gripAlongHand;
            public float shapeChangeTime;
            public SegmentEntry[] segments;
            public PoseEntry[] poses;
        }

        [Serializable]
        private class SegmentEntry
        {
            public string name;
            public float length;
            public float mass;
            public float com;
            public float gyration;
            public bool paired;
        }

        [Serializable]
        private class PoseEntry
        {
            public string name;
            public float shoulder;
            public float elbow;
            public float hip;
            public float knee;
        }
    }
}
