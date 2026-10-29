using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace GymnastGame.Simulation.Tests
{
    /// <summary>
    /// The landing motion after touchdown: it must turn the short way to its final pose even though the
    /// simulation's body angle still counts every giant swing and salto, keep the feet planted, and
    /// report Finished once, only after the whole duration.
    /// </summary>
    public class LandingMotionTests
    {
        private const float FrameTime = 1f / 60f;
        private const float GroundY = 0f;

        private BodyModel body;
        private LandingSettings settings;

        [OneTimeSetUp]
        public void LoadBody()
        {
            body = BodyModel.FromJson(File.ReadAllText(Path.Combine(Application.dataPath, "Data", "Body", "body.json")));
            settings = new LandingSettings();
        }

        // A straight body touching down with its feet on the mat, the trunk at the given (unwrapped) angle.
        private TouchdownInfo Touchdown(float bodyAngleDegrees, float landingAngleDegrees)
        {
            var straight = body.ShapePose(BodyShape.Straight);
            var geometry = body.Evaluate(straight);
            var sole = new Vector2(1.5f, GroundY);
            Vector2 centre = sole + PlaneMath.Rotate(geometry.CentreOfMass - geometry.Sole, bodyAngleDegrees * Mathf.Deg2Rad);
            var pose = new GymnastPose(centre, bodyAngleDegrees, straight);
            return new TouchdownInfo(pose, new Vector2(2f, -4f), sole, landingAngleDegrees, 360f);
        }

        // After three giant swings and a salto the angle is 4 × 360° plus the lean of the body.
        [TestCase(LandingKind.Stick, 4 * 360f + 8f, TestName = "Stick turns the short way")]
        [TestCase(LandingKind.Step, 4 * 360f + 30f, TestName = "Step turns the short way")]
        [TestCase(LandingKind.Fall, 4 * 360f + 60f, TestName = "Fall turns the short way")]
        [TestCase(LandingKind.Step, -3 * 360f - 30f, TestName = "Step turns the short way, other direction")]
        public void Landing_TurnsTheShortWay_WithoutJumps(LandingKind kind, float bodyAngle)
        {
            float lean = Mathf.DeltaAngle(0f, bodyAngle);
            var motion = new LandingMotion(body, settings, GroundY, Touchdown(bodyAngle, lean), kind);

            float previous = motion.Pose.BodyAngleDegrees;
            float largestStep = 0f;
            while (!motion.IsFinished)
            {
                motion.Advance(FrameTime);
                largestStep = Mathf.Max(largestStep, Mathf.Abs(motion.Pose.BodyAngleDegrees - previous));
                previous = motion.Pose.BodyAngleDegrees;
            }

            // The whole turn is at most about 90°, spread over at least 27 frames.
            Assert.Less(largestStep, 15f, "largest change of the body angle in one frame");
            Assert.AreEqual(lean, motion.PoseAt(0f).BodyAngleDegrees, 1e-3f, "starts from the wrapped angle");
        }

        [Test]
        public void Stick_KeepsTheFeetWhereTheyLanded()
        {
            var touchdown = Touchdown(4 * 360f + 8f, 8f);
            var motion = new LandingMotion(body, settings, GroundY, touchdown, LandingKind.Stick);

            for (float t = 0f; t <= 1f; t += 0.05f)
            {
                var pose = motion.PoseAt(t);
                var geometry = body.Evaluate(pose.Joints);
                Vector2 sole = pose.CentreOfMass + PlaneMath.Rotate(geometry.Sole - geometry.CentreOfMass,
                    pose.BodyAngleDegrees * Mathf.Deg2Rad);
                Assert.AreEqual(touchdown.Sole.x, sole.x, 1e-3f, $"feet slide at t = {t}");
                Assert.AreEqual(GroundY, sole.y, 1e-3f, $"feet leave the mat at t = {t}");
            }
            Assert.AreEqual(0f, motion.PoseAt(1f).BodyAngleDegrees, 1e-3f, "ends upright");
        }

        [Test]
        public void Step_EndsOneStepFurtherInTheDirectionOfTravel()
        {
            var touchdown = Touchdown(30f, 30f);    // travelling towards +x
            var motion = new LandingMotion(body, settings, GroundY, touchdown, LandingKind.Step);

            var end = motion.PoseAt(1f);
            var geometry = body.Evaluate(end.Joints);
            Vector2 sole = end.CentreOfMass + PlaneMath.Rotate(geometry.Sole - geometry.CentreOfMass, end.BodyAngleDegrees * Mathf.Deg2Rad);

            Assert.AreEqual(touchdown.Sole.x + settings.StepDistance, sole.x, 1e-3f);
            Assert.AreEqual(0f, end.BodyAngleDegrees, 1e-3f);
        }

        [TestCase(LandingKind.Stick)]
        [TestCase(LandingKind.Step)]
        [TestCase(LandingKind.Fall)]
        public void Finished_IsRaisedOnce_OnlyAfterTheWholeDuration(LandingKind kind)
        {
            var motion = new LandingMotion(body, settings, GroundY, Touchdown(10f, 10f), kind);
            int finishedCount = 0;
            float finishedAt = -1f;
            motion.Finished += () =>
            {
                finishedCount++;
                finishedAt = motion.Elapsed;
            };

            // Every frame that still ends before the duration.
            while (motion.Elapsed + FrameTime < motion.Duration - 1e-4f)
                motion.Advance(FrameTime);
            Assert.AreEqual(0, finishedCount, "finished before the whole duration");
            Assert.IsFalse(motion.IsFinished);

            for (int i = 0; i < 10; i++)    // one frame to finish, then a few more
                motion.Advance(FrameTime);

            Assert.AreEqual(1, finishedCount);
            Assert.AreEqual(motion.Duration, finishedAt, 1e-5f);
        }
    }
}
