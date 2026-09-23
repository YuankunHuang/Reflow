using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;

namespace Reflow
{
    /// <summary>
    /// Frame-coalesced layout queue.
    /// <see cref="ReflowNode.MarkDirty"/> only queues the node's relayout boundary; the queue is flushed
    ///   1. right before LateUpdate scripts run (changes made in Update show up in LateUpdate),
    ///   2. right before RectTransforms and canvases update (changes made in LateUpdate still render this frame),
    ///   3. from <see cref="Canvas.preWillRenderCanvases"/>, which also makes <see cref="Canvas.ForceUpdateCanvases"/>
    ///      a full read barrier, and covers edit mode.
    /// A flush repeats until nothing is left (bounded), so layouts that dirty other layouts still land this frame.
    /// </summary>
    public static class ReflowScheduler
    {
        private const int MAX_PASSES = 8;
        private const int MAX_CHAIN_STEPS = 16;

        private static readonly ProfilerMarker FLUSH_MARKER = new ProfilerMarker("Reflow.Flush");
        private static readonly DepthComparer DEPTH_COMPARER = new DepthComparer();

        private static List<ReflowNode> _pendingList = new List<ReflowNode>(64);
        private static List<ReflowNode> _processingList = new List<ReflowNode>(64);
        private static readonly List<ReflowNode> _sourceWatcherList = new List<ReflowNode>();
        private static bool _isFlushing;
        private static int _writeDepth;
        private static bool _isInstalled;
        private static bool _hasReportedOverflow;

        private struct ReflowFlushBeforeLateUpdate { }

        private struct ReflowFlushBeforeRender { }

        public static bool IsFlushing => _isFlushing;

        /// <summary> Number of queued nodes waiting for the next flush point. </summary>
        public static int PendingCount => _pendingList.Count;

        /// <summary> True while a layout writes rects, so the resulting dimension callbacks are not taken as outside changes. </summary>
        internal static bool IsWriting => _writeDepth > 0;

        /// <summary> Runs every pending layout now. </summary>
        public static void Flush()
        {
            if (_isFlushing)
                return;
            PollSources();
            if (_pendingList.Count == 0)
                return;

            _isFlushing = true;
            ReflowStats.FlushCount++;
            FLUSH_MARKER.Begin();
            try
            {
                int pass = 0;
                while (_pendingList.Count > 0)
                {
                    if (pass++ == MAX_PASSES)
                    {
                        ReportOverflow();
                        break;
                    }

                    List<ReflowNode> swapList = _processingList;
                    _processingList = _pendingList;
                    _pendingList = swapList;

                    SortByDepth(_processingList);
                    for (int i = 0; i < _processingList.Count; i++)
                    {
                        ReflowNode node = _processingList[i];
                        node._isQueued = false;
                        if (node != null)
                            Process(node);
                    }
                    _processingList.Clear();
                    PollSources();
                }
            }
            finally
            {
                _isFlushing = false;
                FLUSH_MARKER.End();
            }
        }

        /// <summary>
        /// Read barrier for one node: runs the queued work on its layout ancestors (outermost first), so its rects are
        /// final. Nodes that are laying out right now are skipped; their result lands when they finish.
        /// </summary>
        internal static void FlushFor(ReflowNode pNode)
        {
            if (!_isFlushing && _pendingList.Count == 0)
                return;

            for (int step = 0; step < MAX_CHAIN_STEPS; step++)
            {
                ReflowNode top = null;
                for (ReflowNode node = pNode; node != null; node = node.ParentLayout)
                {
                    if (node._isQueued && node.HasQueuedWork && !node.IsLayingOut)
                        top = node;
                }
                if (top == null)
                    return;

                Process(top);
                if (top.HasQueuedWork)
                    return;
            }
        }

        internal static void Enqueue(ReflowNode pNode)
        {
            if (pNode._isQueued)
                return;
            pNode._isQueued = true;
            _pendingList.Add(pNode);
            if (!_isInstalled)
                Install();
        }

        internal static void BeginWrite()
        {
            _writeDepth++;
        }

        internal static void EndWrite()
        {
            _writeDepth--;
        }

        /// <summary> Nodes whose size depends on other rects (constraint sources) are polled at every flush. </summary>
        internal static void SetSourceWatcher(ReflowNode pNode, bool pWatch)
        {
            int index = _sourceWatcherList.IndexOf(pNode);
            if (pWatch && index < 0)
                _sourceWatcherList.Add(pNode);
            else if (!pWatch && index >= 0)
                _sourceWatcherList.RemoveAt(index);
        }

        internal static void EnsureInstalled()
        {
            if (!_isInstalled)
                Install();
        }

        private static void Process(ReflowNode pNode)
        {
            try
            {
                pNode.ProcessQueued();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, pNode);
            }
        }

        private static void PollSources()
        {
            for (int i = _sourceWatcherList.Count - 1; i >= 0; i--)
            {
                ReflowNode node = _sourceWatcherList[i];
                if (node == null)
                {
                    _sourceWatcherList.RemoveAt(i);
                    continue;
                }
                node.PollSources();
            }
        }

        private static void SortByDepth(List<ReflowNode> pNodeList)
        {
            if (pNodeList.Count < 2)
                return;
            for (int i = 0; i < pNodeList.Count; i++)
            {
                ReflowNode node = pNodeList[i];
                node._sortDepth = node != null ? GetDepth(node.transform) : 0;
            }
            pNodeList.Sort(DEPTH_COMPARER);
        }

        private static int GetDepth(Transform pTransform)
        {
            int depth = 0;
            for (Transform parent = pTransform.parent; parent != null; parent = parent.parent)
                depth++;
            return depth;
        }

        private static void ReportOverflow()
        {
            if (_hasReportedOverflow)
                return;
            _hasReportedOverflow = true;
            ReflowNode sample = _pendingList.Count > 0 ? _pendingList[0] : null;
            Debug.LogError($"[Reflow] Layout did not settle after {MAX_PASSES} passes; {_pendingList.Count} node(s) carried to the next flush point. " +
                           "Usually a layout callback keeps dirtying a layout it depends on.", sample);
        }

        private static void Install()
        {
            _isInstalled = true;
            Canvas.preWillRenderCanvases -= Flush;
            Canvas.preWillRenderCanvases += Flush;
            if (Application.isPlaying)
                InstallPlayerLoop();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.update -= OnEditorUpdate;
            UnityEditor.EditorApplication.update += OnEditorUpdate;
#endif
        }

        private static void InstallPlayerLoop()
        {
            PlayerLoopSystem root = PlayerLoop.GetCurrentPlayerLoop();
            PlayerLoopSystem.UpdateFunction flush = Flush;
            bool beforeLateUpdate = ReflowPlayerLoopUtil.InsertBefore(ref root, typeof(PreLateUpdate),
                typeof(PreLateUpdate.ScriptRunBehaviourLateUpdate), typeof(ReflowFlushBeforeLateUpdate), flush);
            bool beforeRender = ReflowPlayerLoopUtil.InsertBefore(ref root, typeof(PostLateUpdate),
                                    typeof(PostLateUpdate.UpdateRectTransform), typeof(ReflowFlushBeforeRender), flush)
                                || ReflowPlayerLoopUtil.InsertBefore(ref root, typeof(PostLateUpdate),
                                    typeof(PostLateUpdate.PlayerUpdateCanvases), typeof(ReflowFlushBeforeRender), flush);
            PlayerLoop.SetPlayerLoop(root);

            if (!beforeLateUpdate || !beforeRender)
                Debug.LogWarning("[Reflow] PlayerLoop anchors not found; layouts flush from Canvas.preWillRenderCanvases only.");
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForPlayMode()
        {
            // Also covers "Enter Play Mode" without a domain reload, where statics survive.
            _pendingList.Clear();
            _processingList.Clear();
            _sourceWatcherList.Clear();
            _isFlushing = false;
            _writeDepth = 0;
            _hasReportedOverflow = false;
            Install();
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void InstallInEditor()
        {
            EnsureInstalled();
        }

        private static void OnEditorUpdate()
        {
            if (Application.isPlaying)
                return;
            int writeCount = ReflowStats.RectWriteCount;
            Flush();
            if (writeCount != ReflowStats.RectWriteCount)
            {
                UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
                UnityEditor.SceneView.RepaintAll();
            }
        }
#endif

        private sealed class DepthComparer : IComparer<ReflowNode>
        {
            public int Compare(ReflowNode pA, ReflowNode pB)
            {
                return pA._sortDepth.CompareTo(pB._sortDepth);
            }
        }
    }
}
