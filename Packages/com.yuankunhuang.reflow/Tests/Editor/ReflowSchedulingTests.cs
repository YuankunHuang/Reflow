using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace Reflow.Tests
{
    public class ReflowSchedulingTests
    {
        private RectTransform _root;

        [SetUp]
        public void SetUp()
        {
            _root = ReflowTestUtil.CreateRoot();
        }

        [TearDown]
        public void TearDown()
        {
            ReflowTestUtil.DestroyAll();
        }

        [Test]
        public void ManyRequests_OneLayout()
        {
            ReflowVertical layout = ReflowTestUtil.CreateLayout<ReflowVertical>(_root, new Vector2(200f, 200f));
            ReflowTestUtil.CreateRect("A", layout.transform, new Vector2(50f, 50f));
            ReflowScheduler.Flush();
            ReflowStats.Reset();

            for (int i = 0; i < 5; i++)
                layout.RefreshAllLayout();
            Assert.AreEqual(1, ReflowScheduler.PendingCount);
            ReflowScheduler.Flush();

            Assert.AreEqual(1, ReflowStats.RootLayoutCount);
            Assert.AreEqual(1, ReflowStats.SolveCount);
            Assert.IsFalse(layout.IsDirty);
        }

        [Test]
        public void Boundary_StopsPropagation()
        {
            ReflowVertical outer = ReflowTestUtil.CreateLayout<ReflowVertical>(_root, new Vector2(400f, 400f), "Outer");
            ReflowVertical inner = ReflowTestUtil.CreateLayout<ReflowVertical>(outer.transform, new Vector2(200f, 200f), "Inner");
            ReflowFitter fitter = ReflowTestUtil.CreateFittedText(inner.transform, "A", ReflowFitter.FitMode.Preferred, ReflowFitter.FitMode.Preferred);
            ReflowScheduler.Flush();
            ReflowStats.Reset();

            fitter.GetComponent<TMP_Text>().text = "Longer text";
            Assert.IsTrue(inner.IsDirty);
            Assert.IsFalse(outer.IsDirty, "inner does not follow its content, so outer is not affected");
            ReflowScheduler.Flush();

            Assert.AreEqual(1, ReflowStats.SolveCount, "only the inner layout solved");
            Assert.IsFalse(inner.IsDirty);
        }

        [Test]
        public void Follower_DirtiesUpToTheBoundary()
        {
            ReflowVertical outer = ReflowTestUtil.CreateLayout<ReflowVertical>(_root, new Vector2(400f, 400f), "Outer");
            ReflowHorizontal row = ReflowTestUtil.CreateLayout<ReflowHorizontal>(outer.transform, new Vector2(10f, 10f), "Row");
            row.FollowWidth = true;
            row.FollowHeight = true;
            ReflowFitter fitter = ReflowTestUtil.CreateFittedText(row.transform, "A", ReflowFitter.FitMode.Preferred, ReflowFitter.FitMode.Preferred);
            ReflowScheduler.Flush();

            fitter.GetComponent<TMP_Text>().text = "Much longer text";
            Assert.IsTrue(outer.IsDirty && row.IsDirty && fitter.IsDirty);

            // Read barrier on the deepest node settles the whole chain.
            fitter.EnsureLayout();
            Assert.IsFalse(outer.IsDirty || row.IsDirty || fitter.IsDirty);
            float textWidth = fitter.GetComponent<TMP_Text>().GetPreferredValues(32767f, 32767f).x;
            ReflowTestUtil.AssertSize(row.RectTransform, textWidth, row.RectTransform.rect.height);
        }

        [Test]
        public void ForceUpdateCanvases_IsAReadBarrier()
        {
            ReflowVertical layout = ReflowTestUtil.CreateLayout<ReflowVertical>(_root, new Vector2(200f, 200f));
            layout.FollowHeight = true;
            RectTransform a = ReflowTestUtil.CreateRect("A", layout.transform, new Vector2(50f, 50f));
            ReflowScheduler.Flush();

            a.sizeDelta = new Vector2(50f, 80f);
            layout.RefreshAllLayout();
            Canvas.ForceUpdateCanvases();

            Assert.IsFalse(layout.IsDirty);
            ReflowTestUtil.AssertSize(layout.RectTransform, 200f, 80f);
        }

        [Test]
        public void InactiveLayout_CatchesUpWhenEnabled()
        {
            ReflowVertical layout = ReflowTestUtil.CreateLayout<ReflowVertical>(_root, new Vector2(200f, 200f));
            layout.FollowHeight = true;
            RectTransform a = ReflowTestUtil.CreateRect("A", layout.transform, new Vector2(50f, 50f));
            ReflowScheduler.Flush();

            layout.gameObject.SetActive(false);
            a.sizeDelta = new Vector2(50f, 120f);
            layout.RefreshAllLayout();
            ReflowScheduler.Flush();
            layout.gameObject.SetActive(true);
            ReflowScheduler.Flush();

            ReflowTestUtil.AssertSize(layout.RectTransform, 200f, 120f);
        }

        [Test]
        public void ChildLayoutInsideFollower_IsArrangedWithItsFinalSize()
        {
            ReflowVertical outer = ReflowTestUtil.CreateLayout<ReflowVertical>(_root, new Vector2(300f, 10f), "Outer");
            outer.FollowHeight = true;
            outer.CrossAxisMode = ReflowChildSizeMode.Expand;
            ReflowHorizontal row = ReflowTestUtil.CreateLayout<ReflowHorizontal>(outer.transform, new Vector2(10f, 60f), "Row");
            row.MainAxisMode = ReflowChildSizeMode.Expand;
            RectTransform left = ReflowTestUtil.CreateRect("Left", row.transform, new Vector2(10f, 20f));
            RectTransform right = ReflowTestUtil.CreateRect("Right", row.transform, new Vector2(10f, 20f));
            outer.EnsureLayout();

            // Row got the outer width (Expand) and shared it between its children in the same pass.
            ReflowTestUtil.AssertSize(row.RectTransform, 300f, 60f);
            ReflowTestUtil.AssertRect(left, 0f, 20f, 150f, 20f);
            ReflowTestUtil.AssertRect(right, 150f, 20f, 150f, 20f);
        }
    }
}
