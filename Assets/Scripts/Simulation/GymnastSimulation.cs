using System;
using UnityEngine;

namespace GymnastGame.Simulation
{
    /// <summary>
    /// The gymnast's motion in the plane of the bar swing, as plain C# so it can be unit-tested.
    /// On the bar it is a pendulum (hands on the bar, straight body) that taps (pumps) speed up.
    /// After the release the centre of mass flies as a projectile, and the body keeps its angular
    /// momentum L: changing shape changes the moment of inertia I, so the rotation speed is ω = L / I.
    /// It keeps its own clock, so the same commands always give the same motion.
    /// Call Step once per fixed time step.
    /// </summary>
    public class GymnastSimulation
    {
        private readonly SimulationSettings settings;
        private readonly BodyModel body;
        private readonly Vector2 barPosition;
        private readonly BodyPose hangPose;
        private readonly Vector2 gripToCentreOfMass;    // body frame, in the hanging pose

        private float time;
        private float lastPumpTime = float.NegativeInfinity;

        // On the bar
        private float angle;            // radians, not wrapped; 0 = hanging straight down
        private float angularVelocity;  // radians per second

        // In the air
        private Vector2 position;       // centre of mass
        private Vector2 velocity;
        private float bodyAngle;        // radians, same convention as angle
        private float angularMomentum;  // per kilogram: L = I ω
        private float airRotation;      // radians since release
        private BodyPose joints;

        public GymnastPhase Phase { get; private set; } = GymnastPhase.OnBar;

        /// <summary>Distance from the hands to the centre of mass in the hanging pose.</summary>
        public float PendulumLength { get; }

        /// <summary>Swing angle in degrees, wrapped to -180..180 (0 = bottom, ±180 = handstand on top).</summary>
        public float AngleDegrees => Mathf.DeltaAngle(0f, angle * Mathf.Rad2Deg);
        public float AngularVelocity => angularVelocity;
        public Vector2 Velocity => velocity;

        /// <summary>Body rotation speed in the air (rad/s): ω = L / I for the current shape.</summary>
        public float BodyAngularVelocity { get; private set; }
        public float AngularMomentum => angularMomentum;

        /// <summary>Quality (0..1) of the most recent tap: 1 = exactly at the bottom.</summary>
        public float LastPumpQuality { get; private set; }
        public int MissCount { get; private set; }
        public int FullTurns { get; private set; }
        public TouchdownInfo Touchdown { get; private set; }

        public event Action<float> Pumped;              // quality, 0 = miss
        public event Action<int> FullTurnCompleted;     // total turns so far
        public event Action Released;
        public event Action<TouchdownInfo> TouchedDown;

        public GymnastSimulation(SimulationSettings settings, BodyModel body, Vector2 barPosition)
        {
            this.settings = settings;
            this.body = body;
            this.barPosition = barPosition;

            hangPose = body.ShapePose(BodyShape.Straight);
            var hang = body.Evaluate(hangPose);
            gripToCentreOfMass = hang.CentreOfMass - hang.Grip;
            PendulumLength = gripToCentreOfMass.magnitude;

            joints = hangPose;
            angle = settings.StartAngle * Mathf.Deg2Rad;
        }

        public GymnastPose Pose
        {
            get
            {
                if (Phase == GymnastPhase.OnBar)
                    return new GymnastPose(barPosition + RadiusVector(), angle * Mathf.Rad2Deg, hangPose);
                return new GymnastPose(position, bodyAngle * Mathf.Rad2Deg, joints);
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
            else StepFlight(dt, commands.Shape);
        }

        private void StepSwing(float dt)
        {
            // Pendulum equation: angular acceleration = -(g / L) * sin(angle)
            float angularAcceleration = -(settings.Gravity / PendulumLength) * Mathf.Sin(angle);
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
            joints = hangPose;

            // The body leaves the bar rotating at the swing speed: L = I ω.
            BodyAngularVelocity = angularVelocity;
            angularMomentum = body.Evaluate(joints).Inertia * angularVelocity;

            Phase = GymnastPhase.Flying;
            Released?.Invoke();
        }

        private void StepFlight(float dt, BodyShape shape)
        {
            // The joints move towards the chosen shape at a fixed angular speed.
            joints = joints.MoveTowards(body.ShapePose(shape), body.JointSpeed * dt);
            var geometry = body.Evaluate(joints);

            // Angular momentum is conserved in the air, so a smaller I spins faster.
            // Simplification: the joints' own motion does not turn the trunk.
            BodyAngularVelocity = angularMomentum / geometry.Inertia;

            velocity.y -= settings.Gravity * dt;
            position += velocity * dt;

            float spinStep = BodyAngularVelocity * dt;
            bodyAngle += spinStep;
            airRotation += spinStep;

            bool fallingDown = velocity.y < 0f;
            if (fallingDown && geometry.LowestY(position.y, bodyAngle) <= settings.GroundY)
            {
                Phase = GymnastPhase.Touchdown;
                Vector2 sole = position + PlaneMath.Rotate(geometry.Sole - geometry.CentreOfMass, bodyAngle);
                Touchdown = new TouchdownInfo(Pose, velocity, sole,
                    PlaneMath.AngleFromVertical(position - sole), airRotation * Mathf.Rad2Deg);
                TouchedDown?.Invoke(Touchdown);
            }
        }

        // From the hands on the bar to the centre of mass: the body hangs straight down at angle 0.
        private Vector2 RadiusVector() => PlaneMath.Rotate(gripToCentreOfMass, angle);
    }
}
