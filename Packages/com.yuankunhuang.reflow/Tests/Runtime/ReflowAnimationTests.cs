using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Reflow.Tests
{
    public class ReflowAnimationTests
    {
        private RectTransform _root;
        private GameObject _template;
        private ReflowVertical _layout;

        [SetUp]
        public void SetUp()
        {
            _root = ReflowTestUtil.CreateRoot();
            _template = ReflowTestUtil.CreateItemTemplate(new Vector2(100f, 50f));
            _layout = ReflowTestUtil.CreateLayout<ReflowVertical>(_root, new Vector2(300f, 600f));
        }

        [TearDown]
        public void TearDown()
        {
            ReflowTestUtil.DestroyAll();
        }

        private ReflowSpawnAnimator AddAnimator(ReflowSpawnAnimator.Effect pEffect)
        {
            ReflowSpawnAnimator animator = _layout.gameObject.AddComponent<ReflowSpawnAnimator>();
            animator.SpawnEffect = pEffect;
            animator.Duration = 0.2f;
            animator.Stagger = 0.05f;
            return animator;
        }

        private RectTransform Item(int pIndex)
        {
            return _layout.GetActiveElement(pIndex).RectTransform;
        }

        private static float Alpha(RectTransform pItem)
        {
            CanvasGroup group = pItem.GetComponent<CanvasGroup>();
            return group != null ? group.alpha : 1f;
        }

        [UnityTest]
        public IEnumerator FadeSlide_StartsOffsetAndSettles()
        {
            AddAnimator(ReflowSpawnAnimator.Effect.FadeSlide);
            for (int i = 0; i < 3; i++)
                _layout.AddElement(_template, new ReflowTestData(i));
            _layout.EnsureLayout();

            Assert.AreEqual(0f, Alpha(Item(2)), 0.001f);
            ReflowTestUtil.AssertRect(Item(0), 0f, 40f, 100f, 50f, "starts 40 below its place");
            Assert.IsTrue(_layout.IsAnimating);

            yield return new WaitForSecondsRealtime(0.5f);

            Assert.IsFalse(_layout.IsAnimating);
            for (int i = 0; i < 3; i++)
            {
                Assert.AreEqual(1f, Alpha(Item(i)), 0.001f);
                ReflowTestUtil.AssertRect(Item(i), 0f, i * 50f, 100f, 50f);
            }
        }

        [UnityTest]
        public IEnumerator Scale_StaggersItems()
        {
            AddAnimator(ReflowSpawnAnimator.Effect.Scale);
            for (int i = 0; i < 6; i++)
                _layout.AddElement(_template, new ReflowTestData(i));
            _layout.EnsureLayout();
            yield return new WaitForSecondsRealtime(0.12f);

            Assert.Greater(Item(0).localScale.x, Item(5).localScale.x, "the first item started earlier");
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.AreEqual(1f, Item(5).localScale.x, 0.001f);
        }

        [UnityTest]
        public IEnumerator Despawn_MidAnimation_RestoresTheItem()
        {
            AddAnimator(ReflowSpawnAnimator.Effect.Scale);
            _layout.AddElement(_template, new ReflowTestData(0));
            _layout.EnsureLayout();
            RectTransform item = Item(0);
            yield return null;

            _layout.RemoveElement(0);
            Assert.AreEqual(1f, item.localScale.x, 0.001f);
            Assert.AreEqual(1f, Alpha(item), 0.001f);
            Assert.IsFalse(_layout.IsAnimating);
        }

        [UnityTest]
        public IEnumerator Suppress_SkipsTheNextBatch()
        {
            ReflowSpawnAnimator animator = AddAnimator(ReflowSpawnAnimator.Effect.Fade);
            animator.SuppressNextSpawnAnimation();
            _layout.AddElement(_template, new ReflowTestData(0));
            _layout.EnsureLayout();
            Assert.AreEqual(1f, Alpha(Item(0)), 0.001f);
            Assert.IsFalse(_layout.IsAnimating);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Reflow_SlidesFromOldPlace()
        {
            for (int i = 0; i < 3; i++)
                _layout.AddElement(_template, new ReflowTestData(i));
            _layout.EnsureLayout();
            RectTransform second = Item(1);

            _layout.RemoveElement(0);
            _layout.RefreshWithAnimation(0.3f);
            _layout.EnsureLayout();
            ReflowTestUtil.AssertRect(second, 0f, 50f, 100f, 50f, "still where it was");

            yield return new WaitForSecondsRealtime(0.12f);
            float y = ReflowTestUtil.GetRectInParent(second).y;
            Assert.Greater(y, 0.5f);
            Assert.Less(y, 49.5f);

            // A second call continues from where the item is.
            _layout.RefreshWithAnimation(0.3f);
            _layout.EnsureLayout();
            Assert.AreEqual(y, ReflowTestUtil.GetRectInParent(second).y, 0.5f);

            yield return new WaitForSecondsRealtime(0.5f);
            ReflowTestUtil.AssertRect(second, 0f, 0f, 100f, 50f);
            Assert.IsFalse(_layout.IsAnimating);
        }

        [UnityTest]
        public IEnumerator RelayoutDuringAnimation_MovesTheTarget()
        {
            AddAnimator(ReflowSpawnAnimator.Effect.FadeSlide);
            _layout.AddElement(_template, new ReflowTestData(0));
            _layout.AddElement(_template, new ReflowTestData(1));
            _layout.EnsureLayout();
            RectTransform second = Item(1);
            yield return null;

            _layout.InsertElement(0, _template, new ReflowTestData(9));
            _layout.EnsureLayout();
            yield return new WaitForSecondsRealtime(0.6f);

            ReflowTestUtil.AssertRect(second, 0f, 100f, 100f, 50f);
        }
    }
}
