using System;
using UnityEngine;

namespace GymnastGame.Simulation
{
    public enum GymnastPhase
    {
        OnBar,      // swinging around the bar
        Flying,     // released, in the air
        Touchdown   // the body reached the ground; the simulation stops here
    }

    /// <summary>What the player asked for during one simulation step.</summary>
    public readonly struct GymnastCommands
    {
        public bool Pump { get; }
        public bool ReleaseOrCatch { get; } // on the bar: let go; in the air: try to catch the bar
        public BodyShape Shape { get; }     // used in the air
        public bool Twist { get; }          // held in the air
        public bool CatchHeld { get; }      // Enter held in the air: catch as soon as the hands are within reach

        public GymnastCommands(bool pump, bool releaseOrCatch, BodyShape shape = BodyShape.Straight, bool twist = false,
            bool catchHeld = false)
        {
            Pump = pump;
            ReleaseOrCatch = releaseOrCatch;
            Shape = shape;
            Twist = twist;
            CatchHeld = catchHeld;
        }

        public static GymnastCommands None => new GymnastCommands(false, false);
    }

    /// <summary>
    /// Where the body is in the plane of movement. CentreOfMass is in world space;
    /// BodyAngleDegrees is the rotation of the body frame (trunk) about the bar axis,
    /// 0 = head up, counter-clockwise positive. TwistDegrees is the rotation about the body's long axis
    /// (0 = chest facing +x). Joints gives the shape.
    /// </summary>
    public readonly struct GymnastPose
    {
        public Vector2 CentreOfMass { get; }
        public float BodyAngleDegrees { get; }
        public float TwistDegrees { get; }
        public BodyPose Joints { get; }

        public GymnastPose(Vector2 centreOfMass, float bodyAngleDegrees, BodyPose joints, float twistDegrees = 0f)
        {
            CentreOfMass = centreOfMass;
            BodyAngleDegrees = bodyAngleDegrees;
            TwistDegrees = twistDegrees;
            Joints = joints;
        }
    }

    /// <summary>The moment the body first reaches the ground after a release.</summary>
    public readonly struct TouchdownInfo
    {
        public GymnastPose Pose { get; }
        public Vector2 Velocity { get; }
        public Vector2 Sole { get; }                // world position of the feet
        public float LandingAngleDegrees { get; }   // feet → centre of mass line against vertical, -180..180
        public float AirRotationDegrees { get; }    // total body rotation since release, signed
        public float FlightTwistDegrees { get; }    // twist since release, signed

        /// <summary>Completed half twists in this flight, counted to the nearest half twist (so 340° counts as two).</summary>
        public int HalfTwists => TwistMath.NearestHalfTwists(FlightTwistDegrees);

        /// <summary>How far the twist is from that nearest half twist, 0..90 degrees.</summary>
        public float TwistErrorDegrees => TwistMath.ErrorFromHalfTwist(FlightTwistDegrees);

        public TouchdownInfo(GymnastPose pose, Vector2 velocity, Vector2 sole, float landingAngleDegrees,
            float airRotationDegrees, float flightTwistDegrees = 0f)
        {
            Pose = pose;
            Velocity = velocity;
            Sole = sole;
            LandingAngleDegrees = landingAngleDegrees;
            AirRotationDegrees = airRotationDegrees;
            FlightTwistDegrees = flightTwistDegrees;
        }
    }

    /// <summary>What makes a catch impossible right now. None means the hands can catch the bar.</summary>
    public enum CatchBlocker
    {
        None,
        TooEarly,       // the hands have not moved away from the bar yet after the release
        TooFar,         // the hands are further from the bar than the maximum reach
        TooFast         // moving too fast towards or away from the bar: the hands would slip
    }

    /// <summary>
    /// How clean a catch is (or would be now). The simulation only measures; the regrasp rules in
    /// deductions.json turn these into deductions.
    /// </summary>
    public readonly struct CatchQuality
    {
        public float Reach { get; }             // metres between the hands and the bar
        public float LegBend { get; }           // degrees the hips or knees are still bent (tuck or pike)
        public float ArmDeviation { get; }      // degrees the arms are away from straight overhead
        public float TwistError { get; }        // degrees from the nearest half twist
        public float ImpactSpeed { get; }       // m/s towards or away from the bar

        public CatchQuality(float reach, float legBend, float armDeviation, float twistError, float impactSpeed)
        {
            Reach = reach;
            LegBend = legBend;
            ArmDeviation = armDeviation;
            TwistError = twistError;
            ImpactSpeed = impactSpeed;
        }
    }

    /// <summary>Whether the hands could catch the bar at this moment, and how clean it would be.</summary>
    public readonly struct CatchStatus
    {
        public bool InFlight { get; }
        public CatchBlocker Blocker { get; }
        public CatchQuality Quality { get; }

        public bool CanCatch => InFlight && Blocker == CatchBlocker.None;

        public CatchStatus(bool inFlight, CatchBlocker blocker, CatchQuality quality)
        {
            InFlight = inFlight;
            Blocker = blocker;
            Quality = quality;
        }

        public static CatchStatus NotFlying => new CatchStatus(false, CatchBlocker.TooFar,
            new CatchQuality(float.PositiveInfinity, 0f, 0f, 0f, 0f));
    }

    public enum FlightEnd
    {
        Caught,     // the hands caught the bar again
        Landed      // ended on the mat (a dismount, or a missed or slipped catch)
    }

    /// <summary>
    /// What happened in one flight, from release to catch or landing. Element recognition (step 7)
    /// turns these into element ids; until then the placeholder score adds up saltos and twists.
    /// </summary>
    public readonly struct FlightRecord
    {
        public float ReleaseAngleDegrees { get; }       // swing angle at release, -180..180
        public float ReleaseAngularVelocity { get; }    // rad/s, the swing direction is its sign
        public float RotationDegrees { get; }           // body rotation from release to the end, signed
        public float TwistDegrees { get; }              // twist from release to the end, signed
        public BodyShape MainShape { get; }             // the shape held longest
        public bool OverBar { get; }                    // the centre of mass crossed above the bar
        public FlightEnd End { get; }
        public bool Slipped { get; }                    // the hands reached the bar but could not hold it
        public CatchQuality Catch { get; }              // how clean the catch was (caught flights only)
        public float Duration { get; }                  // seconds in the air

        public float CatchImpactSpeed => Catch.ImpactSpeed;

        public FlightRecord(float releaseAngleDegrees, float releaseAngularVelocity, float rotationDegrees,
            float twistDegrees, BodyShape mainShape, bool overBar, FlightEnd end, bool slipped,
            CatchQuality catchQuality, float duration)
        {
            ReleaseAngleDegrees = releaseAngleDegrees;
            ReleaseAngularVelocity = releaseAngularVelocity;
            RotationDegrees = rotationDegrees;
            TwistDegrees = twistDegrees;
            MainShape = mainShape;
            OverBar = overBar;
            End = end;
            Slipped = slipped;
            Catch = catchQuality;
            Duration = duration;
        }

        /// <summary>
        /// Full saltos. A caught flight starts and ends with the body along the radius, so the extra
        /// rotation between release and catch angle is not a salto and the count is rounded; a landing
        /// counts only completed turns.
        /// </summary>
        public int Saltos => End == FlightEnd.Caught
            ? Mathf.RoundToInt(Mathf.Abs(RotationDegrees) / 360f)
            : Mathf.FloorToInt(Mathf.Abs(RotationDegrees) / 360f);

        public int HalfTwists => TwistMath.NearestHalfTwists(TwistDegrees);
        public int FullTwists => HalfTwists / 2;
    }

    /// <summary>
    /// Twist bookkeeping. Until elements are recognised, the intended twist is taken to be the nearest
    /// half twist, because a landing must face forwards or backwards.
    /// </summary>
    public static class TwistMath
    {
        public static int NearestHalfTwists(float twistDegrees) => Mathf.RoundToInt(Mathf.Abs(twistDegrees) / 180f);

        public static float ErrorFromHalfTwist(float twistDegrees)
        {
            float nearest = Mathf.Round(twistDegrees / 180f) * 180f;
            return Mathf.Abs(twistDegrees - nearest);
        }
    }

    /// <summary>Tuning values, edited in the Inspector of GymnastController. Body sizes come from body.json.</summary>
    [Serializable]
    public class SimulationSettings
    {
        [Header("Swing")]
        [SerializeField] private float gravity = 9.81f;
        [SerializeField] private float damping = 0.15f;         // air/friction loss per second
        [SerializeField] private float startAngle = 30f;        // degrees, so the swing starts moving

        [Header("Pump (tap)")]
        [SerializeField] private float pumpStrength = 1.6f;     // angular speed added by a perfect tap (rad/s)
        [SerializeField] private float pumpWindow = 25f;        // degrees either side of the bottom where a tap counts
        [SerializeField] private float pumpCooldown = 0.35f;    // seconds; one tap per pass through the bottom

        [Header("Ground")]
        [SerializeField] private float groundY = 0f;            // top surface of the landing mat

        public float Gravity => gravity;
        public float Damping => damping;
        public float StartAngle => startAngle;
        public float PumpStrength => pumpStrength;
        public float PumpWindow => pumpWindow;
        public float PumpCooldown => pumpCooldown;
        public float GroundY => groundY;

        /// <summary>Copy with a different damping and start angle; used by tests.</summary>
        public SimulationSettings With(float? damping = null, float? startAngle = null)
        {
            var copy = (SimulationSettings)MemberwiseClone();
            if (damping.HasValue) copy.damping = damping.Value;
            if (startAngle.HasValue) copy.startAngle = startAngle.Value;
            return copy;
        }
    }

    /// <summary>Rotations in the plane of movement.</summary>
    public static class PlaneMath
    {
        /// <summary>Rotates v counter-clockwise by the given angle in radians.</summary>
        public static Vector2 Rotate(Vector2 v, float radians)
        {
            float sin = Mathf.Sin(radians), cos = Mathf.Cos(radians);
            return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
        }

        /// <summary>Angle of v against straight up, counter-clockwise positive, in degrees (-180..180).</summary>
        public static float AngleFromVertical(Vector2 v) => Mathf.Atan2(-v.x, v.y) * Mathf.Rad2Deg;
    }
}
