using NUnit.Framework;
using UnityEngine;

namespace Reflow.Tests
{
    public class ReflowLinearTests
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

        private ReflowVertical CreateVertical(Vector2 pSize)
        {
            ReflowVertical layout = ReflowTestUtil.CreateLayout<ReflowVertical>(_root, pSize);
            layout.Padding = new RectOffset(10, 20, 30, 40);
            layout.Spacing = 5f;
            return layout;
        }

        [Test]
        public void Vertical_StacksWithPaddingAndSpacing()
        {
            ReflowVertical layout = CreateVertical(new Vector2(300f, 500f));
            RectTransform a = ReflowTestUtil.CreateRect("A", layout.transform, new Vector2(100f, 50f));
            RectTransform b = ReflowTestUtil.CreateRect("B", layout.transform, new Vector2(120f, 60f));
            layout.EnsureLayout();

            ReflowTestUtil.AssertRect(a, 10f, 30f, 100f, 50f);
            ReflowTestUtil.AssertRect(b, 10f, 85f, 120f, 60f);
            ReflowTestUtil.AssertSize(layout.RectTransform, 300f, 500f);
        }

        [Test]
        public void Vertical_CenterAlignmentHonorsPadding()
        {
            ReflowVertical layout = CreateVertical(new Vector2(300f, 500f));
            layout.ChildAlignment = TextAnchor.MiddleCenter;
            RectTransform a = ReflowTestUtil.CreateRect("A", layout.transform, new Vector2(100f, 50f));
            layout.EnsureLayout();

            // Inner area: x 10..280 (270 wide), y 30..460 (430 tall).
            ReflowTestUtil.AssertRect(a, 10f + (270f - 100f) * 0.5f, 30f + (430f - 50f) * 0.5f, 100f, 50f);
        }

        [Test]
        public void Vertical_FollowsContent()
        {
            ReflowVertical layout = CreateVertical(new Vector2(300f, 10f));
            layout.FollowHeight = true;
            layout.FollowWidth = true;
            ReflowTestUtil.CreateRect("A", layout.transform, new Vector2(100f, 50f));
            ReflowTestUtil.CreateRect("B", layout.transform, new Vector2(120f, 60f));
            layout.EnsureLayout();

            ReflowTestUtil.AssertSize(layout.RectTransform, 10f + 120f + 20f, 30f + 50f + 5f + 60f + 40f);
        }

        [Test]
        public void Vertical_ReverseArrangement()
        {
            ReflowVertical layout = CreateVertical(new Vector2(300f, 500f));
            layout.ReverseArrangement = true;
            RectTransform a = ReflowTestUtil.CreateRect("A", layout.transform, new Vector2(100f, 50f));
            RectTransform b = ReflowTestUtil.CreateRect("B", layout.transform, new Vector2(120f, 60f));
            layout.EnsureLayout();

            ReflowTestUtil.AssertRect(b, 10f, 30f, 120f, 60f);
            ReflowTestUtil.AssertRect(a, 10f, 95f, 100f, 50f);
        }

        [Test]
        public void Vertical_CrossExpandFillsWidth()
        {
            ReflowVertical layout = CreateVertical(new Vector2(300f, 500f));
            layout.CrossAxisMode = ReflowChildSizeMode.Expand;
            RectTransform a = ReflowTestUtil.CreateRect("A", layout.transform, new Vector2(100f, 50f));
            RectTransform b = ReflowTestUtil.CreateRect("B", layout.transform, new Vector2(120f, 60f));
            ReflowElement element = b.gameObject.AddComponent<ReflowElement>();
            element.LayoutControlsWidth = false;
            layout.EnsureLayout();

            ReflowTestUtil.AssertRect(a, 10f, 30f, 270f, 50f);
            ReflowTestUtil.AssertRect(b, 10f, 85f, 120f, 60f, "uncontrolled width kept");
        }

        [Test]
        public void Vertical_MainExpandSharesFreeSpace()
        {
            ReflowVertical layout = CreateVertical(new Vector2(300f, 500f));
            layout.MainAxisMode = ReflowChildSizeMode.Expand;
            RectTransform a = ReflowTestUtil.CreateRect("A", layout.transform, new Vector2(100f, 50f));
            RectTransform b = ReflowTestUtil.CreateRect("B", layout.transform, new Vector2(100f, 50f));
            b.gameObject.AddComponent<ReflowElement>().FlexibleHeight = 3f;
            layout.EnsureLayout();

            // 430 inner - 5 spacing = 425 shared 1:3.
            ReflowTestUtil.AssertRect(a, 10f, 30f, 100f, 106.25f);
            ReflowTestUtil.AssertRect(b, 10f, 30f + 106.25f + 5f, 100f, 318.75f);
        }

        [Test]
        public void Vertical_NaturalSizeComesBackAfterExpand()
        {
            ReflowVertical layout = CreateVertical(new Vector2(300f, 500f));
            layout.CrossAxisMode = ReflowChildSizeMode.Expand;
            RectTransform a = ReflowTestUtil.CreateRect("A", layout.transform, new Vector2(100f, 50f));
            layout.EnsureLayout();
            layout.CrossAxisMode = ReflowChildSizeMode.Natural;
            layout.EnsureLayout();

            ReflowTestUtil.AssertRect(a, 10f, 30f, 100f, 50f);
        }

        [Test]
        public void Vertical_FollowCapShrinksChildren()
        {
            ReflowVertical layout = ReflowTestUtil.CreateLayout<ReflowVertical>(_root, new Vector2(100f, 10f));
            layout.HeightPolicy = new ReflowSizePolicy { follow = true, max = 100f };
            RectTransform a = ReflowTestUtil.CreateRect("A", layout.transform, new Vector2(100f, 80f));
            RectTransform b = ReflowTestUtil.CreateRect("B", layout.transform, new Vector2(100f, 80f));
            layout.EnsureLayout();

            ReflowTestUtil.AssertSize(layout.RectTransform, 100f, 100f);
            ReflowTestUtil.AssertRect(a, 0f, 0f, 100f, 50f);
            ReflowTestUtil.AssertRect(b, 0f, 50f, 100f, 50f);
        }

        [Test]
        public void Vertical_FollowCapFeedsFlexibleChild()
        {
            ReflowVertical layout = ReflowTestUtil.CreateLayout<ReflowVertical>(_root, new Vector2(100f, 10f));
            layout.HeightPolicy = new ReflowSizePolicy { follow = true, max = 150f };
            RectTransform header = ReflowTestUtil.CreateRect("Header", layout.transform, new Vector2(100f, 40f));
            RectTransform list = ReflowTestUtil.CreateRect("List", layout.transform, new Vector2(100f, 300f));
            list.gameObject.AddComponent<ReflowElement>();
            layout.EnsureLayout();

            ReflowTestUtil.AssertRect(header, 0f, 0f, 100f, 40f);
            ReflowTestUtil.AssertRect(list, 0f, 40f, 100f, 110f);
        }

        [Test]
        public void Vertical_FollowMinAlignsContent()
        {
            ReflowVertical layout = ReflowTestUtil.CreateLayout<ReflowVertical>(_root, new Vector2(100f, 10f));
            layout.HeightPolicy = new ReflowSizePolicy { follow = true, min = 300f };
            layout.ChildAlignment = TextAnchor.MiddleLeft;
            RectTransform a = ReflowTestUtil.CreateRect("A", layout.transform, new Vector2(100f, 100f));
            layout.EnsureLayout();

            ReflowTestUtil.AssertSize(layout.RectTransform, 100f, 300f);
            ReflowTestUtil.AssertRect(a, 0f, 100f, 100f, 100f);
        }

        [Test]
        public void Vertical_ConstraintSourceCapsHeight()
        {
            RectTransform slot = ReflowTestUtil.CreateRect("Slot", _root, new Vector2(100f, 120f));
            ReflowVertical layout = ReflowTestUtil.CreateLayout<ReflowVertical>(_root, new Vector2(100f, 10f));
            layout.HeightPolicy = new ReflowSizePolicy { follow = true, constraintSource = slot };
            ReflowTestUtil.CreateRect("A", layout.transform, new Vector2(100f, 100f));
            ReflowTestUtil.CreateRect("B", layout.transform, new Vector2(100f, 100f));
            layout.EnsureLayout();
            ReflowTestUtil.AssertSize(layout.RectTransform, 100f, 120f);

            // The source grows: picked up at the next flush.
            slot.sizeDelta = new Vector2(100f, 150f);
            ReflowScheduler.Flush();
            ReflowTestUtil.AssertSize(layout.RectTransform, 100f, 150f);
        }

        [Test]
        public void Vertical_SkipsIgnoredAndInactiveChildren()
        {
            ReflowVertical layout = CreateVertical(new Vector2(300f, 500f));
            RectTransform a = ReflowTestUtil.CreateRect("A", layout.transform, new Vector2(100f, 50f));
            RectTransform ignored = ReflowTestUtil.CreateRect("Ignored", layout.transform, new Vector2(100f, 50f));
            ignored.gameObject.AddComponent<ReflowElement>().IgnoreLayout = true;
            RectTransform inactive = ReflowTestUtil.CreateRect("Inactive", layout.transform, new Vector2(100f, 50f));
            inactive.gameObject.SetActive(false);
            RectTransform b = ReflowTestUtil.CreateRect("B", layout.transform, new Vector2(100f, 50f));
            layout.RefreshAllLayout();
            layout.EnsureLayout();

            ReflowTestUtil.AssertRect(a, 10f, 30f, 100f, 50f);
            ReflowTestUtil.AssertRect(b, 10f, 85f, 100f, 50f);
        }

        [Test]
        public void Vertical_ElementMinMaxClampNaturalSize()
        {
            ReflowVertical layout = CreateVertical(new Vector2(300f, 500f));
            RectTransform a = ReflowTestUtil.CreateRect("A", layout.transform, new Vector2(100f, 50f));
            ReflowElement element = a.gameObject.AddComponent<ReflowElement>();
            element.MinHeight = 70f;
            element.MaxWidth = 80f;
            layout.EnsureLayout();

            ReflowTestUtil.AssertRect(a, 10f, 30f, 80f, 70f);
        }

        [Test]
        public void Vertical_FixedModeUsesDefaultChildSize()
        {
            ReflowVertical layout = CreateVertical(new Vector2(300f, 500f));
            layout.MainAxisMode = ReflowChildSizeMode.Fixed;
            layout.CrossAxisMode = ReflowChildSizeMode.Fixed;
            layout.DefaultChildSize = new Vector2(60f, 40f);
            RectTransform a = ReflowTestUtil.CreateRect("A", layout.transform, new Vector2(100f, 50f));
            layout.EnsureLayout();

            ReflowTestUtil.AssertRect(a, 10f, 30f, 60f, 40f);
        }

        [Test]
        public void Horizontal_ScaleToFitKeepsSizeDelta()
        {
            ReflowHorizontal layout = ReflowTestUtil.CreateLayout<ReflowHorizontal>(_root, new Vector2(500f, 200f));
            layout.MainAxisMode = ReflowChildSizeMode.Fixed;
            layout.CrossAxisMode = ReflowChildSizeMode.Fixed;
            layout.DefaultChildSize = new Vector2(50f, 50f);
            layout.ScaleToFit = true;
            layout.ChildAlignment = TextAnchor.UpperLeft;
            RectTransform a = ReflowTestUtil.CreateRect("A", layout.transform, new Vector2(100f, 100f));
            layout.EnsureLayout();

            Assert.AreEqual(new Vector2(100f, 100f), a.sizeDelta);
            Assert.AreEqual(0.5f, a.localScale.x, ReflowTestUtil.TOLERANCE);
            ReflowTestUtil.AssertRect(a, 0f, 0f, 50f, 50f);
        }

        [Test]
        public void Horizontal_MiddleAlignmentByDefault()
        {
            ReflowHorizontal layout = ReflowTestUtil.CreateLayout<ReflowHorizontal>(_root, new Vector2(500f, 200f));
            RectTransform a = ReflowTestUtil.CreateRect("A", layout.transform, new Vector2(100f, 50f));
            RectTransform b = ReflowTestUtil.CreateRect("B", layout.transform, new Vector2(80f, 100f));
            layout.EnsureLayout();

            ReflowTestUtil.AssertRect(a, 0f, 75f, 100f, 50f);
            ReflowTestUtil.AssertRect(b, 100f, 50f, 80f, 100f);
        }

        [Test]
        public void Nested_FollowChainSettlesInOneLayout()
        {
            ReflowVertical outer = ReflowTestUtil.CreateLayout<ReflowVertical>(_root, new Vector2(400f, 10f));
            outer.FollowHeight = true;
            ReflowHorizontal row = ReflowTestUtil.CreateLayout<ReflowHorizontal>(outer.transform, new Vector2(10f, 10f));
            row.FollowWidth = true;
            row.FollowHeight = true;
            ReflowTestUtil.CreateRect("Icon", row.transform, new Vector2(40f, 40f));
            RectTransform box = ReflowTestUtil.CreateRect("Box", row.transform, new Vector2(100f, 70f));
            outer.EnsureLayout();

            ReflowTestUtil.AssertSize(row.RectTransform, 140f, 70f);
            ReflowTestUtil.AssertSize(outer.RectTransform, 400f, 70f);

            ReflowStats.Reset();
            box.sizeDelta = new Vector2(100f, 90f);
            row.RefreshAllLayout();
            ReflowScheduler.Flush();

            Assert.AreEqual(1, ReflowStats.RootLayoutCount, "one layout pass from the outermost follower");
            ReflowTestUtil.AssertSize(row.RectTransform, 140f, 90f);
            ReflowTestUtil.AssertSize(outer.RectTransform, 400f, 90f);
        }
    }
}
