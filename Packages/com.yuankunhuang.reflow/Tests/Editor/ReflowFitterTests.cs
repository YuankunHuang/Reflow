using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace Reflow.Tests
{
    public class ReflowFitterTests
    {
        private const float UNBOUNDED = 32767f;
        private const string SHORT_TEXT = "Hello World";
        private const string LONG_TEXT = "The quick brown fox jumps over the lazy dog again and again";

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

        /// <summary> A plain text with the same settings, measured directly with TMP. </summary>
        private Vector2 Reference(string pText, float pWidth = UNBOUNDED)
        {
            TextMeshProUGUI text = ReflowTestUtil.CreateText(_root, pText, new Vector2(100f, 100f));
            return text.GetPreferredValues(pWidth, UNBOUNDED);
        }

        [Test]
        public void Preferred_SizesToText()
        {
            ReflowFitter fitter = ReflowTestUtil.CreateFittedText(_root, SHORT_TEXT, ReflowFitter.FitMode.Preferred, ReflowFitter.FitMode.Preferred);
            fitter.EnsureLayout();

            Vector2 expected = Reference(SHORT_TEXT);
            Assert.Greater(expected.x, 10f, "TMP essentials must be imported for this test");
            ReflowTestUtil.AssertSize(fitter.RectTransform, expected.x, expected.y);
        }

        [Test]
        public void Padding_AddsAroundText()
        {
            ReflowFitter fitter = ReflowTestUtil.CreateFittedText(_root, SHORT_TEXT, ReflowFitter.FitMode.Preferred, ReflowFitter.FitMode.Preferred);
            fitter.Padding = new RectOffset(10, 20, 5, 15);
            fitter.EnsureLayout();

            Vector2 expected = Reference(SHORT_TEXT);
            ReflowTestUtil.AssertSize(fitter.RectTransform, expected.x + 30f, expected.y + 20f);
            Assert.AreEqual(new Vector4(10f, 5f, 20f, 15f), fitter.GetComponent<TMP_Text>().margin);
        }

        [Test]
        public void MaxWidth_WrapsAndRemeasuresHeight()
        {
            ReflowFitter fitter = ReflowTestUtil.CreateFittedText(_root, LONG_TEXT, ReflowFitter.FitMode.Preferred, ReflowFitter.FitMode.Preferred);
            fitter.MaxSize = new Vector2(150f, 0f);
            fitter.EnsureLayout();

            Vector2 singleLine = Reference(LONG_TEXT);
            Vector2 wrapped = Reference(LONG_TEXT, 150f);
            Assert.Greater(singleLine.x, 150f);
            ReflowTestUtil.AssertSize(fitter.RectTransform, 150f, wrapped.y);
            Assert.Greater(wrapped.y, singleLine.y * 1.5f, "text wrapped to several lines");
        }

        [Test]
        public void MinSize_KeepsShortTextWide()
        {
            ReflowFitter fitter = ReflowTestUtil.CreateFittedText(_root, "A", ReflowFitter.FitMode.Preferred, ReflowFitter.FitMode.Preferred);
            fitter.MinSize = new Vector2(200f, 0f);
            fitter.EnsureLayout();

            ReflowTestUtil.AssertSize(fitter.RectTransform, 200f, Reference("A").y);
        }

        [Test]
        public void TextChange_IsSeenWithoutRefreshCall()
        {
            ReflowFitter fitter = ReflowTestUtil.CreateFittedText(_root, "A", ReflowFitter.FitMode.Preferred, ReflowFitter.FitMode.Preferred);
            fitter.EnsureLayout();
            Assert.IsFalse(fitter.IsDirty);

            fitter.GetComponent<TMP_Text>().text = SHORT_TEXT;
            Assert.IsTrue(fitter.IsDirty, "TMP's layout-dirty callback marked the fitter");
            fitter.EnsureLayout();

            ReflowTestUtil.AssertSize(fitter.RectTransform, Reference(SHORT_TEXT).x, Reference(SHORT_TEXT).y);
        }

        [Test]
        public void VerticalOnly_UsesOwnWidthForWrapping()
        {
            ReflowFitter fitter = ReflowTestUtil.CreateFittedText(_root, LONG_TEXT, ReflowFitter.FitMode.Unconstrained, ReflowFitter.FitMode.Preferred);
            fitter.RectTransform.sizeDelta = new Vector2(200f, 10f);
            fitter.EnsureLayout();

            ReflowTestUtil.AssertSize(fitter.RectTransform, 200f, Reference(LONG_TEXT, 200f).y);
        }

        [Test]
        public void InsideRow_ContainerFollowsText()
        {
            ReflowHorizontal row = ReflowTestUtil.CreateLayout<ReflowHorizontal>(_root, new Vector2(10f, 10f));
            row.FollowWidth = true;
            row.FollowHeight = true;
            row.Spacing = 8f;
            ReflowTestUtil.CreateRect("Icon", row.transform, new Vector2(40f, 40f));
            ReflowFitter fitter = ReflowTestUtil.CreateFittedText(row.transform, SHORT_TEXT, ReflowFitter.FitMode.Preferred, ReflowFitter.FitMode.Preferred);
            row.EnsureLayout();

            Vector2 text = Reference(SHORT_TEXT);
            ReflowTestUtil.AssertSize(row.RectTransform, 40f + 8f + text.x, Mathf.Max(40f, text.y));

            fitter.GetComponent<TMP_Text>().text = LONG_TEXT;
            row.EnsureLayout();
            ReflowTestUtil.AssertSize(row.RectTransform, 40f + 8f + Reference(LONG_TEXT).x, Mathf.Max(40f, text.y));
        }

        [Test]
        public void ShrunkInsideCappedRow_WrapsText()
        {
            ReflowHorizontal row = ReflowTestUtil.CreateLayout<ReflowHorizontal>(_root, new Vector2(10f, 10f));
            row.WidthPolicy = new ReflowSizePolicy { follow = true, max = 300f };
            row.FollowHeight = true;
            ReflowTestUtil.CreateRect("Icon", row.transform, new Vector2(100f, 20f));
            ReflowFitter fitter = ReflowTestUtil.CreateFittedText(row.transform, LONG_TEXT, ReflowFitter.FitMode.Preferred, ReflowFitter.FitMode.Preferred);
            fitter.gameObject.AddComponent<ReflowElement>();
            row.EnsureLayout();

            // Capped at 300: the flexible text gets 200 and wraps there.
            ReflowTestUtil.AssertSize(row.RectTransform, 300f, Reference(LONG_TEXT, 200f).y);
            ReflowTestUtil.AssertSize(fitter.RectTransform, 200f, Reference(LONG_TEXT, 200f).y);
        }

        [Test]
        public void FitterAddedLater_IsPickedUpByTheParent()
        {
            ReflowVertical layout = ReflowTestUtil.CreateLayout<ReflowVertical>(_root, new Vector2(400f, 400f));
            TextMeshProUGUI text = ReflowTestUtil.CreateText(layout.transform, SHORT_TEXT, new Vector2(20f, 20f));
            layout.EnsureLayout();
            ReflowTestUtil.AssertSize(text.rectTransform, 20f, 20f);

            ReflowFitter fitter = text.gameObject.AddComponent<ReflowFitter>();
            fitter.HorizontalFit = ReflowFitter.FitMode.Preferred;
            fitter.VerticalFit = ReflowFitter.FitMode.Preferred;
            layout.EnsureLayout();

            ReflowTestUtil.AssertSize(text.rectTransform, Reference(SHORT_TEXT).x, Reference(SHORT_TEXT).y);
        }

        [Test]
        public void Source_FollowsAnotherFitter()
        {
            ReflowFitter label = ReflowTestUtil.CreateFittedText(_root, SHORT_TEXT, ReflowFitter.FitMode.Preferred, ReflowFitter.FitMode.Preferred);
            RectTransform background = ReflowTestUtil.CreateRect("Background", _root, new Vector2(10f, 10f));
            ReflowFitter follower = background.gameObject.AddComponent<ReflowFitter>();
            follower.Source = label;
            follower.HorizontalFit = ReflowFitter.FitMode.Preferred;
            follower.VerticalFit = ReflowFitter.FitMode.Preferred;
            follower.Padding = new RectOffset(10, 10, 10, 10);
            follower.EnsureLayout();

            Vector2 text = Reference(SHORT_TEXT);
            ReflowTestUtil.AssertSize(background, text.x + 20f, text.y + 20f);

            label.GetComponent<TMP_Text>().text = LONG_TEXT;
            ReflowScheduler.Flush();
            ReflowTestUtil.AssertSize(background, Reference(LONG_TEXT).x + 20f, text.y + 20f);
        }
    }
}
