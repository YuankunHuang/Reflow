using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace Reflow.Tests
{
    /// <summary> Changes land in the frame they are made, with one layout per boundary per frame. </summary>
    public class ReflowFrameTimingTests
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

        private ReflowFrameProbe CreateProbe()
        {
            return _root.gameObject.AddComponent<ReflowFrameProbe>();
        }

        [UnityTest]
        public IEnumerator FlushPoints_AreInThePlayerLoop()
        {
            yield return null;
            UnityEngine.LowLevel.PlayerLoopSystem root = UnityEngine.LowLevel.PlayerLoop.GetCurrentPlayerLoop();
            Assert.AreEqual(0, IndexIn(root, "PreLateUpdate", "ReflowFlushBeforeLateUpdate") - IndexIn(root, "PreLateUpdate", "ScriptRunBehaviourLateUpdate") + 1,
                "flush runs right before LateUpdate scripts");
            Assert.AreEqual(0, IndexIn(root, "PostLateUpdate", "ReflowFlushBeforeRender") - IndexIn(root, "PostLateUpdate", "UpdateRectTransform") + 1,
                "flush runs right before rect transforms and canvases update");
        }

        private static int IndexIn(UnityEngine.LowLevel.PlayerLoopSystem pRoot, string pPhase, string pSystem)
        {
            foreach (UnityEngine.LowLevel.PlayerLoopSystem phase in pRoot.subSystemList)
            {
                if (phase.type == null || phase.type.Name != pPhase)
                    continue;
                for (int i = 0; i < phase.subSystemList.Length; i++)
                {
                    if (phase.subSystemList[i].type != null && phase.subSystemList[i].type.Name == pSystem)
                        return i;
                }
            }
            return -100;
        }

        [UnityTest]
        public IEnumerator ChangeInUpdate_IsVisibleInLateUpdate()
        {
            ReflowVertical layout = ReflowTestUtil.CreateLayout<ReflowVertical>(_root, new Vector2(200f, 10f));
            layout.FollowHeight = true;
            RectTransform child = ReflowTestUtil.CreateRect("A", layout.transform, new Vector2(100f, 50f));
            yield return null;

            float heightInLateUpdate = 0f;
            bool dirtyInLateUpdate = true;
            ReflowFrameProbe probe = CreateProbe();
            probe.onUpdate = () =>
            {
                child.sizeDelta = new Vector2(100f, 130f);
                layout.RefreshAllLayout();
            };
            probe.onLateUpdate = () =>
            {
                heightInLateUpdate = layout.RectTransform.rect.height;
                dirtyInLateUpdate = layout.IsDirty;
            };
            // The probe runs next frame; a coroutine resumes before that frame's LateUpdate, so wait one more.
            yield return null;
            yield return null;

            Assert.IsFalse(dirtyInLateUpdate);
            Assert.AreEqual(130f, heightInLateUpdate, ReflowTestUtil.TOLERANCE);
        }

        [UnityTest]
        public IEnumerator ChangeInLateUpdate_IsLaidOutBeforeCanvasesRender()
        {
            ReflowFitter fitter = ReflowTestUtil.CreateFittedText(_root, "A", ReflowFitter.FitMode.Preferred, ReflowFitter.FitMode.Preferred);
            yield return null;

            bool dirtyAtRender = true;
            float widthAtRender = 0f;
            Canvas.WillRenderCanvases onRender = () =>
            {
                dirtyAtRender = fitter.IsDirty;
                widthAtRender = fitter.RectTransform.rect.width;
            };

            ReflowFrameProbe probe = CreateProbe();
            probe.onLateUpdate = () =>
            {
                fitter.GetComponent<TMP_Text>().text = "A much longer label";
                Canvas.willRenderCanvases += onRender;
            };
            // The probe runs next frame; a coroutine resumes before that frame's LateUpdate, so wait one more.
            yield return null;
            yield return null;
            Canvas.willRenderCanvases -= onRender;

            float expected = fitter.GetComponent<TMP_Text>().GetPreferredValues(32767f, 32767f).x;
            Assert.IsFalse(dirtyAtRender);
            Assert.AreEqual(expected, widthAtRender, ReflowTestUtil.TOLERANCE);
        }

        [UnityTest]
        public IEnumerator ManyChangesInAFrame_OneLayoutPerBoundary()
        {
            ReflowVertical outer = ReflowTestUtil.CreateLayout<ReflowVertical>(_root, new Vector2(400f, 10f), "Outer");
            outer.FollowHeight = true;
            ReflowFitter[] fitterArray = new ReflowFitter[5];
            for (int i = 0; i < fitterArray.Length; i++)
                fitterArray[i] = ReflowTestUtil.CreateFittedText(outer.transform, "Line " + i, ReflowFitter.FitMode.Preferred, ReflowFitter.FitMode.Preferred);
            yield return null;

            ReflowStats.Reset();
            ReflowFrameProbe probe = CreateProbe();
            probe.onUpdate = () =>
            {
                for (int round = 0; round < 3; round++)
                {
                    for (int i = 0; i < fitterArray.Length; i++)
                        fitterArray[i].GetComponent<TMP_Text>().text = $"Line {i} round {round}";
                    outer.RefreshAllLayout();
                }
            };
            // The probe runs next frame; a coroutine resumes before that frame's LateUpdate, so wait one more.
            yield return null;
            yield return null;

            Assert.AreEqual(1, ReflowStats.RootLayoutCount, "15 text changes and 3 refresh calls, one layout");
            Assert.AreEqual(1, ReflowStats.SolveCount);
        }

        [UnityTest]
        public IEnumerator NormalizedPosition_IsComputedFromThisFramesContent()
        {
            GameObject template = ReflowTestUtil.CreateItemTemplate(new Vector2(100f, 50f));
            ReflowVertical layout = ReflowTestUtil.CreateScrollList<ReflowVertical>(_root, new Vector2(300f, 400f), true, out ReflowScrollRect scrollRect);
            for (int i = 0; i < 100; i++)
                layout.AddElement(template, new ReflowTestData(i));

            // Same frame as the adds: the scroll rect settles the layout before applying the position.
            scrollRect.verticalNormalizedPosition = 0f;
            Assert.AreEqual(5000f - 400f, layout.RectTransform.anchoredPosition.y, 1f);
            yield return null;

            Assert.IsNotNull(layout.GetActiveElement(99));
            Assert.IsNull(layout.GetActiveElement(0));
        }

        [UnityTest]
        public IEnumerator Dragging_SpawnsInTheSameFrame()
        {
            GameObject template = ReflowTestUtil.CreateItemTemplate(new Vector2(100f, 50f));
            ReflowVertical layout = ReflowTestUtil.CreateScrollList<ReflowVertical>(_root, new Vector2(300f, 400f), true, out ReflowScrollRect scrollRect);
            for (int i = 0; i < 100; i++)
                layout.AddElement(template, new ReflowTestData(i));
            yield return null;

            bool visibleAtRender = false;
            Canvas.WillRenderCanvases onRender = () => visibleAtRender = layout.GetActiveElementNoLayout(40) != null;
            ReflowFrameProbe probe = CreateProbe();
            probe.onUpdate = () =>
            {
                layout.RectTransform.anchoredPosition = new Vector2(0f, 2000f);
                Canvas.willRenderCanvases += onRender;
            };
            // The probe runs next frame; a coroutine resumes before that frame's LateUpdate, so wait one more.
            yield return null;
            yield return null;
            Canvas.willRenderCanvases -= onRender;

            Assert.IsTrue(visibleAtRender, "the ScrollRect reported the move in LateUpdate and the item spawned before render");
        }
    }
}
