using System;
using System.Collections.Generic;
using UnityEngine;

namespace GymnastGame.Simulation
{
    /// <summary>
    /// Key points of the body for one pose, in the body frame: origin at the hip joint,
    /// +y along the trunk towards the head, +x the way the chest faces.
    /// Inertia is the moment of inertia about the centre of mass for the salto (about an axis parallel to the bar);
    /// TwistInertia is about the body's long axis through the centre of mass (for the twist).
    /// Both are per kilogram of body mass: mass cancels out in ω = L / I, so the simulation works per kilogram.
    /// </summary>
    public readonly struct BodyGeometry
    {
        public Vector2 CentreOfMass { get; }
        public float Inertia { get; }
        public float TwistInertia { get; }
        public Vector2 Shoulder { get; }
        public Vector2 HeadTop { get; }
        public Vector2 Elbow { get; }
        public Vector2 Grip { get; }        // where the hands hold the bar
        public Vector2 Knee { get; }
        public Vector2 Sole { get; }        // bottom of the feet

        public BodyGeometry(Vector2 centreOfMass, float inertia, float twistInertia, Vector2 shoulder, Vector2 headTop,
            Vector2 elbow, Vector2 grip, Vector2 knee, Vector2 sole)
        {
            CentreOfMass = centreOfMass;
            Inertia = inertia;
            TwistInertia = twistInertia;
            Shoulder = shoulder;
            HeadTop = headTop;
            Elbow = elbow;
            Grip = grip;
            Knee = knee;
            Sole = sole;
        }

        /// <summary>
        /// Lowest world height of any key point when the centre of mass is at comY, the body frame is
        /// rotated by bodyAngle (radians, counter-clockwise) and twisted by twistAngle (radians) about its
        /// long axis. Twisting turns the forward offsets out of the plane, so they shrink by cos(twist).
        /// </summary>
        public float LowestY(float comY, float bodyAngle, float twistAngle = 0f)
        {
            float sin = Mathf.Sin(bodyAngle), cos = Mathf.Cos(bodyAngle), twist = Mathf.Cos(twistAngle);
            // A local function inside a struct cannot use the struct's own members, so copy them first.
            Vector2 centre = CentreOfMass;
            float WorldY(Vector2 p) => comY + (p.x - centre.x) * twist * sin + (p.y - centre.y) * cos;

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
    /// When the hands can catch the bar. Only distance and impact speed can stop a catch; how clean it was
    /// (reach, shape, arms, twist) is judged by the regrasp rules in deductions.json.
    /// </summary>
    public readonly struct CatchLimits
    {
        public float MaxReach { get; }              // metres between the hands and the bar
        public float ReachTime { get; }             // seconds the body takes to reach for the bar
        public float InputBuffer { get; }           // seconds an early Enter press still counts
        public float MaxImpactSpeed { get; }        // m/s towards or away from the bar
        public float RearmDistance { get; }         // metres the hands must move away after the release

        public CatchLimits(float maxReach, float reachTime, float inputBuffer, float maxImpactSpeed, float rearmDistance)
        {
            MaxReach = maxReach;
            ReachTime = reachTime;
            InputBuffer = inputBuffer;
            MaxImpactSpeed = maxImpactSpeed;
            RearmDistance = rearmDistance;
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
        private readonly float shoulderHalfWidth, hipHalfWidth;

        public float Height { get; }
        public float ShapeChangeTime { get; }

        /// <summary>Angular speed of every joint while changing shape, in degrees per second.</summary>
        public float JointSpeed { get; }

        /// <summary>How fast HandsIn changes (0..1 per second), so the arms move in step with the joints.</summary>
        public float HandsInSpeed { get; }

        /// <summary>Twist angular momentum per kilogram at full twist: the maximum rate in the 'twist' pose.</summary>
        public float MaxTwistMomentum { get; }
        public float TwistSpinUpTime { get; }
        public float TwistStopTime { get; }

        /// <summary>Limits for catching the bar after a flight (body.json "catch").</summary>
        public CatchLimits Catch { get; }

        public float ShoulderHalfWidth => shoulderHalfWidth;
        public float HipHalfWidth => hipHalfWidth;
        public float TrunkRadius => trunk.Radius;
        public float HeadRadius => head.Radius;
        public float UpperArmRadius => upperArm.Radius;
        public float ForearmRadius => forearm.Radius;
        public float HandRadius => hand.Radius;
        public float ThighRadius => thigh.Radius;
        public float ShankRadius => shank.Radius;
        public float FootRadius => foot.Radius;

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
            shoulderHalfWidth = data.shoulderHalfWidth * data.height;
            hipHalfWidth = data.hipHalfWidth * data.height;

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
                poses[pose.name] = new BodyPose(pose.shoulder, pose.elbow, pose.hip, pose.knee, pose.handsIn);

            float range = BodyPose.MaxDifference(ShapePose(BodyShape.Straight), ShapePose(BodyShape.Tuck));
            JointSpeed = range / ShapeChangeTime;
            HandsInSpeed = 1f / ShapeChangeTime;

            Catch = new CatchLimits(data.@catch.maxReach, data.@catch.reachTime, data.@catch.inputBuffer,
                data.@catch.maxImpactSpeed, data.@catch.rearmDistance);

            TwistSpinUpTime = data.twist.spinUpTime;
            TwistStopTime = data.twist.stopTime;
            float maxTwistRate = data.twist.maxTurnsPerSecond * 2f * Mathf.PI;
            MaxTwistMomentum = maxTwistRate * Evaluate(Pose("twist")).TwistInertia;
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

            // Distance of each segment from the body's midline (the plane of movement is the middle of the body).
            float forearmSide = shoulderHalfWidth * (1f - 0.5f * pose.HandsIn);
            float handSide = shoulderHalfWidth * (1f - pose.HandsIn);

            // Centre of mass and moments of inertia: sum over segments of m (distance² + own term).
            var accumulator = new InertiaAccumulator();
            accumulator.Add(trunk, hip, up, 0f);
            accumulator.Add(head, shoulder, up, 0f);
            accumulator.Add(upperArm, shoulder, upperArmDir, shoulderHalfWidth);
            accumulator.Add(forearm, elbow, forearmDir, forearmSide);
            accumulator.Add(hand, wrist, forearmDir, handSide);
            accumulator.Add(thigh, hip, thighDir, hipHalfWidth);
            accumulator.Add(shank, knee, shankDir, hipHalfWidth);
            accumulator.Add(foot, ankle, shankDir, hipHalfWidth);

            Vector2 centreOfMass = accumulator.CentreOfMass;
            return new BodyGeometry(centreOfMass, accumulator.SaltoInertiaAbout(centreOfMass),
                accumulator.TwistInertiaAbout(centreOfMass), shoulder, headTop, elbow, grip, knee, sole);
        }

        private readonly struct Segment
        {
            public float Length { get; }
            public float Radius { get; }
            public float Mass { get; }              // fraction of body mass, both sides for paired segments
            public float ComAlong { get; }
            public float OwnSaltoInertia { get; }   // m k² about its own centre, across the segment
            public float OwnAxialInertia { get; }   // ½ m r² about its own length (a cylinder)

            public Segment(SegmentEntry entry, float height)
            {
                Length = entry.length * height;
                Radius = entry.radius * height;
                Mass = entry.mass * (entry.paired ? 2f : 1f);
                ComAlong = entry.com;
                float k = entry.gyration * Length;
                OwnSaltoInertia = Mass * k * k;
                OwnAxialInertia = 0.5f * Mass * Radius * Radius;
            }
        }

        // Collects segment masses so the centre of mass is known before the parallel-axis sums.
        private struct InertiaAccumulator
        {
            private Vector2 weightedSum;
            private float mass;
            private float ownSalto;
            private float secondMoment;     // Σ m |p|², about the hip (salto: distances in the plane)
            private float ownTwist;
            private float sideMoment;       // Σ m z², distance from the midline (twist)
            private float forwardMoment;    // Σ m x², about the trunk line (twist)

            public void Add(Segment segment, Vector2 start, Vector2 direction, float side)
            {
                Vector2 p = start + direction * (segment.Length * segment.ComAlong);
                weightedSum += p * segment.Mass;
                mass += segment.Mass;
                ownSalto += segment.OwnSaltoInertia;
                secondMoment += segment.Mass * p.sqrMagnitude;

                // About the long axis a segment along the trunk only turns about its own length (axial),
                // and one across the trunk turns end over end (as in a salto). direction.y = cos of the angle between them.
                float along = direction.y * direction.y;
                ownTwist += segment.OwnAxialInertia * along + segment.OwnSaltoInertia * (1f - along);
                sideMoment += segment.Mass * side * side;
                forwardMoment += segment.Mass * p.x * p.x;
            }

            public Vector2 CentreOfMass => weightedSum / mass;

            // Σ m |p - c|² = Σ m |p|² - M |c|²   (parallel-axis theorem)
            public float SaltoInertiaAbout(Vector2 c) => ownSalto + secondMoment - mass * c.sqrMagnitude;

            // The long axis runs through the centre of mass along the trunk: distance² = (x - c.x)² + z².
            public float TwistInertiaAbout(Vector2 c) => ownTwist + forwardMoment - mass * c.x * c.x + sideMoment;
        }

        // Shapes of body.json, only used while loading.
        [Serializable]
        private class BodyFile
        {
            public float height;
            public float gripAlongHand;
            public float shapeChangeTime;
            public float shoulderHalfWidth;
            public float hipHalfWidth;
            public TwistEntry twist;
            public CatchEntry @catch;   // "catch" is a C# keyword, so the field name needs @
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
            public float radius;
            public bool paired;
        }

        [Serializable]
        private class CatchEntry
        {
            public float maxReach;
            public float reachTime;
            public float inputBuffer;
            public float maxImpactSpeed;
            public float rearmDistance;
        }

        [Serializable]
        private class TwistEntry
        {
            public float maxTurnsPerSecond;
            public float spinUpTime;
            public float stopTime;
        }

        [Serializable]
        private class PoseEntry
        {
            public string name;
            public float shoulder;
            public float elbow;
            public float hip;
            public float knee;
            public float handsIn;
        }
    }
}
