using System;
using System.Collections.Generic;
using UnityEngine;

namespace Reflow
{
    public abstract partial class ReflowLayout
    {
        internal ReflowSlot[] _slotArray = new ReflowSlot[16];
        internal int _slotCount;
        /// <summary> Scene children come first in the slot array; this many of them. </summary>
        private int _sceneSlotCount;

        private ReflowSceneChild[] _sceneChildArray = new ReflowSceneChild[8];
        private ReflowSceneChild[] _sceneChildScratchArray = new ReflowSceneChild[8];
        private int _sceneChildCount;
        private bool _isSceneChildrenDirty = true;

        private readonly Dictionary<GameObject, Vector2> _prefabSizeDict = new Dictionary<GameObject, Vector2>();
        private readonly Dictionary<GameObject, ReflowElement> _prefabElementDict = new Dictionary<GameObject, ReflowElement>();

#if UNITY_EDITOR
        private DrivenRectTransformTracker _childTracker;
#endif
        private bool _isTrackerDirty = true;

        /// <summary> Whether this layout positions <paramref name="pNode"/> (so a size change of the node matters to it). </summary>
        internal bool IsLayoutChild(ReflowNode pNode)
        {
            RectTransform rectTransform = pNode.RectTransform;
            if (_managedTransformSet.Contains(rectTransform))
                return true;
            if (ExcludeSceneChild(rectTransform))
                return IsExtraChild(rectTransform);
            if (!_includeSceneChildren)
                return false;
            ReflowElement element = pNode.Element;
            return element == null || !element.IgnoreLayout;
        }

        #region Slots

        /// <summary> Collects this solve's children: included scene children first, then visible managed elements. </summary>
        private void BuildSlots()
        {
            EnsureSceneChildren();
            _isVirtualizedNow = ResolveVirtualized();

            int capacity = _sceneChildCount + _entryCount;
            if (_slotArray.Length < capacity)
                _slotArray = new ReflowSlot[Mathf.NextPowerOfTwo(capacity)];
            _slotCount = 0;

            if (_includeSceneChildren)
            {
                for (int i = 0; i < _sceneChildCount; i++)
                {
                    ref ReflowSceneChild child = ref _sceneChildArray[i];
                    RectTransform rectTransform = child.rectTransform;
                    if (rectTransform == null || !child.gameObject.activeSelf)
                        continue;
                    if ((child.element != null && child.element.IgnoreLayout) || ExcludeSceneChild(rectTransform))
                        continue;

                    ref ReflowSlot slot = ref _slotArray[_slotCount++];
                    slot.sceneIndex = i;
                    slot.entryIndex = -1;
                    slot.rectTransform = rectTransform;
                    slot.node = child.node;
                    slot.element = child.element;
                    slot.isManual = child.element != null && child.element.Source == ReflowElement.SizeSource.Manual;
                    slot.natural = slot.isManual
                        ? child.element.ManualSize
                        : ReflowUtil.ResolveNatural(rectTransform.rect.size, ref child.natural, ref child.hasNatural, child.lastWritten, child.hasWritten);
                }
            }
            _sceneSlotCount = _slotCount;

            if (_entryCount == 0)
                return;

            bool spawnAll = !_isVirtualizedNow;
            _suppressChildDirtyDepth++;
            try
            {
                for (int e = 0; e < _entryCount; e++)
                {
                    ref ReflowEntry entry = ref _entryArray[e];
                    if (entry.hidden)
                    {
                        entry.slotIndex = -1;
                        if (entry.record != null)
                            Despawn(e);
                        continue;
                    }
                    if (spawnAll && entry.record == null)
                        Spawn(e);

                    entry.slotIndex = _slotCount;
                    ref ReflowSlot slot = ref _slotArray[_slotCount++];
                    slot.sceneIndex = -1;
                    slot.entryIndex = e;
                    FillEntrySlot(ref slot, ref entry);
                }
            }
            finally
            {
                _suppressChildDirtyDepth--;
            }
        }

        private void FillEntrySlot(ref ReflowSlot pSlot, ref ReflowEntry pEntry)
        {
            ReflowItemRecord record = pEntry.record;
            if (record != null)
            {
                pSlot.rectTransform = record.rectTransform;
                pSlot.node = record.node;
                pSlot.element = record.element;
            }
            else
            {
                pSlot.rectTransform = null;
                pSlot.node = null;
                pSlot.element = GetPrefabElement(pEntry.prefab);
            }

            pSlot.isManual = pSlot.element != null && pSlot.element.Source == ReflowElement.SizeSource.Manual;
            if (pSlot.isManual)
                pSlot.natural = pSlot.element.ManualSize;
            else if (record != null)
                pSlot.natural = ReflowUtil.ResolveNatural(record.rectTransform.rect.size, ref pEntry.natural, ref pEntry.hasNatural, pEntry.lastWritten, pEntry.hasWritten);
            else
                pSlot.natural = EstimateSize(ref pEntry);
        }

        /// <summary> Size of an element that is not spawned: last measured, else the hint, else the prefab, else the default. </summary>
        private Vector2 EstimateSize(ref ReflowEntry pEntry)
        {
            if (pEntry.hasMeasured)
                return pEntry.measured;
            Vector2 size = GetPrefabSize(pEntry.prefab);
            if (pEntry.sizeHint.x > ReflowUtil.EPSILON)
                size.x = pEntry.sizeHint.x;
            if (pEntry.sizeHint.y > ReflowUtil.EPSILON)
                size.y = pEntry.sizeHint.y;
            return size;
        }

        private Vector2 GetPrefabSize(GameObject pPrefab)
        {
            if (pPrefab == null)
                return _defaultChildSize;
            if (!_prefabSizeDict.TryGetValue(pPrefab, out Vector2 size))
            {
                size = _defaultChildSize;
                if (pPrefab.transform is RectTransform rectTransform)
                {
                    Vector2 prefabSize = rectTransform.anchorMin == rectTransform.anchorMax ? rectTransform.sizeDelta : rectTransform.rect.size;
                    if (prefabSize.x > ReflowUtil.EPSILON)
                        size.x = prefabSize.x;
                    if (prefabSize.y > ReflowUtil.EPSILON)
                        size.y = prefabSize.y;
                }
                _prefabSizeDict.Add(pPrefab, size);
            }
            return size;
        }

        private ReflowElement GetPrefabElement(GameObject pPrefab)
        {
            if (pPrefab == null)
                return null;
            if (!_prefabElementDict.TryGetValue(pPrefab, out ReflowElement element))
            {
                pPrefab.TryGetComponent(out element);
                _prefabElementDict.Add(pPrefab, element);
            }
            return element;
        }

        /// <summary> Size of the fixed-size mode on <paramref name="pAxis"/>: the element's size hint, else the default child size. </summary>
        internal float GetFixedChildSize(ref ReflowSlot pSlot, int pAxis)
        {
            if (pSlot.entryIndex >= 0)
            {
                float hint = ReflowUtil.Get(_entryArray[pSlot.entryIndex].sizeHint, pAxis);
                if (hint > ReflowUtil.EPSILON)
                    return hint;
            }
            return ReflowUtil.Get(_defaultChildSize, pAxis);
        }

        /// <summary>
        /// Measures one slot. <paramref name="pExact"/> holds per axis the size the layout will give the child, or
        /// FREE when the child keeps its own. Plain children report their natural size; Reflow nodes measure their content
        /// on the axes they size themselves.
        /// </summary>
        internal Vector2 MeasureSlot(ref ReflowSlot pSlot, Vector2 pExact)
        {
            pSlot.exact = pExact;
            Vector2 natural = pSlot.natural;
            Vector2 desired;
            ReflowNode node = pSlot.node;
            if (node != null && !pSlot.isManual && node.isActiveAndEnabled)
            {
                Vector2 fixedSize = new Vector2(
                    pExact.x >= 0f ? pExact.x : node.FollowsContent(0) ? ReflowUtil.FREE : natural.x,
                    pExact.y >= 0f ? pExact.y : node.FollowsContent(1) ? ReflowUtil.FREE : natural.y);
                desired = node.Measure(fixedSize);
            }
            else
            {
                desired = new Vector2(pExact.x >= 0f ? pExact.x : natural.x, pExact.y >= 0f ? pExact.y : natural.y);
            }

            pSlot.desired = desired;
            if (pSlot.entryIndex >= 0 && pSlot.rectTransform != null)
            {
                ref ReflowEntry entry = ref _entryArray[pSlot.entryIndex];
                entry.measured = desired;
                entry.hasMeasured = true;
            }
            return desired;
        }

        #endregion

        #region Scene children

        private void EnsureSceneChildren()
        {
            if (!_isSceneChildrenDirty)
                return;
            _isSceneChildrenDirty = false;
            _isTrackerDirty = true;

            Transform self = transform;
            int childCount = self.childCount;
            if (_sceneChildScratchArray.Length < childCount)
                _sceneChildScratchArray = new ReflowSceneChild[Mathf.NextPowerOfTwo(childCount)];

            int count = 0;
            for (int i = 0; i < childCount; i++)
            {
                if (!(self.GetChild(i) is RectTransform rectTransform) || _managedTransformSet.Contains(rectTransform))
                    continue;

                ref ReflowSceneChild child = ref _sceneChildScratchArray[count];
                int previous = FindSceneChild(rectTransform, count);
                child = previous >= 0 ? _sceneChildArray[previous] : new ReflowSceneChild { rectTransform = rectTransform, gameObject = rectTransform.gameObject };
                rectTransform.TryGetComponent(out child.element);
                rectTransform.TryGetComponent(out child.node);
                count++;
            }

            // Swap buffers; drop references to children that left.
            ReflowSceneChild[] oldArray = _sceneChildArray;
            int oldCount = _sceneChildCount;
            _sceneChildArray = _sceneChildScratchArray;
            _sceneChildCount = count;
            _sceneChildScratchArray = oldArray;
            Array.Clear(oldArray, 0, oldCount);
        }

        /// <summary> Index of <paramref name="pRect"/> in the current cache, searched from where it most likely is. </summary>
        private int FindSceneChild(RectTransform pRect, int pHint)
        {
            for (int i = pHint; i < _sceneChildCount; i++)
            {
                if (_sceneChildArray[i].rectTransform == pRect)
                    return i;
            }
            for (int i = 0; i < pHint && i < _sceneChildCount; i++)
            {
                if (_sceneChildArray[i].rectTransform == pRect)
                    return i;
            }
            return -1;
        }

        #endregion

        #region Writing

        private void WriteSlots()
        {
            UpdateChildTracker();
            ReflowScheduler.BeginWrite();
            try
            {
                for (int i = 0; i < _slotCount; i++)
                {
                    if (_slotArray[i].rectTransform != null)
                        WriteSlot(ref _slotArray[i]);
                }
            }
            finally
            {
                ReflowScheduler.EndWrite();
            }
        }

        /// <summary> Writes one slot: anchors to the top-left corner, size (or natural size plus scale), position. </summary>
        internal void WriteSlot(ref ReflowSlot pSlot)
        {
            RectTransform rectTransform = pSlot.rectTransform;
            ReflowUtil.AnchorTopLeft(rectTransform);

            Vector2 size = pSlot.size;
            Vector2 sizeDelta = size;
            bool scaleToFit = UsesScaleToFit && pSlot.natural.x > ReflowUtil.EPSILON && pSlot.natural.y > ReflowUtil.EPSILON;
            ReflowItemRecord record = pSlot.entryIndex >= 0 ? _entryArray[pSlot.entryIndex].record : null;

            Vector3 scale = record != null ? record.targetScale : rectTransform.localScale;
            if (scaleToFit)
            {
                sizeDelta = pSlot.natural;
                scale = new Vector3(size.x / sizeDelta.x, size.y / sizeDelta.y, scale.z);
            }

            if (rectTransform.sizeDelta != sizeDelta)
            {
                rectTransform.sizeDelta = sizeDelta;
                ReflowStats.RectWriteCount++;
            }

            Vector2 position = ReflowUtil.GetAnchoredPosition(rectTransform, pSlot.position, size);
            if (record != null)
            {
                record.targetPosition = position;
                record.targetScale = scale;
                position += record.offset;
                scale *= record.scaleFactor;
            }

            if ((scaleToFit || (record != null && (record.IsAnimating || record.scaleFactor != 1f))) && rectTransform.localScale != scale)
                rectTransform.localScale = scale;
            if (rectTransform.anchoredPosition != position)
            {
                rectTransform.anchoredPosition = position;
                ReflowStats.RectWriteCount++;
            }

            if (pSlot.sceneIndex >= 0)
            {
                ref ReflowSceneChild child = ref _sceneChildArray[pSlot.sceneIndex];
                child.lastWritten = sizeDelta;
                child.hasWritten = true;
            }
            else if (pSlot.entryIndex >= 0)
            {
                ref ReflowEntry entry = ref _entryArray[pSlot.entryIndex];
                entry.lastWritten = sizeDelta;
                entry.hasWritten = true;
            }
        }

        /// <summary> Child layouts and fitters lay out their own content inside the size they were given. </summary>
        private void ArrangeChildNodes()
        {
            bool scaleToFit = UsesScaleToFit;
            for (int i = 0; i < _slotCount; i++)
            {
                ref ReflowSlot slot = ref _slotArray[i];
                ReflowNode node = slot.node;
                if (node == null || slot.rectTransform == null || !node.isActiveAndEnabled)
                    continue;
                node.Arrange(scaleToFit ? slot.natural : slot.size);
            }
        }

        private void UpdateChildTracker()
        {
#if UNITY_EDITOR
            if (!_isTrackerDirty)
                return;
            _isTrackerDirty = false;
            _childTracker.Clear();
            bool scaleToFit = UsesScaleToFit;
            for (int i = 0; i < _slotCount; i++)
            {
                ref ReflowSlot slot = ref _slotArray[i];
                if (slot.rectTransform == null)
                    continue;
                DrivenTransformProperties properties = DrivenTransformProperties.Anchors | DrivenTransformProperties.AnchoredPosition;
                if (scaleToFit)
                    properties |= DrivenTransformProperties.Scale;
                else
                {
                    if (IsSizeDriven(ref slot, 0))
                        properties |= DrivenTransformProperties.SizeDeltaX;
                    if (IsSizeDriven(ref slot, 1))
                        properties |= DrivenTransformProperties.SizeDeltaY;
                }
                _childTracker.Add(this, slot.rectTransform, properties);
            }
#endif
        }

        /// <summary> Whether the size on an axis comes from the layout or the child's content rather than from the rect itself. </summary>
        private static bool IsSizeDriven(ref ReflowSlot pSlot, int pAxis)
        {
            if (ReflowUtil.Get(pSlot.exact, pAxis) >= 0f || pSlot.isManual)
                return true;
            return pSlot.node != null && pSlot.node.FollowsContent(pAxis);
        }

        private void ClearChildTracker()
        {
#if UNITY_EDITOR
            _childTracker.Clear();
#endif
            _isTrackerDirty = true;
        }

        #endregion
    }
}
