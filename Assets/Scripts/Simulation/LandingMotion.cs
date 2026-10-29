using System;
using UnityEngine;

namespace GymnastGame.Simulation
{
    public enum LandingKind
    {
        Stick,  // knees and hips give, then the body straightens up over the feet
        Step,   // one step in the direction of travel while straightening up
        Fall    // tips over and ends lying on the mat
    }

    /// <summary>Tuning values for the landing motion, edited in the Inspector of GymnastController.</summary>
    [Serializable]
    public class LandingSettings
    {
        [SerializeField] private float stickDuration = 0.45f;
        [SerializeField] private float stickLean = -20f;        // trunk leans forward in the crouch (degrees)
        [SerializeField] private float stepDuration = 0.45f;
        [SerializeField] private float stepDistance = 0.3f;     // metres, about one foot
        [SerializeField] private float fallDuration = 0.6f;
        [SerializeField] private float lyingHeight = 0.12f;     // centre of mass above the mat when lying

        public float StickDuration => stickDuration;
        public float StickLean => stickLean;
        public float StepDuration => stepDuration;
        public float StepDistance => stepDistance;
        public float FallDuration => fallDuration;
        public float LyingHeight => lyingHeight;
    }

    /// <summary>
    /// The body's motion from touchdown to its final pose, as plain C# so it can be tested.
    /// Call Advance every frame; Pose is the pose to draw, and Finished is raised once,
    /// when the whole duration has passed.
    /// The body angle from the simulation is not wrapped (it still counts the giant swings and saltos),
    /// so the motion starts from the same angle wrapped to -180..180 and turns the short way.
    /// </summary>
    public class LandingMotion
    {
        private const float FeetSettleShare = 0.2f;    // share of the motion in which the feet settle onto the mat

        private readonly BodyModel body;
        private readonly LandingSettings settings;
        private readonly float groundY;
        private readonly TouchdownInfo touchdown;
        private readonly float startAngle;              // wrapped to -180..180

        public LandingKind Kind { get; }
        public float Duration { get; }
        public float Elapsed { get; private set; }
        public bool IsFinished { get; private set; }
        public GymnastPose Pose { get; private set; }

        public event Action Finished;

        public LandingMotion(BodyModel body, LandingSettings settings, float groundY, TouchdownInfo touchdown, LandingKind kind)
        {
            this.body = body;
            this.settings = settings;
            this.groundY = groundY;
            this.touchdown = touchdown;
            Kind = kind;
            startAngle = Mathf.DeltaAngle(0f, touchdown.Pose.BodyAngleDegrees);

            switch (kind)
            {
                case LandingKind.Stick: Duration = settings.StickDuration; break;
                case LandingKind.Step: Duration = settings.StepDuration; break;
                default: Duration = settings.FallDuration; break;
            }
            Pose = PoseAt(0f);
        }

        public void Advance(float dt)
        {
            if (IsFinished) return;

            Elapsed = Mathf.Min(Elapsed + dt, Duration);
            Pose = PoseAt(Elapsed / Duration);
            if (Elapsed >= Duration)
            {
                IsFinished = true;
                Finished?.Invoke();
            }
        }

        /// <summary>The pose at t from 0 (touchdown) to 1 (final pose).</summary>
        public GymnastPose PoseAt(float t)
        {
            t = Mathf.Clamp01(t);
            switch (Kind)
            {
                case LandingKind.Stick: return StickAt(t);
                case LandingKind.Step: return StepAt(t);
                default: return FallAt(t);
            }
        }

        private GymnastPose StickAt(float t)
        {
            var crouch = body.Pose("landingCrouch");
            var stand = body.Pose("stand");

            // First half: from the touchdown shape into the crouch. Second half: up to standing.
            float down = Mathf.Clamp01(t * 2f), up = Mathf.Clamp01(t * 2f - 1f);
            var joints = t < 0.5f ? BodyPose.Lerp(touchdown.Pose.Joints, crouch, down) : BodyPose.Lerp(crouch, stand, up);
            float angle = t < 0.5f ? Mathf.Lerp(startAngle, settings.StickLean, down) : Mathf.Lerp(settings.StickLean, 0f, up);
            return StandingOn(touchdown.Sole.x, t, angle, joints);
        }

        private GymnastPose StepAt(float t)
        {
            float eased = Mathf.SmoothStep(0f, 1f, t);
            float direction = Mathf.Sign(touchdown.Velocity.x == 0f ? 1f : touchdown.Velocity.x);
            float soleX = touchdown.Sole.x + direction * settings.StepDistance * eased;
            return StandingOn(soleX, t, Mathf.Lerp(startAngle, 0f, eased),
                BodyPose.Lerp(touchdown.Pose.Joints, body.Pose("stand"), eased));
        }

        // Tips over to the side the body was already leaning, and ends lying straight with the feet where they landed.
        private GymnastPose FallAt(float t)
        {
            var straight = body.ShapePose(BodyShape.Straight);
            float side = Mathf.Sign(touchdown.LandingAngleDegrees == 0f ? 1f : touchdown.LandingAngleDegrees);
            float lyingAngle = startAngle + Mathf.DeltaAngle(startAngle, side * 90f);

            var lying = body.Evaluate(straight);
            Vector2 soleToCentre = PlaneMath.Rotate(lying.CentreOfMass - lying.Sole, side * 90f * Mathf.Deg2Rad);
            var lyingCentre = new Vector2(touchdown.Sole.x + soleToCentre.x, groundY + settings.LyingHeight);

            float eased = t * t;    // speeds up like a real topple
            return new GymnastPose(Vector2.Lerp(touchdown.Pose.CentreOfMass, lyingCentre, eased),
                Mathf.Lerp(startAngle, lyingAngle, eased),
                BodyPose.Lerp(touchdown.Pose.Joints, straight, eased));
        }

        /// <summary>
        /// The pose with the feet at soleX, the trunk at the given angle and the given shape. The feet start
        /// where they touched down and settle onto the mat early in the motion, so there is no jump.
        /// </summary>
        private GymnastPose StandingOn(float soleX, float t, float bodyAngleDegrees, BodyPose joints)
        {
            var geometry = body.Evaluate(joints);
            Vector2 soleToCentre = PlaneMath.Rotate(geometry.CentreOfMass - geometry.Sole, bodyAngleDegrees * Mathf.Deg2Rad);
            float soleY = Mathf.Lerp(touchdown.Sole.y, groundY, Mathf.Clamp01(t / FeetSettleShare));
            return new GymnastPose(new Vector2(soleX, soleY) + soleToCentre, bodyAngleDegrees, joints);
        }
    }
}
