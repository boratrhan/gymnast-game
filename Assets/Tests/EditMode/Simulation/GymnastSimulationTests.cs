using NUnit.Framework;
using UnityEngine;

namespace GymnastGame.Simulation.Tests
{
    /// <summary>
    /// Physics tests for the planar gymnast simulation, with the scene's real-scale settings:
    /// bar at 2.6 m, centre of mass 1.1 m below it, ground at y = 0, half body height 0.85 m.
    /// </summary>
    public class GymnastSimulationTests
    {
        private const float Dt = 0.02f;     // Unity's default fixed time step
        private const float Gravity = 9.81f;
        private const float ArmLength = 1.1f;
        private static readonly Vector2 Bar = new Vector2(0f, 2.6f);

        private static GymnastSimulation Create(float damping = 0.15f, float startAngle = 30f) =>
            new GymnastSimulation(new SimulationSettings().With(damping, startAngle), Bar);

        private static GymnastCommands Pump => new GymnastCommands(pump: true, release: false);
        private static GymnastCommands Release => new GymnastCommands(pump: false, release: true);

        // Energy per unit mass of a point pendulum, measured from the bottom of the swing.
        private static float Energy(GymnastSimulation sim)
        {
            float a = sim.AngleDegrees * Mathf.Deg2Rad;
            float w = sim.AngularVelocity;
            return 0.5f * ArmLength * ArmLength * w * w + Gravity * ArmLength * (1f - Mathf.Cos(a));
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
            sim.Step(Dt, Release);

            // v = w x r, then one step of gravity in the air
            var expected = new Vector2(w * ArmLength * Mathf.Cos(a), w * ArmLength * Mathf.Sin(a) - Gravity * Dt);
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

            const float dt = 0.001f;
            float time = 0f;
            sim.Step(dt, Release);
            while (sim.Phase == GymnastPhase.Flying && time < 2f)
            {
                time += dt;
                sim.Step(dt, GymnastCommands.None);
            }

            // Feet start at 2.6 - 1.1 - 0.85 = 0.65 m: t = sqrt(2h / g)
            float expectedTime = Mathf.Sqrt(2f * 0.65f / Gravity);
            Assert.IsTrue(touchdown.HasValue);
            Assert.AreEqual(expectedTime, time, 0.005f);
            Assert.AreEqual(0f, touchdown.Value.BodyAngleDegrees, 1e-3f);
            Assert.AreEqual(0f, touchdown.Value.AirRotationDegrees, 1e-3f);
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
            Assert.AreEqual(before.Position, sim.Pose.Position);
        }

        [Test]
        public void SameCommands_GiveTheSameMotion()
        {
            GymnastPose Run()
            {
                var sim = Create();
                for (int i = 0; i < 400; i++)
                {
                    var commands = i % 37 == 0 ? Pump : i == 390 ? Release : GymnastCommands.None;
                    sim.Step(Dt, commands);
                }
                return sim.Pose;
            }

            var first = Run();
            var second = Run();

            Assert.AreEqual(first.Position, second.Position);
            Assert.AreEqual(first.BodyAngleDegrees, second.BodyAngleDegrees);
        }
    }
}
