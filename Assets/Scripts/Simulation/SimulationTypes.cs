using System;
using UnityEngine;

namespace GymnastGame.Simulation
{
    public enum GymnastPhase
    {
        OnBar,      // swinging around the bar
        Flying,     // released, in the air
        Touchdown   // feet (or body) reached the ground; the simulation stops here
    }

    /// <summary>What the player asked for during one simulation step.</summary>
    public readonly struct GymnastCommands
    {
        public bool Pump { get; }
        public bool Release { get; }

        public GymnastCommands(bool pump, bool release)
        {
            Pump = pump;
            Release = release;
        }

        public static GymnastCommands None => new GymnastCommands(false, false);
    }

    /// <summary>
    /// Where the body is in the plane of movement. Position is the centre of mass;
    /// BodyAngleDegrees is the rotation about the bar axis (0 = head up, counter-clockwise positive).
    /// Crouch (0..1) is only used by the landing animation.
    /// </summary>
    public readonly struct GymnastPose
    {
        public Vector2 Position { get; }
        public float BodyAngleDegrees { get; }
        public float Crouch { get; }

        public GymnastPose(Vector2 position, float bodyAngleDegrees, float crouch = 0f)
        {
            Position = position;
            BodyAngleDegrees = bodyAngleDegrees;
            Crouch = crouch;
        }
    }

    /// <summary>The moment the body first reaches the ground after a release.</summary>
    public readonly struct TouchdownInfo
    {
        public Vector2 Position { get; }
        public Vector2 Velocity { get; }
        public float BodyAngleDegrees { get; }      // -180..180, 0 = perfectly upright
        public float AirRotationDegrees { get; }    // total rotation since release, signed

        public TouchdownInfo(Vector2 position, Vector2 velocity, float bodyAngleDegrees, float airRotationDegrees)
        {
            Position = position;
            Velocity = velocity;
            BodyAngleDegrees = bodyAngleDegrees;
            AirRotationDegrees = airRotationDegrees;
        }
    }

    /// <summary>Tuning values, edited in the Inspector of GymnastController.</summary>
    [Serializable]
    public class SimulationSettings
    {
        [Header("Swing")]
        [SerializeField] private float gravity = 9.81f;
        [SerializeField] private float armLength = 1.1f;        // bar to the centre of mass
        [SerializeField] private float damping = 0.15f;         // air/friction loss per second
        [SerializeField] private float startAngle = 30f;        // degrees, so the swing starts moving

        [Header("Pump (tap)")]
        [SerializeField] private float pumpStrength = 1.6f;     // angular speed added by a perfect tap (rad/s)
        [SerializeField] private float pumpWindow = 25f;        // degrees either side of the bottom where a tap counts
        [SerializeField] private float pumpCooldown = 0.35f;    // seconds; one tap per pass through the bottom

        [Header("Body and ground")]
        [SerializeField] private float groundY = 0f;            // top surface of the landing mat
        [SerializeField] private float halfHeight = 0.85f;      // centre of the body to the feet
        [SerializeField] private float bodyRadius = 0.175f;     // half the body's thickness

        public float Gravity => gravity;
        public float ArmLength => armLength;
        public float Damping => damping;
        public float StartAngle => startAngle;
        public float PumpStrength => pumpStrength;
        public float PumpWindow => pumpWindow;
        public float PumpCooldown => pumpCooldown;
        public float GroundY => groundY;
        public float HalfHeight => halfHeight;
        public float BodyRadius => bodyRadius;

        public SimulationSettings() { }

        /// <summary>Copy with a different damping and start angle; used by tests.</summary>
        public SimulationSettings With(float? damping = null, float? startAngle = null)
        {
            var copy = (SimulationSettings)MemberwiseClone();
            if (damping.HasValue) copy.damping = damping.Value;
            if (startAngle.HasValue) copy.startAngle = startAngle.Value;
            return copy;
        }
    }
}
