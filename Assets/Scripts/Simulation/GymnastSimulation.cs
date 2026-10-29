using System;
using System.Collections.Generic;
using UnityEngine;

namespace GymnastGame.Simulation
{
    /// <summary>
    /// The gymnast's motion in the plane of the bar swing, as plain C# so it can be unit-tested.
    /// On the bar it is a pendulum (hands on the bar, straight body) that taps (pumps) speed up.
    /// After the release the centre of mass flies as a projectile, and the body keeps its angular
    /// momentum L: changing shape changes the moment of inertia I, so the rotation speed is ω = L / I.
    /// Holding twist builds up twist momentum H about the body's long axis; the twist rate is H / I_long,
    /// so a straight body twists fast and a pike or tuck slowly. The salto stays in the plane: twist and
    /// salto are kept independent (a simplification).
    /// Near the bar the hands can catch it again: the flight becomes a swing with the speed around the bar, and the
    /// body reaches for the bar over a short time. A catch is only impossible when the hands are too far or hit the
    /// bar too hard; how clean it was is measured (CatchQuality) and judged by the rules.
    /// Every flight is kept as a FlightRecord for element recognition.
    /// It keeps its own clock, so the same commands always give the same motion.
    /// Call Step once per fixed time step.
    /// </summary>
    public class GymnastSimulation
    {
        private static readonly BodyShape[] Shapes = { BodyShape.Straight, BodyShape.Pike, BodyShape.Tuck };

        private readonly SimulationSettings settings;
        private readonly BodyModel body;
        private readonly Vector2 barPosition;
        private readonly BodyPose hangPose;
        private readonly Vector2 gripToCentreOfMass;    // body frame, in the hanging pose
        private readonly List<FlightRecord> flights = new List<FlightRecord>();

        private float time;
        private float lastPumpTime = float.NegativeInfinity;

        // On the bar
        private float angle;            // radians; 0 = hanging straight down
        private float angularVelocity;  // radians per second
        private int facing = 1;         // +1 = chest towards +x when hanging, -1 = turned round (twisted ½)
        private GymnastPose reachFrom;  // the flight pose at the catch; the drawn pose blends from it to the hang
        private float reachElapsed = float.PositiveInfinity;

        // In the air
        private Vector2 position;       // centre of mass
        private Vector2 velocity;
        private float bodyAngle;        // radians, same convention as angle
        private float angularMomentum;  // per kilogram: L = I ω
        private float airRotation;      // radians since release
        private BodyPose joints;
        private float twistAngle;       // radians about the long axis, not wrapped
        private float twistMomentum;    // per kilogram: H = I_long ω_twist

        // The flight in progress
        private float flightStartTime;
        private float releaseAngleDegrees;
        private float releaseAngularVelocity;
        private float twistAtRelease;
        private readonly float[] shapeTime = new float[Shapes.Length];  // seconds spent nearest to each shape
        private bool overBar;
        private bool catchArmed;                            // the hands have left the catch window once
        private float lastCatchPress = float.NegativeInfinity;
        private bool catchPressPending;                     // Enter was pressed and has not caught or failed yet
        private bool catchHeld;                             // Enter is held down this step
        private CatchBlocker pressBlocker;                  // the reason to report if that press fails
        private bool slipped;

        public GymnastPhase Phase { get; private set; } = GymnastPhase.OnBar;

        /// <summary>Distance from the hands to the centre of mass in the hanging pose.</summary>
        public float PendulumLength { get; }

        /// <summary>Swing angle in degrees, wrapped to -180..180 (0 = bottom, ±180 = handstand on top).</summary>
        public float AngleDegrees => Mathf.DeltaAngle(0f, angle * Mathf.Rad2Deg);
        public float AngularVelocity => angularVelocity;
        public Vector2 Velocity => velocity;
        public int Facing => facing;

        /// <summary>Body rotation speed in the air (rad/s): ω = L / I for the current shape.</summary>
        public float BodyAngularVelocity { get; private set; }
        public float AngularMomentum => angularMomentum;

        /// <summary>Twist rate in the air (rad/s): H / I_long for the current shape.</summary>
        public float TwistAngularVelocity { get; private set; }
        public float TwistMomentum => twistMomentum;

        /// <summary>Quality (0..1) of the most recent tap: 1 = exactly at the bottom.</summary>
        public float LastPumpQuality { get; private set; }
        public int MissCount { get; private set; }
        public int FullTurns { get; private set; }
        public TouchdownInfo Touchdown { get; private set; }

        /// <summary>Every finished flight in order: caught releases and the final dismount.</summary>
        public IReadOnlyList<FlightRecord> Flights => flights;

        /// <summary>Whether the hands could catch the bar right now, updated every step in the air.</summary>
        public CatchStatus CatchStatus { get; private set; } = CatchStatus.NotFlying;

        public event Action<float> Pumped;              // quality, 0 = miss
        public event Action<int> FullTurnCompleted;     // total turns so far
        public event Action Released;
        public event Action<FlightRecord> Regrasped;    // caught the bar again
        public event Action CatchSlipped;               // reached the bar but could not hold it
        public event Action<CatchBlocker> CatchFailed;  // Enter was pressed but no catch followed in time
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
                if (Phase != GymnastPhase.OnBar)
                    return new GymnastPose(position, bodyAngle * Mathf.Rad2Deg, joints, twistAngle * Mathf.Rad2Deg);

                var hanging = new GymnastPose(barPosition + RadiusVector(), angle * Mathf.Rad2Deg, joints, FacingTwist());
                float reachTime = body.Catch.ReachTime;
                if (reachElapsed >= reachTime) return hanging;

                // Just after a catch: the body reaches for the bar, from the flight pose to the hanging one,
                // turning the short way.
                float t = Mathf.SmoothStep(0f, 1f, reachElapsed / reachTime);
                float fromAngle = hanging.BodyAngleDegrees + Mathf.DeltaAngle(hanging.BodyAngleDegrees, reachFrom.BodyAngleDegrees);
                float fromTwist = hanging.TwistDegrees + Mathf.DeltaAngle(hanging.TwistDegrees, reachFrom.TwistDegrees);
                return new GymnastPose(Vector2.Lerp(reachFrom.CentreOfMass, hanging.CentreOfMass, t),
                    Mathf.Lerp(fromAngle, hanging.BodyAngleDegrees, t), joints, Mathf.Lerp(fromTwist, hanging.TwistDegrees, t));
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
                if (commands.ReleaseOrCatch) Release();
            }
            else if (commands.ReleaseOrCatch)
            {
                lastCatchPress = time;
                catchPressPending = true;
                pressBlocker = CatchBlocker.TooFar;
            }

            catchHeld = commands.CatchHeld;

            if (Phase == GymnastPhase.OnBar) StepSwing(dt);
            else StepFlight(dt, commands.Shape, commands.Twist);
        }

        /// <summary>
        /// Starts a flight from a given state, without a swing before it, as if the hands had already left the
        /// bar. Only for tests, so they can set up exact catches (the tests assembly can see internal members,
        /// see AssemblyInfo.cs).
        /// </summary>
        internal void StartFlightForTests(Vector2 centreOfMass, Vector2 flightVelocity, float bodyAngleDegrees,
            float bodyAngularVelocity, float twistDegrees = 0f, BodyPose? startJoints = null)
        {
            position = centreOfMass;
            velocity = flightVelocity;
            bodyAngle = bodyAngleDegrees * Mathf.Deg2Rad;
            joints = startJoints ?? hangPose;
            angularMomentum = body.Evaluate(joints).Inertia * bodyAngularVelocity;
            BodyAngularVelocity = bodyAngularVelocity;
            twistAngle = twistDegrees * Mathf.Deg2Rad;
            twistMomentum = 0f;
            BeginFlightRecord(bodyAngleDegrees, bodyAngularVelocity);
            catchArmed = true;
            Phase = GymnastPhase.Flying;
        }

        private void StepSwing(float dt)
        {
            float before = AngleDegrees;

            // Pendulum equation: angular acceleration = -(g / L) * sin(angle)
            float angularAcceleration = -(settings.Gravity / PendulumLength) * Mathf.Sin(angle);
            angularVelocity += angularAcceleration * dt;
            angularVelocity *= 1f - settings.Damping * dt;
            angle += angularVelocity * dt;

            // After a catch the body reaches for the bar and opens into the hanging shape.
            reachElapsed += dt;
            joints = joints.MoveTowards(hangPose, body.JointSpeed * dt, body.HandsInSpeed * dt);

            // A giant swing counts each time the gymnast passes over the top of the bar,
            // where the wrapped angle jumps between +180 and -180.
            if (Mathf.Abs(AngleDegrees - before) > 180f)
            {
                FullTurns++;
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
            twistAngle = FacingTwist() * Mathf.Deg2Rad;
            twistMomentum = 0f;

            // The body leaves the bar rotating at the swing speed: L = I ω.
            BodyAngularVelocity = angularVelocity;
            angularMomentum = body.Evaluate(joints).Inertia * angularVelocity;

            BeginFlightRecord(AngleDegrees, angularVelocity);
            Phase = GymnastPhase.Flying;
            Released?.Invoke();
        }

        private void BeginFlightRecord(float angleDegrees, float swingVelocity)
        {
            flightStartTime = time;
            releaseAngleDegrees = Mathf.DeltaAngle(0f, angleDegrees);
            releaseAngularVelocity = swingVelocity;
            twistAtRelease = twistAngle;
            airRotation = 0f;
            Array.Clear(shapeTime, 0, shapeTime.Length);
            overBar = false;
            catchArmed = false;
            slipped = false;
            lastCatchPress = float.NegativeInfinity;
            catchPressPending = false;
        }

        private void StepFlight(float dt, BodyShape shape, bool twist)
        {
            // The joints move towards the chosen shape at a fixed angular speed.
            // Twisting straight pulls the arms onto the chest, as gymnasts do.
            var target = twist && shape == BodyShape.Straight ? body.Pose("twist") : body.ShapePose(shape);
            joints = joints.MoveTowards(target, body.JointSpeed * dt, body.HandsInSpeed * dt);
            var geometry = body.Evaluate(joints);

            // Angular momentum is conserved in the air, so a smaller I spins faster.
            // Simplification: the joints' own motion does not turn the trunk.
            BodyAngularVelocity = angularMomentum / geometry.Inertia;

            // Twist momentum builds up while twist is held and fades when it is let go.
            float rate = body.MaxTwistMomentum / (twist ? body.TwistSpinUpTime : body.TwistStopTime);
            twistMomentum = Mathf.MoveTowards(twistMomentum, twist ? body.MaxTwistMomentum : 0f, rate * dt);
            TwistAngularVelocity = twistMomentum / geometry.TwistInertia;
            twistAngle += TwistAngularVelocity * dt;

            Vector2 previous = position;
            velocity.y -= settings.Gravity * dt;
            position += velocity * dt;

            float spinStep = BodyAngularVelocity * dt;
            bodyAngle += spinStep;
            airRotation += spinStep;

            RecordFlightStep(dt, previous);
            if (TryCatch(geometry))
                return;

            bool fallingDown = velocity.y < 0f;
            if (fallingDown && geometry.LowestY(position.y, bodyAngle, twistAngle) <= settings.GroundY)
            {
                Phase = GymnastPhase.Touchdown;
                CatchStatus = CatchStatus.NotFlying;
                // In the plane, a twisted body's forward offsets shrink by cos(twist).
                Vector2 sole = position + PlaneMath.Rotate(InPlane(geometry.Sole - geometry.CentreOfMass), bodyAngle);
                flights.Add(EndFlightRecord(FlightEnd.Landed, default));
                Touchdown = new TouchdownInfo(Pose, velocity, sole, PlaneMath.AngleFromVertical(position - sole),
                    airRotation * Mathf.Rad2Deg, (twistAngle - twistAtRelease) * Mathf.Rad2Deg);
                TouchedDown?.Invoke(Touchdown);
            }
        }

        private void RecordFlightStep(float dt, Vector2 previous)
        {
            shapeTime[(int)NearestShape(joints)] += dt;

            // Over the bar: the centre of mass passes the bar's vertical line while above it.
            bool crossedBarLine = (previous.x - barPosition.x) * (position.x - barPosition.x) <= 0f;
            if (crossedBarLine && position.y > barPosition.y)
                overBar = true;
        }

        /// <summary>
        /// Catches the bar if Enter was pressed recently (or now) and the catch is possible: the hands are within
        /// reach and not moving too fast towards or away from the bar. The body keeps its speed around the bar:
        /// ω = v_around / L, where v_around is the part of the velocity perpendicular to the bar → centre of mass
        /// line. The speed along that line is taken by the arms. Holding Enter catches as soon as the catch is
        /// possible. A press that does not lead to a catch within the input buffer reports why (CatchFailed);
        /// only holding the key reports nothing.
        /// </summary>
        private bool TryCatch(BodyGeometry geometry)
        {
            Vector2 r = position - barPosition;
            CatchStatus = EvaluateCatch(geometry, r);

            bool pressed = catchHeld || time - lastCatchPress <= body.Catch.InputBuffer;
            if (catchPressPending && CatchStatus.Blocker != CatchBlocker.TooFar)
                pressBlocker = CatchStatus.Blocker;     // a reason seen within reach says more than "too far"

            if (pressed && CatchStatus.Blocker == CatchBlocker.TooFast && !slipped)
            {
                slipped = true;
                catchPressPending = false;
                CatchSlipped?.Invoke();
                return false;
            }

            if (pressed && CatchStatus.CanCatch)
            {
                CatchBar(r, CatchStatus.Quality);
                return true;
            }

            if (catchPressPending && !pressed)
            {
                catchPressPending = false;
                CatchFailed?.Invoke(pressBlocker);
            }
            return false;
        }

        private CatchStatus EvaluateCatch(BodyGeometry geometry, Vector2 r)
        {
            var limits = body.Catch;
            Vector2 grip = position + PlaneMath.Rotate(InPlane(geometry.Grip - geometry.CentreOfMass), bodyAngle);
            float reach = (grip - barPosition).magnitude;
            if (reach > limits.RearmDistance)
                catchArmed = true;      // the hands must move away from the bar before they can catch it again

            float impact = Mathf.Abs(Vector2.Dot(velocity, r.normalized));
            float legBend = Mathf.Max(Mathf.Abs(joints.Hip - hangPose.Hip), Mathf.Abs(joints.Knee - hangPose.Knee));
            float armDeviation = Mathf.Max(Mathf.Max(Mathf.Abs(joints.Shoulder - hangPose.Shoulder),
                Mathf.Abs(joints.Elbow - hangPose.Elbow)), joints.HandsIn * 90f);   // hands on the chest ≈ 90°
            float twistError = TwistMath.ErrorFromHalfTwist(twistAngle * Mathf.Rad2Deg);
            var quality = new CatchQuality(reach, legBend, armDeviation, twistError, impact);

            CatchBlocker blocker;
            if (reach > limits.MaxReach) blocker = CatchBlocker.TooFar;
            else if (!catchArmed) blocker = CatchBlocker.TooEarly;
            else if (slipped || impact > limits.MaxImpactSpeed) blocker = CatchBlocker.TooFast;
            else blocker = CatchBlocker.None;
            return new CatchStatus(true, blocker, quality);
        }

        // Back on the bar: a swing with the same speed around it, facing set by the half twists.
        private void CatchBar(Vector2 r, CatchQuality quality)
        {
            reachFrom = Pose;
            reachElapsed = 0f;

            float speedAround = (r.x * velocity.y - r.y * velocity.x) / r.magnitude;    // (r × v) / |r|
            angle = Mathf.Atan2(r.x, -r.y);
            angularVelocity = speedAround / PendulumLength;
            facing = TwistMath.NearestHalfTwists(twistAngle * Mathf.Rad2Deg) % 2 == 0 ? 1 : -1;
            twistMomentum = 0f;
            catchPressPending = false;
            CatchStatus = CatchStatus.NotFlying;
            Phase = GymnastPhase.OnBar;

            var record = EndFlightRecord(FlightEnd.Caught, quality);
            flights.Add(record);
            Regrasped?.Invoke(record);
        }

        private FlightRecord EndFlightRecord(FlightEnd end, CatchQuality catchQuality)
        {
            int longest = 0;
            for (int i = 1; i < shapeTime.Length; i++)
                if (shapeTime[i] > shapeTime[longest]) longest = i;

            return new FlightRecord(releaseAngleDegrees, releaseAngularVelocity, airRotation * Mathf.Rad2Deg,
                (twistAngle - twistAtRelease) * Mathf.Rad2Deg, (BodyShape)longest, overBar, end, slipped,
                catchQuality, time - flightStartTime);
        }

        private BodyShape NearestShape(BodyPose pose)
        {
            var nearest = BodyShape.Straight;
            float smallest = float.MaxValue;
            foreach (var shape in Shapes)
            {
                float difference = BodyPose.MaxDifference(pose, body.ShapePose(shape));
                if (difference < smallest)
                {
                    smallest = difference;
                    nearest = shape;
                }
            }
            return nearest;
        }

        // A body-frame offset seen in the plane of movement: twisting turns its forward part out of the plane.
        private Vector2 InPlane(Vector2 offset) => new Vector2(offset.x * Mathf.Cos(twistAngle), offset.y);

        private float FacingTwist() => facing > 0 ? 0f : 180f;

        // From the hands on the bar to the centre of mass: the body hangs straight down at angle 0.
        private Vector2 RadiusVector() => PlaneMath.Rotate(gripToCentreOfMass, angle);
    }
}
