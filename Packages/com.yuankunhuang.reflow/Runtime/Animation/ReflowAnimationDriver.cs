using System.Collections.Generic;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;

namespace Reflow
{
    /// <summary>
    /// Runs every spawn and reflow animation from one PlayerLoop update (end of Update, before the layout flush),
    /// not one MonoBehaviour per layout. An animation moves an item from an offset / alpha / scale back to where
    /// the layout put it, so a relayout during the animation just moves the target.
    /// </summary>
    internal static class ReflowAnimationDriver
    {
        private static readonly List<ReflowItemRecord> _activeList = new List<ReflowItemRecord>(32);
        private static bool _isInstalled;

        private struct ReflowAnimationUpdate { }

        internal static void StartAppear(ReflowItemRecord pRecord, Vector2 pOffsetFrom, float pAlphaFrom, float pScaleFrom, float pDelay, float pDuration)
        {
            if (pAlphaFrom < 1f)
            {
                pRecord.EnsureCanvasGroup();
                pRecord.animatesAlpha = true;
                pRecord.alphaFrom = pAlphaFrom * pRecord.baseAlpha;
            }
            else
            {
                pRecord.animatesAlpha = false;
            }
            pRecord.animatesScale = !Mathf.Approximately(pScaleFrom, 1f);
            pRecord.scaleFrom = pScaleFrom;
            pRecord.offsetFrom = pOffsetFrom;
            Begin(pRecord, pDelay, pDuration);
        }

        /// <summary> Slides from the current visual state; alpha and scale continue from where they are. </summary>
        internal static void StartReflow(ReflowItemRecord pRecord, Vector2 pOffsetFrom, float pDuration)
        {
            if (pRecord.animatesAlpha && pRecord.canvasGroup != null)
                pRecord.alphaFrom = pRecord.canvasGroup.alpha;
            pRecord.scaleFrom = pRecord.scaleFactor;
            pRecord.offsetFrom = pOffsetFrom;
            Begin(pRecord, 0f, pDuration);
        }

        /// <summary> Jumps to the end state and stops. </summary>
        internal static void Complete(ReflowItemRecord pRecord)
        {
            if (!pRecord.IsAnimating)
                return;
            Apply(pRecord, 1f);
            Remove(pRecord.animationIndex);
        }

        internal static void CompleteAll(ReflowLayout pOwner)
        {
            for (int i = _activeList.Count - 1; i >= 0; i--)
            {
                if (i < _activeList.Count && _activeList[i].owner == pOwner)
                    Complete(_activeList[i]);
            }
        }

        internal static bool IsAnimating(ReflowLayout pOwner)
        {
            for (int i = 0; i < _activeList.Count; i++)
            {
                if (_activeList[i].owner == pOwner)
                    return true;
            }
            return false;
        }

        private static void Begin(ReflowItemRecord pRecord, float pDelay, float pDuration)
        {
            if (!_isInstalled)
                Install();
            pRecord.animationStart = Time.unscaledTime;
            pRecord.animationDelay = Mathf.Max(0f, pDelay);
            pRecord.animationDuration = Mathf.Max(0f, pDuration);
            if (pRecord.animationIndex < 0)
            {
                pRecord.animationIndex = _activeList.Count;
                _activeList.Add(pRecord);
            }
            Apply(pRecord, 0f);
        }

        private static void Tick()
        {
            float now = Time.unscaledTime;
            for (int i = _activeList.Count - 1; i >= 0; i--)
            {
                if (i >= _activeList.Count)
                    continue;
                ReflowItemRecord record = _activeList[i];
                if (record.rectTransform == null)
                {
                    Remove(i);
                    continue;
                }

                float elapsed = now - record.animationStart - record.animationDelay;
                if (elapsed < 0f)
                    continue;
                float t = record.animationDuration > 0f ? elapsed / record.animationDuration : 1f;
                if (t >= 1f)
                {
                    Apply(record, 1f);
                    Remove(i);
                }
                else
                {
                    Apply(record, EaseOutCubic(t));
                }
            }
        }

        private static float EaseOutCubic(float pT)
        {
            float inverse = 1f - pT;
            return 1f - inverse * inverse * inverse;
        }

        /// <param name="pProgress"> 0 = start state, 1 = where the layout put the item. </param>
        private static void Apply(ReflowItemRecord pRecord, float pProgress)
        {
            RectTransform rectTransform = pRecord.rectTransform;
            if (rectTransform == null)
                return;

            pRecord.offset = pProgress >= 1f ? Vector2.zero : pRecord.offsetFrom * (1f - pProgress);
            pRecord.scaleFactor = pProgress >= 1f ? 1f : Mathf.LerpUnclamped(pRecord.scaleFrom, 1f, pProgress);

            ReflowScheduler.BeginWrite();
            try
            {
                rectTransform.anchoredPosition = pRecord.targetPosition + pRecord.offset;
                if (pRecord.animatesScale || pProgress >= 1f)
                    rectTransform.localScale = pRecord.targetScale * pRecord.scaleFactor;
            }
            finally
            {
                ReflowScheduler.EndWrite();
            }

            if (pRecord.animatesAlpha && pRecord.canvasGroup != null)
                pRecord.canvasGroup.alpha = pProgress >= 1f ? pRecord.baseAlpha : Mathf.LerpUnclamped(pRecord.alphaFrom, pRecord.baseAlpha, pProgress);
            if (pProgress >= 1f)
            {
                pRecord.animatesAlpha = false;
                pRecord.animatesScale = false;
            }
        }

        private static void Remove(int pIndex)
        {
            int last = _activeList.Count - 1;
            ReflowItemRecord removed = _activeList[pIndex];
            ReflowItemRecord moved = _activeList[last];
            _activeList[pIndex] = moved;
            moved.animationIndex = pIndex;
            _activeList.RemoveAt(last);
            removed.animationIndex = -1;
        }

        private static void Install()
        {
            _isInstalled = true;
            if (!Application.isPlaying)
                return;
            PlayerLoopSystem root = PlayerLoop.GetCurrentPlayerLoop();
            if (ReflowPlayerLoopUtil.Append(ref root, typeof(Update), typeof(ReflowAnimationUpdate), Tick))
                PlayerLoop.SetPlayerLoop(root);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForPlayMode()
        {
            _activeList.Clear();
            _isInstalled = false;
        }
    }
}
