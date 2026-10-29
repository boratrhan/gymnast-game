using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace GymnastGame.Simulation.Tests
{
    /// <summary>
    /// Catching the bar after a flight: when it is possible, the speed after it, the reach motion, the measured
    /// quality, the failure reasons, and the flight records. Flights start from an exact state
    /// (StartFlightForTests), so the expected catch can be calculated by hand with the same formulas.
    /// </summary>
    public class CatchTests
    {
        private const float Dt = 0.02f;
        private const float Gravity = 9.81f;
        private static readonly Vector2 Bar = new Vector2(0f, 2.6f);

        private BodyModel body;

        [OneTimeSetUp]
        public void LoadBody()
        {
            body = BodyModel.FromJson(File.ReadAllText(Path.Combine(Application.dataPath, "Data", "Body", "body.json")));
        }

        private GymnastSimulation Create() => new GymnastSimulation(new SimulationSettings().With(damping: 0f), body, Bar);

        private static GymnastCommands Catch => new GymnastCommands(false, releaseOrCatch: true);
        private static GymnastCommands TuckAndCatch => new GymnastCommands(false, true, BodyShape.Tuck);

        // Upright body (angle 0) in the given shape, with the hands the given distance straight below the bar
        // (negative = above it).
        private Vector2 CentreWithHandsBelowBar(float handsBelow, BodyPose? joints = null)
        {
            var geometry = body.Evaluate(joints ?? body.ShapePose(BodyShape.Straight));
            Vector2 hands = Bar + new Vector2(0f, -handsBelow);
            return hands - (geometry.Grip - geometry.CentreOfMass);
        }

        [Test]
        public void Catch_KeepsTheSpeedAroundTheBar()
        {
            var sim = Create();
            Vector2 start = CentreWithHandsBelowBar(0.05f);
            var velocity = new Vector2(2f, 1f);
            sim.StartFlightForTests(start, velocity, 0f, 0f);

            sim.Step(Dt, Catch);

            // One step of flight, then ω = v_around / L, with v_around = (r × v) / |r|.
            Vector2 v = velocity + new Vector2(0f, -Gravity * Dt);
            Vector2 r = start + v * Dt - Bar;
            float expectedSpin = (r.x * v.y - r.y * v.x) / r.magnitude / sim.PendulumLength;
            float expectedImpact = Mathf.Abs(Vector2.Dot(v, r.normalized));

            Assert.AreEqual(GymnastPhase.OnBar, sim.Phase);
            Assert.AreEqual(expectedSpin, sim.AngularVelocity, 1e-4f);
            Assert.AreEqual(Mathf.Atan2(r.x, -r.y) * Mathf.Rad2Deg, sim.AngleDegrees, 1e-3f);
            Assert.AreEqual(1, sim.Flights.Count);
            Assert.AreEqual(FlightEnd.Caught, sim.Flights[0].End);
            Assert.AreEqual(expectedImpact, sim.Flights[0].CatchImpactSpeed, 1e-4f);
        }

        [Test]
        public void Catch_FromAwayFromTheBar_ReachesForItWithoutAJump()
        {
            var sim = Create();
            Vector2 start = CentreWithHandsBelowBar(0.5f);      // within the 0.6 m reach
            sim.StartFlightForTests(start, new Vector2(0.5f, 0f), 0f, 0f);

            sim.Step(Dt, Catch);
            Assert.AreEqual(GymnastPhase.OnBar, sim.Phase);
            Vector2 flightCentre = start + new Vector2(0.5f, -Gravity * Dt) * Dt;
            Assert.AreEqual(0f, (sim.Pose.CentreOfMass - flightCentre).magnitude, 1e-4f, "drawn where it flew, no jump");
            Assert.AreEqual(0.5f, sim.Flights[0].Catch.Reach, 0.02f);

            Vector2 previous = sim.Pose.CentreOfMass;
            float largestMove = 0f;
            int reachSteps = Mathf.CeilToInt(body.Catch.ReachTime / Dt) + 1;
            for (int i = 0; i < reachSteps; i++)
            {
                sim.Step(Dt, GymnastCommands.None);
                largestMove = Mathf.Max(largestMove, (sim.Pose.CentreOfMass - previous).magnitude);
                previous = sim.Pose.CentreOfMass;
            }

            Assert.AreEqual(sim.PendulumLength, (sim.Pose.CentreOfMass - Bar).magnitude, 1e-3f, "hanging from the bar after the reach");
            Assert.Less(largestMove, 0.2f, "largest move of the drawn centre of mass in one step");
        }

        [Test]
        public void Catch_WithoutEnter_DoesNotHappen()
        {
            var sim = Create();
            sim.StartFlightForTests(CentreWithHandsBelowBar(0.05f), new Vector2(2f, 1f), 0f, 0f);

            sim.Step(Dt, GymnastCommands.None);

            Assert.AreEqual(GymnastPhase.Flying, sim.Phase);
            Assert.IsTrue(sim.CatchStatus.CanCatch, "the indicator would light up here");
        }

        [Test]
        public void Catch_TooHard_Slips()
        {
            var sim = Create();
            bool slipped = false;
            sim.CatchSlipped += () => slipped = true;
            sim.StartFlightForTests(CentreWithHandsBelowBar(0.05f), new Vector2(0f, 5f), 0f, 0f);   // 5 m/s into the bar

            sim.Step(Dt, Catch);

            Assert.IsTrue(slipped);
            Assert.AreEqual(GymnastPhase.Flying, sim.Phase);
        }

        [Test]
        public void Catch_TooFar_FailsOnce_AfterTheBuffer()
        {
            var sim = Create();
            var reasons = new List<CatchBlocker>();
            sim.CatchFailed += reason => reasons.Add(reason);
            // Hands 0.7 m above the bar (further than the 0.6 m reach) and rising, so the flight outlasts the buffer.
            sim.StartFlightForTests(CentreWithHandsBelowBar(-0.7f), new Vector2(0f, 2f), 0f, 0f);

            sim.Step(Dt, Catch);
            int stepsInBuffer = Mathf.FloorToInt(body.Catch.InputBuffer / Dt) - 1;
            for (int i = 0; i < stepsInBuffer; i++)
                sim.Step(Dt, GymnastCommands.None);
            Assert.AreEqual(0, reasons.Count, "no message while the buffer may still catch");

            for (int i = 0; i < 5; i++)
                sim.Step(Dt, GymnastCommands.None);

            Assert.AreEqual(GymnastPhase.Flying, sim.Phase);
            CollectionAssert.AreEqual(new[] { CatchBlocker.TooFar }, reasons);
        }

        [Test]
        public void Catch_InATuck_StillCatches_MeasuresTheBend_ThenOpens()
        {
            var sim = Create();
            var tuck = body.ShapePose(BodyShape.Tuck);
            sim.StartFlightForTests(CentreWithHandsBelowBar(0.05f, tuck), new Vector2(1f, 0f), 0f, 0f, startJoints: tuck);

            sim.Step(Dt, TuckAndCatch);

            Assert.AreEqual(GymnastPhase.OnBar, sim.Phase, "a tucked catch holds, with a deduction");
            Assert.AreEqual(tuck.Hip, sim.Flights[0].Catch.LegBend, 1e-3f);

            for (int i = 0; i < 15; i++)    // 0.3 s on the bar
                sim.Step(Dt, GymnastCommands.None);
            Assert.AreEqual(0f, BodyPose.MaxDifference(sim.Pose.Joints, body.ShapePose(BodyShape.Straight)), 1e-3f, "opened");
        }

        [Test]
        public void Catch_EarlyPress_IsBuffered()
        {
            var sim = Create();
            // Hands 0.75 m above the bar and falling towards it.
            sim.StartFlightForTests(CentreWithHandsBelowBar(-0.75f), new Vector2(0f, -2f), 0f, 0f);

            sim.Step(Dt, Catch);     // pressed while still out of reach
            Assert.AreEqual(CatchBlocker.TooFar, sim.CatchStatus.Blocker);
            for (int i = 0; i < 5 && sim.Phase == GymnastPhase.Flying; i++)
                sim.Step(Dt, GymnastCommands.None);

            Assert.AreEqual(GymnastPhase.OnBar, sim.Phase, "caught within the input buffer");
        }

        private static GymnastCommands HoldingEnter => new GymnastCommands(false, false, catchHeld: true);

        [Test]
        public void Catch_HoldingEnter_CatchesWhenTheHandsComeWithinReach()
        {
            var sim = Create();
            // Hands 1 m above the bar and falling towards it: out of reach for several steps, longer than the buffer.
            sim.StartFlightForTests(CentreWithHandsBelowBar(-1f), new Vector2(0f, -1f), 0f, 0f);

            bool caughtInReach = false;
            for (int i = 0; i < 40 && sim.Phase == GymnastPhase.Flying; i++)
            {
                sim.Step(Dt, HoldingEnter);
                if (sim.Phase == GymnastPhase.OnBar)
                    caughtInReach = sim.Flights[0].Catch.Reach <= body.Catch.MaxReach;
            }

            Assert.AreEqual(GymnastPhase.OnBar, sim.Phase);
            Assert.IsTrue(caughtInReach);
            Assert.AreEqual(body.Catch.MaxReach, sim.Flights[0].Catch.Reach, 0.1f, "caught right at the edge of the reach");
        }

        [Test]
        public void HoldingEnter_OutOfReach_ReportsNoFailure()
        {
            var sim = Create();
            var reasons = new List<CatchBlocker>();
            sim.CatchFailed += reason => reasons.Add(reason);
            sim.StartFlightForTests(CentreWithHandsBelowBar(-1f), new Vector2(0f, 2f), 0f, 0f);   // rising, away from the bar

            for (int i = 0; i < 10; i++)
                sim.Step(Dt, HoldingEnter);
            for (int i = 0; i < 15; i++)
                sim.Step(Dt, GymnastCommands.None);

            Assert.AreEqual(GymnastPhase.Flying, sim.Phase);
            Assert.IsEmpty(reasons, "only a press that fails is reported");
        }

        [Test]
        public void Catch_AfterAHalfTwist_FacesTheOtherWay()
        {
            var sim = Create();
            sim.StartFlightForTests(CentreWithHandsBelowBar(0.05f), new Vector2(2f, 1f), 0f, 0f, twistDegrees: 180f);

            sim.Step(Dt, Catch);
            for (int i = 0; i < 10; i++)    // past the reach motion
                sim.Step(Dt, GymnastCommands.None);

            Assert.AreEqual(-1, sim.Facing);
            Assert.AreEqual(180f, sim.Pose.TwistDegrees, 1e-3f);
        }

        [Test]
        public void Status_RightAfterTheRelease_IsTooEarly()
        {
            var sim = new GymnastSimulation(new SimulationSettings().With(damping: 0f, startAngle: 0f), body, Bar);

            sim.Step(Dt, new GymnastCommands(false, releaseOrCatch: true));     // let go while hanging still

            Assert.AreEqual(GymnastPhase.Flying, sim.Phase);
            Assert.AreEqual(CatchBlocker.TooEarly, sim.CatchStatus.Blocker);
        }

        [Test]
        public void Quality_MeasuresBendArmsAndTwist_WithoutBlockingTheCatch()
        {
            float jointStep = body.JointSpeed * Dt, handsStep = body.HandsInSpeed * Dt;   // one step towards straight

            var bent = new BodyPose(180f, 0f, 60f, 0f);
            var simBent = Create();
            simBent.StartFlightForTests(CentreWithHandsBelowBar(0.05f, bent), new Vector2(1f, 0f), 0f, 0f, startJoints: bent);
            simBent.Step(Dt, GymnastCommands.None);
            Assert.AreEqual(60f - jointStep, simBent.CatchStatus.Quality.LegBend, 1e-3f, "leg bend");

            var armsIn = new BodyPose(180f, 0f, 0f, 0f, handsIn: 0.6f);
            var simArms = Create();
            simArms.StartFlightForTests(CentreWithHandsBelowBar(0.05f, armsIn), new Vector2(1f, 0f), 0f, 0f, startJoints: armsIn);
            simArms.Step(Dt, GymnastCommands.None);
            Assert.AreEqual((0.6f - handsStep) * 90f, simArms.CatchStatus.Quality.ArmDeviation, 1e-2f, "arm deviation");

            var simTwist = Create();
            simTwist.StartFlightForTests(CentreWithHandsBelowBar(0.05f), new Vector2(1f, 0f), 0f, 0f, twistDegrees: 90f);
            simTwist.Step(Dt, GymnastCommands.None);
            Assert.AreEqual(90f, simTwist.CatchStatus.Quality.TwistError, 1e-3f, "twist error");

            Assert.IsTrue(simBent.CatchStatus.CanCatch && simArms.CatchStatus.CanCatch && simTwist.CatchStatus.CanCatch,
                "faults are deductions, not blockers");
        }

        [Test]
        public void AfterACatch_TheNextReleaseStartsASecondFlight()
        {
            var sim = Create();
            sim.StartFlightForTests(CentreWithHandsBelowBar(0.05f), new Vector2(2f, 1f), 0f, 0f);
            sim.Step(Dt, Catch);
            for (int i = 0; i < 10; i++)
                sim.Step(Dt, GymnastCommands.None);

            sim.Step(Dt, new GymnastCommands(false, releaseOrCatch: true));
            for (int i = 0; i < 200 && sim.Phase == GymnastPhase.Flying; i++)
                sim.Step(Dt, GymnastCommands.None);

            Assert.AreEqual(GymnastPhase.Touchdown, sim.Phase);
            Assert.AreEqual(2, sim.Flights.Count);
            Assert.AreEqual(FlightEnd.Caught, sim.Flights[0].End);
            Assert.AreEqual(FlightEnd.Landed, sim.Flights[1].End);
        }

        [Test]
        public void Flight_AboveTheBar_IsRecordedAsOverTheBar()
        {
            var sim = Create();
            sim.StartFlightForTests(new Vector2(-0.6f, 3.6f), new Vector2(3f, 0f), 0f, 0f);   // crosses the bar line 1 m above it

            for (int i = 0; i < 200 && sim.Phase == GymnastPhase.Flying; i++)
                sim.Step(Dt, GymnastCommands.None);

            Assert.AreEqual(1, sim.Flights.Count);
            Assert.IsTrue(sim.Flights[0].OverBar);
            Assert.AreEqual(FlightEnd.Landed, sim.Flights[0].End);
        }

        [TestCase(FlightEnd.Caught, 700f, 2, TestName = "Caught flight rounds the saltos (release and catch angles differ)")]
        [TestCase(FlightEnd.Landed, 700f, 1, TestName = "Landing counts only completed saltos")]
        [TestCase(FlightEnd.Caught, -380f, 1, TestName = "Caught flight, other direction")]
        public void FlightRecord_CountsSaltos(FlightEnd end, float rotation, int saltos)
        {
            var record = new FlightRecord(0f, 0f, rotation, 0f, BodyShape.Tuck, true, end, false, default, 1f);

            Assert.AreEqual(saltos, record.Saltos);
        }

        [Test]
        public void GiantSwings_CountEachPassOverTheTop_AfterACatch()
        {
            var sim = Create();
            int turns = 0;
            sim.FullTurnCompleted += total => turns = total;
            // Caught fast near the bottom: about 6.9 rad/s around the bar, enough to go over the top (needs about 6.2).
            sim.StartFlightForTests(CentreWithHandsBelowBar(0.02f), new Vector2(7f, 0f), 0f, 0f);
            sim.Step(Dt, Catch);
            Assert.AreEqual(GymnastPhase.OnBar, sim.Phase);

            for (int i = 0; i < 100; i++)   // 2 seconds without damping: at least one turn
                sim.Step(Dt, GymnastCommands.None);

            Assert.GreaterOrEqual(turns, 1);
            Assert.AreEqual(turns, sim.FullTurns);
        }
    }
}
