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
        public bool Release { get; }
        public BodyShape Shape { get; }     // used in the air

        public GymnastCommands(bool pump, bool release, BodyShape shape = BodyShape.Straight)
        {
            Pump = pump;
            Release = release;
            Shape = shape;
        }

        public static GymnastCommands None => new GymnastCommands(false, false);
    }

    /// <summary>
    /// Where the body is in the plane of movement. CentreOfMass is in world space;
    /// BodyAngleDegrees is the rotation of the body frame (trunk) about the bar axis,
    /// 0 = head up, counter-clockwise positive. Joints gives the shape.
    /// </summary>
    public readonly struct GymnastPose
    {
        public Vector2 CentreOfMass { get; }
        public float BodyAngleDegrees { get; }
        public BodyPose Joints { get; }

        public GymnastPose(Vector2 centreOfMass, float bodyAngleDegrees, BodyPose joints)
        {
            CentreOfMass = centreOfMass;
            BodyAngleDegrees = bodyAngleDegrees;
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

        public TouchdownInfo(GymnastPose pose, Vector2 velocity, Vector2 sole, float landingAngleDegrees,
            float airRotationDegrees)
        {
            Pose = pose;
            Velocity = velocity;
            Sole = sole;
            LandingAngleDegrees = landingAngleDegrees;
            AirRotationDegrees = airRotationDegrees;
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
