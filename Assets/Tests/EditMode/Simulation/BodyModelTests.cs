using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace GymnastGame.Simulation.Tests
{
    /// <summary>
    /// Sanity checks of the planar body model against known human values for a 1.70 m gymnast:
    /// standing centre of mass at about 55-59 % of height, a tucked body about a third as hard to turn
    /// as a straight one, and the hands about a metre from the centre of mass when hanging.
    /// </summary>
    public class BodyModelTests
    {
        private BodyModel body;

        [OneTimeSetUp]
        public void LoadBody()
        {
            body = BodyModel.FromJson(File.ReadAllText(Path.Combine(Application.dataPath, "Data", "Body", "body.json")));
        }

        private float InertiaOf(BodyShape shape) => body.Evaluate(body.ShapePose(shape)).Inertia;

        [Test]
        public void Inertia_StraightIsLargest_TuckIsSmallest()
        {
            float straight = InertiaOf(BodyShape.Straight);
            float pike = InertiaOf(BodyShape.Pike);
            float tuck = InertiaOf(BodyShape.Tuck);

            Assert.Greater(straight, pike);
            Assert.Greater(pike, tuck);
            Assert.That(tuck / straight, Is.InRange(0.25f, 0.40f), "tuck / straight");
            Assert.That(pike / straight, Is.InRange(0.40f, 0.60f), "pike / straight");
        }

        [Test]
        public void Standing_CentreOfMassIsAboutFiftySevenPercentOfHeight()
        {
            var standingArmsDown = body.Evaluate(new BodyPose(0f, 0f, 0f, 0f));

            float heightOfCentre = standingArmsDown.CentreOfMass.y - standingArmsDown.Sole.y;

            Assert.That(heightOfCentre / body.Height, Is.InRange(0.55f, 0.59f));
        }

        [Test]
        public void Hanging_HandsAreAboutAMetreFromTheCentreOfMass()
        {
            var hanging = body.Evaluate(body.ShapePose(BodyShape.Straight));

            float gripToCentre = (hanging.CentreOfMass - hanging.Grip).magnitude;

            Assert.That(gripToCentre, Is.InRange(0.95f, 1.10f));
            Assert.AreEqual(0f, hanging.CentreOfMass.x, 1e-5f, "a straight body hangs straight below the hands");
        }

        [Test]
        public void Pike_MovesTheCentreOfMassTowardsTheChest()
        {
            var piked = body.Evaluate(body.ShapePose(BodyShape.Pike));

            Assert.Greater(piked.CentreOfMass.x, 0.05f, "legs forward move the centre of mass forward");   // about 0.13 m
        }
    }
}
