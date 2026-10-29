using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace GymnastGame.Rules.Tests
{
    /// <summary>Deductions for catching the bar, from the regrasp rules in deductions.json.</summary>
    public class RegraspJudgeTests
    {
        private RegraspJudge judge;

        [OneTimeSetUp]
        public void LoadData()
        {
            string path = Path.Combine(Application.dataPath, "Data", "HighBar", "deductions.json");
            judge = new RegraspJudge(DeductionTable.FromJson(File.ReadAllText(path)));
        }

        [Test]
        public void CleanCatch_HasNoDeductions()
        {
            Assert.IsEmpty(judge.Deductions(0.1f, 5f, 0f, 3f));
            Assert.IsTrue(judge.IsClean(0.1f, 5f, 0f, 3f));
        }

        [TestCase(0.2f, 0f, 0f, 0f, 0.1, TestName = "Hands 0.2 m away: small")]
        [TestCase(0.5f, 0f, 0f, 0f, 0.3, TestName = "Hands 0.5 m away: medium")]
        [TestCase(0.1f, 30f, 0f, 0f, 0.1, TestName = "Tuck 30° not opened: small")]
        [TestCase(0.1f, 120f, 0f, 0f, 0.3, TestName = "Tuck 120° not opened: medium")]
        [TestCase(0.1f, 0f, 20f, 0f, 0.1, TestName = "Arms 20° off: small")]
        [TestCase(0.1f, 0f, 60f, 0f, 0.3, TestName = "Arms 60° off: medium")]
        [TestCase(0.1f, 0f, 0f, 40f, 0.3, TestName = "Twist 40° off: medium")]
        public void OneFault_GivesOneDeduction(float reach, float legBend, float arms, float twist, double points)
        {
            var items = judge.Deductions(reach, legBend, arms, twist);

            Assert.AreEqual(1, items.Count);
            Assert.AreEqual((decimal)points, items[0].Points);
            Assert.IsFalse(judge.IsClean(reach, legBend, arms, twist));
        }

        [Test]
        public void SeveralFaults_AreSeparateLines()
        {
            var items = judge.Deductions(0.4f, 50f, 60f, 40f);

            Assert.AreEqual(4, items.Count);
            Assert.AreEqual(1.2m, items.Sum(item => item.Points));
        }
    }
}
