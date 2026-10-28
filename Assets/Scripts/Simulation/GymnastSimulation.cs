using System;
using UnityEngine;

namespace GymnastGame.Simulation
{
    /// <summary>
    /// The gymnast's motion in the plane of the bar swing, as plain C# so it can be unit-tested.
    /// On the bar it is a pendulum that taps (pumps) speed up; after the release it is a projectile
    /// that keeps rotating. It keeps its own clock, so the same commands always give the same motion.
    /// Call Step once per fixed time step.
    /// </summary>
    public class GymnastSimulation
    {
        private readonly SimulationSettings settings;
        private readonly Vector2 barPosition;

        private float time;
        private float lastPumpTime = float.NegativeInfinity;

        // On the bar
        private float angle;            // radians, not wrapped; 0 = hanging straight down
        private float angularVelocity;  // radians per second

        // In the air
        private Vector2 position;
        private Vector2 velocity;
        private float bodyAngle;        // radians, same convention as angle
        private float spin;             // radians per second, kept from the swing
        private float airRotation;      // radians since release

        public GymnastPhase Phase { get; private set; } = GymnastPhase.OnBar;

        /// <summary>Swing angle in degrees, wrapped to -180..180 (0 = bottom, ±180 = handstand on top).</summary>
        public float AngleDegrees => Mathf.DeltaAngle(0f, angle * Mathf.Rad2Deg);
        public float AngularVelocity => angularVelocity;
        public Vector2 Velocity => velocity;

        /// <summary>Quality (0..1) of the most recent tap: 1 = exactly at the bottom.</summary>
        public float LastPumpQuality { get; private set; }
        public int MissCount { get; private set; }
        public int FullTurns { get; private set; }
        public TouchdownInfo Touchdown { get; private set; }

        public event Action<float> Pumped;              // quality, 0 = miss
        public event Action<int> FullTurnCompleted;     // total turns so far
        public event Action Released;
        public event Action<TouchdownInfo> TouchedDown;

        public GymnastSimulation(SimulationSettings settings, Vector2 barPosition)
        {
            this.settings = settings;
            this.barPosition = barPosition;
            angle = settings.StartAngle * Mathf.Deg2Rad;
        }

        public GymnastPose Pose
        {
            get
            {
                if (Phase == GymnastPhase.OnBar)
                    return new GymnastPose(barPosition + RadiusVector(), angle * Mathf.Rad2Deg);
                return new GymnastPose(position, bodyAngle * Mathf.Rad2Deg);
            }
        }

        public void Step(float dt, GymnastCommands commands)
        {
            if (Phase == GymnastPhase.Touchdown)
                return;

            time += dt;

            if (Phase == GymnastPhase.OnBar)
            {
                if (commands.Pump) TryPump();
                if (commands.Release) Release();
            }

            if (Phase == GymnastPhase.OnBar) StepSwing(dt);
            else StepFlight(dt);
        }

        private void StepSwing(float dt)
        {
            // Pendulum equation: angular acceleration = -(g / L) * sin(angle)
            float angularAcceleration = -(settings.Gravity / settings.ArmLength) * Mathf.Sin(angle);
            angularVelocity += angularAcceleration * dt;
            angularVelocity *= 1f - settings.Damping * dt;
            angle += angularVelocity * dt;

            // angle is not wrapped, so it keeps growing with each turn.
            // A turn counts when the gymnast passes over the top of the bar: |angle| > PI, 3*PI, 5*PI ...
            int turns = Mathf.FloorToInt((Mathf.Abs(angle) + Mathf.PI) / (2f * Mathf.PI));
            if (turns > FullTurns)
            {
                FullTurns = turns;
                FullTurnCompleted?.Invoke(FullTurns);
            }
        }

        private void TryPump()
        {
            if (time - lastPumpTime < settings.PumpCooldown)
                return;
            lastPumpTime = time;

            float distanceFromBottom = Mathf.Abs(AngleDegrees);
            if (distanceFromBottom > settings.PumpWindow)
            {
                LastPumpQuality = 0f;
                MissCount++;
                Pumped?.Invoke(0f);
                return;
            }

            LastPumpQuality = 1f - distanceFromBottom / settings.PumpWindow;
            float direction = Mathf.Sign(angularVelocity == 0f ? 1f : angularVelocity);
            angularVelocity += direction * settings.PumpStrength * LastPumpQuality;
            Pumped?.Invoke(LastPumpQuality);
        }

        private void Release()
        {
            // On a circle, velocity is perpendicular to the radius: v = w x r.
            // In 2D with counter-clockwise positive rotation that is (-w * r.y, w * r.x).
            Vector2 r = RadiusVector();
            position = barPosition + r;
            velocity = new Vector2(-angularVelocity * r.y, angularVelocity * r.x);
            bodyAngle = angle;
            spin = angularVelocity;     // the body keeps rotating at the swing speed
            Phase = GymnastPhase.Flying;
            Released?.Invoke();
        }

        private void StepFlight(float dt)
        {
            velocity.y -= settings.Gravity * dt;
            position += velocity * dt;

            float spinStep = spin * dt;
            bodyAngle += spinStep;
            airRotation += spinStep;

            bool fallingDown = velocity.y < 0f;
            if (fallingDown && LowestPointY() <= settings.GroundY)
            {
                Phase = GymnastPhase.Touchdown;
                Touchdown = new TouchdownInfo(position, velocity,
                    Mathf.DeltaAngle(0f, bodyAngle * Mathf.Rad2Deg), airRotation * Mathf.Rad2Deg);
                TouchedDown?.Invoke(Touchdown);
            }
        }

        // From the bar to the centre of mass: the body hangs straight down at angle 0.
        private Vector2 RadiusVector() =>
            new Vector2(Mathf.Sin(angle), -Mathf.Cos(angle)) * settings.ArmLength;

        /// <summary>Lowest point of the body, whatever angle it is rotated to.</summary>
        private float LowestPointY()
        {
            // The body's "up" direction after rotating by bodyAngle counter-clockwise.
            float upX = -Mathf.Sin(bodyAngle);
            float upY = Mathf.Cos(bodyAngle);
            float verticalExtent = settings.HalfHeight * Mathf.Abs(upY) + settings.BodyRadius * Mathf.Abs(upX);
            return position.y - verticalExtent;
        }
    }
}
