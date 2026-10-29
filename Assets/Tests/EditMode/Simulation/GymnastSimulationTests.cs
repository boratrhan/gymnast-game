using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace GymnastGame.Simulation.Tests
{
    /// <summary>
    /// Physics tests for the planar gymnast simulation, with the scene's real-scale settings:
    /// bar at 2.6 m, ground at y = 0, body from Assets/Data/Body/body.json.
    /// </summary>
    public class GymnastSimulationTests
    {
        private const float Dt = 0.02f;     // Unity's default fixed time step
        private const float Gravity = 9.81f;
        private static readonly Vector2 Bar = new Vector2(0f, 2.6f);

        private static BodyModel body;

        [OneTimeSetUp]
        public void LoadBody()
        {
            body = BodyModel.FromJson(File.ReadAllText(Path.Combine(Application.dataPath, "Data", "Body", "body.json")));
        }

        private static GymnastSimulation Create(float damping = 0.15f, float startAngle = 30f) =>
            new GymnastSimulation(new SimulationSettings().With(damping, startAngle), body, Bar);

        private static GymnastCommands Pump => new GymnastCommands(pump: true, releaseOrCatch: false);
        private static GymnastCommands Release => new GymnastCommands(pump: false, releaseOrCatch: true);
        private static GymnastCommands InShape(BodyShape shape) => new GymnastCommands(false, false, shape);
        private static GymnastCommands Twisting(BodyShape shape) => new GymnastCommands(false, false, shape, twist: true);

        // Energy per unit mass of a point pendulum, measured from the bottom of the swing.
        private static float Energy(GymnastSimulation sim)
        {
            float length = sim.PendulumLength;
            float a = sim.AngleDegrees * Mathf.Deg2Rad;
            float w = sim.AngularVelocity;
            return 0.5f * length * length * w * w + Gravity * length * (1f - Mathf.Cos(a));
        }

        // The swing uses semi-implicit Euler (velocity first, then angle). It conserves a slightly
        // different "shadow" energy, so the true energy wobbles by about (dt/2)·ω·(g/L)·sin θ:
        // roughly ±3 % mid-swing at dt = 0.02, close to zero at the turning points, and it never drifts.
        // So we check for drift and measure at the turning points instead of at an arbitrary moment.
        [Test]
        public void Swing_WithoutDamping_HasNoEnergyDrift()
        {
            var sim = Create(damping: 0f);
            float start = Energy(sim);
            const int steps = 10000;            // 200 seconds, about 95 swings
            const int window = 500;             // 10 seconds
            float firstWindowMax = 0f, lastWindowMax = 0f, turningPointMax = 0f;

            for (int i = 0; i < steps; i++)
            {
                float previousVelocity = sim.AngularVelocity;
                sim.Step(Dt, GymnastCommands.None);
                float deviation = Mathf.Abs(Energy(sim) - start) / start;

                if (i < window) firstWindowMax = Mathf.Max(firstWindowMax, deviation);
                if (i >= steps - window) lastWindowMax = Mathf.Max(lastWindowMax, deviation);
                bool turned = previousVelocity != 0f && Mathf.Sign(previousVelocity) != Mathf.Sign(sim.AngularVelocity);
                if (turned) turningPointMax = Mathf.Max(turningPointMax, deviation);
            }

            Assert.LessOrEqual(lastWindowMax, firstWindowMax + 0.001f, "energy drifts over time");
            Assert.Less(turningPointMax, 0.005f, "energy at the turning points");
        }

        [Test]
        public void Pump_OutsideTheWindow_IsAMiss()
        {
            var sim = Create(startAngle: 30f);   // window is 25° either side of the bottom
            float quality = -1f;
            sim.Pumped += q => quality = q;

            sim.Step(Dt, Pump);

            Assert.AreEqual(1, sim.MissCount);
            Assert.AreEqual(0f, quality);
        }

        [Test]
        public void Pump_AtTheBottom_IsPerfectAndAddsSpeed()
        {
            var sim = Create(damping: 0f, startAngle: 0f);

            sim.Step(Dt, Pump);

            Assert.AreEqual(1f, sim.LastPumpQuality);
            Assert.AreEqual(1.6f, sim.AngularVelocity, 0.01f);   // pump strength; no gravity torque at the bottom
        }

        [Test]
        public void Pump_TwiceWithinTheCooldown_CountsOnce()
        {
            var sim = Create(startAngle: 0f);
            int pumps = 0;
            sim.Pumped += _ => pumps++;

            sim.Step(Dt, Pump);
            sim.Step(Dt, Pump);     // 0.02 s later, cooldown is 0.35 s

            Assert.AreEqual(1, pumps);
            Assert.AreEqual(0, sim.MissCount);
        }

        [Test]
        public void Pumping_AtTheBottom_ReachesAGiantSwing()
        {
            var sim = Create();
            int turnsReported = 0;
            sim.FullTurnCompleted += turns => turnsReported = turns;

            for (int i = 0; i < 3000 && sim.FullTurns == 0; i++)    // at most 60 seconds
            {
                bool nearBottom = Mathf.Abs(sim.AngleDegrees) < 5f;
                sim.Step(Dt, nearBottom ? Pump : GymnastCommands.None);
            }

            Assert.AreEqual(1, sim.FullTurns);
            Assert.AreEqual(1, turnsReported);
        }

        [Test]
        public void Release_VelocityIsPerpendicularToTheRadius()
        {
            var sim = Create(damping: 0f, startAngle: 60f);
            for (int i = 0; i < 20; i++)
                sim.Step(Dt, GymnastCommands.None);

            float a = sim.AngleDegrees * Mathf.Deg2Rad;
            float w = sim.AngularVelocity;
            float r = sim.PendulumLength;
            sim.Step(Dt, Release);

            // v = w x r, then one step of gravity in the air
            var expected = new Vector2(w * r * Mathf.Cos(a), w * r * Mathf.Sin(a) - Gravity * Dt);
            Assert.AreEqual(GymnastPhase.Flying, sim.Phase);
            Assert.AreEqual(expected.x, sim.Velocity.x, 1e-4f);
            Assert.AreEqual(expected.y, sim.Velocity.y, 1e-4f);
        }

        [Test]
        public void Release_FromStandstill_FallsInTheExpectedTime()
        {
            var sim = Create(damping: 0f, startAngle: 0f);
            TouchdownInfo? touchdown = null;
            sim.TouchedDown += info => touchdown = info;

            // Height of the feet when hanging still: the lowest point of the straight body.
            var hanging = body.Evaluate(body.ShapePose(BodyShape.Straight));
            float feetHeight = hanging.LowestY(Bar.y - sim.PendulumLength, 0f);

            const float dt = 0.001f;
            float time = 0f;
            sim.Step(dt, Release);
            while (sim.Phase == GymnastPhase.Flying && time < 2f)
            {
                time += dt;
                sim.Step(dt, GymnastCommands.None);
            }

            Assert.IsTrue(touchdown.HasValue);
            Assert.AreEqual(Mathf.Sqrt(2f * feetHeight / Gravity), time, 0.005f);   // t = sqrt(2h / g)
            Assert.AreEqual(0f, touchdown.Value.LandingAngleDegrees, 1e-3f);
            Assert.AreEqual(0f, touchdown.Value.AirRotationDegrees, 1e-3f);
        }

        [Test]
        public void Tuck_KeepsAngularMomentumAndSpinsFaster()
        {
            var sim = ReleasedUpwards();
            sim.Step(Dt, InShape(BodyShape.Straight));
            float straightSpin = sim.BodyAngularVelocity;
            float momentum = sim.AngularMomentum;

            for (int i = 0; i < 20; i++)    // 0.4 s, longer than the change to a full tuck
                sim.Step(Dt, InShape(BodyShape.Tuck));

            float straightInertia = body.Evaluate(body.ShapePose(BodyShape.Straight)).Inertia;
            float tuckInertia = body.Evaluate(body.ShapePose(BodyShape.Tuck)).Inertia;
            Assert.AreEqual(GymnastPhase.Flying, sim.Phase);
            Assert.AreEqual(momentum, sim.AngularMomentum);
            Assert.AreEqual(straightInertia / tuckInertia, sim.BodyAngularVelocity / straightSpin, 0.01f);   // ω = L / I
        }

        [Test]
        public void ShapeChange_ReachesTuckInTheShapeChangeTime()
        {
            var sim = ReleasedUpwards();
            var tuck = body.ShapePose(BodyShape.Tuck);
            int stepsNeeded = Mathf.CeilToInt(body.ShapeChangeTime / Dt);

            for (int i = 0; i < stepsNeeded - 1; i++)
                sim.Step(Dt, InShape(BodyShape.Tuck));
            Assert.Greater(BodyPose.MaxDifference(sim.Pose.Joints, tuck), 0f, "too early to be fully tucked");

            sim.Step(Dt, InShape(BodyShape.Tuck));
            Assert.AreEqual(0f, BodyPose.MaxDifference(sim.Pose.Joints, tuck), 1e-3f);
        }

        [Test]
        public void Twist_StraightReachesTheMaximumRate()
        {
            var sim = ReleasedUpwards();

            for (int i = 0; i < 15; i++)    // 0.3 s: spin-up and arms on the chest are both done
                sim.Step(Dt, Twisting(BodyShape.Straight));

            Assert.AreEqual(GymnastPhase.Flying, sim.Phase);
            Assert.AreEqual(3f * 2f * Mathf.PI, sim.TwistAngularVelocity, 0.01f * 3f * 2f * Mathf.PI);   // 3 turns per second
        }

        [Test]
        public void Twist_InATuckIsSlowerByTheInertiaRatio()
        {
            var sim = ReleasedUpwards();

            for (int i = 0; i < 15; i++)
                sim.Step(Dt, Twisting(BodyShape.Tuck));

            float twistPose = body.Evaluate(body.Pose("twist")).TwistInertia;
            float tuck = body.Evaluate(body.ShapePose(BodyShape.Tuck)).TwistInertia;
            float straightRate = 3f * 2f * Mathf.PI;
            Assert.AreEqual(straightRate * twistPose / tuck, sim.TwistAngularVelocity, 0.01f * straightRate);   // ω = H / I_long
        }

        [Test]
        public void Twist_StopsAfterLettingGo()
        {
            var sim = ReleasedUpwards();
            for (int i = 0; i < 10; i++)
                sim.Step(Dt, Twisting(BodyShape.Straight));
            for (int i = 0; i < 10; i++)    // 0.2 s, longer than the stop time
                sim.Step(Dt, InShape(BodyShape.Straight));

            float twistAfterStop = sim.Pose.TwistDegrees;
            sim.Step(Dt, InShape(BodyShape.Straight));

            Assert.AreEqual(0f, sim.TwistAngularVelocity);
            Assert.AreEqual(twistAfterStop, sim.Pose.TwistDegrees);
            Assert.Greater(twistAfterStop, 90f, "it twisted while D was held");
        }

        [TestCase(350f, 10f, 2)]
        [TestCase(190f, 10f, 1)]
        [TestCase(100f, 80f, 1)]
        [TestCase(-170f, 10f, 1)]
        [TestCase(725f, 5f, 4)]
        [TestCase(0f, 0f, 0)]
        public void TwistMath_MeasuresFromTheNearestHalfTwist(float twist, float error, int halfTwists)
        {
            Assert.AreEqual(error, TwistMath.ErrorFromHalfTwist(twist), 1e-3f);
            Assert.AreEqual(halfTwists, TwistMath.NearestHalfTwists(twist));
        }

        [Test]
        public void Touchdown_StopsTheSimulation()
        {
            var sim = Create(damping: 0f, startAngle: 0f);
            sim.Step(Dt, Release);
            for (int i = 0; i < 100 && sim.Phase == GymnastPhase.Flying; i++)
                sim.Step(Dt, GymnastCommands.None);

            var before = sim.Pose;
            sim.Step(Dt, GymnastCommands.None);

            Assert.AreEqual(GymnastPhase.Touchdown, sim.Phase);
            Assert.AreEqual(before.CentreOfMass, sim.Pose.CentreOfMass);
        }

        [Test]
        public void SameCommands_GiveTheSameMotion()
        {
            GymnastPose Run()
            {
                var sim = Create();
                for (int i = 0; i < 400; i++)
                {
                    var commands = i % 37 == 0 ? Pump : i == 390 ? Release : InShape(i > 392 ? BodyShape.Tuck : BodyShape.Straight);
                    sim.Step(Dt, commands);
                }
                return sim.Pose;
            }

            var first = Run();
            var second = Run();

            Assert.AreEqual(first.CentreOfMass, second.CentreOfMass);
            Assert.AreEqual(first.BodyAngleDegrees, second.BodyAngleDegrees);
        }

        // Swings down from near the handstand and lets go 45° past the bottom on the way up,
        // so the flight is long enough for shape changes (about a second in the air).
        private static GymnastSimulation ReleasedUpwards()
        {
            var sim = Create(damping: 0f, startAngle: 179f);
            for (int i = 0; i < 500 && sim.AngleDegrees > -45f; i++)
                sim.Step(Dt, GymnastCommands.None);
            sim.Step(Dt, Release);
            Assert.AreEqual(GymnastPhase.Flying, sim.Phase);
            return sim;
        }
    }
}
