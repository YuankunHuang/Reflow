using NUnit.Framework;
using ChildInput = Reflow.ReflowAxisSolver.ChildInput;
using MainAxisMode = Reflow.ReflowAxisSolver.MainAxisMode;

namespace Reflow.Tests
{
    public class ReflowAxisSolverTests
    {
        private const float TOLERANCE = 0.001f;

        private static ChildInput Plain(float pNatural)
        {
            return new ChildInput { natural = pNatural, layoutControls = true, flexWeight = 1f };
        }

        private static ChildInput Element(float pNatural, float pFlex = 1f, float pMin = 0f, float pMax = 0f, bool pControls = true)
        {
            return new ChildInput { natural = pNatural, layoutControls = pControls, hasElement = true, flexWeight = pFlex, minSize = pMin, maxSize = pMax };
        }

        private static float[] Run(MainAxisMode pMode, float pAvail, float pSpacingTotal, params ChildInput[] pInputArray)
        {
            float[] outArray = new float[pInputArray.Length];
            ReflowAxisSolver.Distribute(pInputArray.Length, pMode, pAvail, pSpacingTotal, pInputArray, outArray);
            return outArray;
        }

        private static void AssertSizes(float[] pActual, params float[] pExpected)
        {
            Assert.AreEqual(pExpected.Length, pActual.Length);
            for (int i = 0; i < pExpected.Length; i++)
                Assert.AreEqual(pExpected[i], pActual[i], TOLERANCE, $"child {i}");
        }

        [Test]
        public void Natural_KeepsSizesClampedByBounds()
        {
            AssertSizes(Run(MainAxisMode.Natural, 100f, 0f, Plain(30f), Element(40f, pMax: 35f), Element(10f, pMin: 20f)), 30f, 35f, 20f);
        }

        [Test]
        public void Natural_UncontrolledIgnoresBounds()
        {
            AssertSizes(Run(MainAxisMode.Natural, 100f, 0f, Element(40f, pMax: 35f, pControls: false)), 40f);
        }

        [Test]
        public void Expand_PlainChildrenShareEvenly()
        {
            AssertSizes(Run(MainAxisMode.Expand, 300f, 0f, Plain(50f), Plain(20f), Plain(80f)), 100f, 100f, 100f);
        }

        [Test]
        public void Expand_SpacingIsReserved()
        {
            AssertSizes(Run(MainAxisMode.Expand, 310f, 10f, Plain(0f), Plain(0f)), 150f, 150f);
        }

        [Test]
        public void Expand_SplitsByWeight()
        {
            AssertSizes(Run(MainAxisMode.Expand, 300f, 0f, Element(0f, 1f), Element(0f, 2f)), 100f, 200f);
        }

        [Test]
        public void Expand_MaxFreezesAndHandsBackSpace()
        {
            AssertSizes(Run(MainAxisMode.Expand, 300f, 0f, Element(0f, pMax: 50f), Element(0f)), 50f, 250f);
        }

        [Test]
        public void Expand_MinFreezesAndTakesSpace()
        {
            AssertSizes(Run(MainAxisMode.Expand, 300f, 0f, Element(0f, pMin: 200f), Element(0f)), 200f, 100f);
        }

        [Test]
        public void Expand_ZeroWeightKeepsNatural()
        {
            AssertSizes(Run(MainAxisMode.Expand, 300f, 0f, Element(80f, 0f), Plain(0f)), 80f, 220f);
        }

        [Test]
        public void Expand_UncontrolledKeepsNatural()
        {
            AssertSizes(Run(MainAxisMode.Expand, 300f, 0f, Element(80f, pControls: false), Plain(0f)), 80f, 220f);
        }

        [Test]
        public void Expand_NoFlexScalesWithNaturalSize()
        {
            AssertSizes(Run(MainAxisMode.Expand, 600f, 0f, Element(100f, 0f), Element(200f, 0f)), 200f, 400f);
        }

        [Test]
        public void Capped_FlexibleChildTakesTheRest()
        {
            AssertSizes(Run(MainAxisMode.Capped, 250f, 0f, Plain(100f), Element(300f)), 100f, 150f);
        }

        [Test]
        public void Capped_ShrinkToFitStopsAtMin()
        {
            AssertSizes(Run(MainAxisMode.Capped, 300f, 0f, Element(200f, 0f, pMin: 180f), Element(200f, 0f)), 180f, 120f);
        }

        [Test]
        public void Capped_ShrinkLeavesUncontrolledAlone()
        {
            AssertSizes(Run(MainAxisMode.Capped, 150f, 0f, Element(100f, 0f, pControls: false), Plain(200f)), 100f, 50f);
        }

        [Test]
        public void Cross_ExpandFillsWithinBounds()
        {
            Assert.AreEqual(300f, ReflowAxisSolver.ResolveCross(Plain(50f), 300f, true, false), TOLERANCE);
            Assert.AreEqual(120f, ReflowAxisSolver.ResolveCross(Element(50f, pMax: 120f), 300f, true, false), TOLERANCE);
            Assert.AreEqual(50f, ReflowAxisSolver.ResolveCross(Element(50f, pControls: false), 300f, true, false), TOLERANCE);
        }

        [Test]
        public void Cross_CappedShrinksToAvailable()
        {
            Assert.AreEqual(200f, ReflowAxisSolver.ResolveCross(Plain(260f), 200f, false, true), TOLERANCE);
            Assert.AreEqual(150f, ReflowAxisSolver.ResolveCross(Plain(150f), 200f, false, true), TOLERANCE);
            Assert.AreEqual(260f, ReflowAxisSolver.ResolveCross(Plain(260f), 200f, false, false), TOLERANCE);
        }
    }
}
