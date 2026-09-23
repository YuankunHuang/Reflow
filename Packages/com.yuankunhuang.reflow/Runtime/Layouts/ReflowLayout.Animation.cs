using UnityEngine;

namespace Reflow
{
    public abstract partial class ReflowLayout
    {
        private float _pendingReflowDuration;

        /// <summary> Whether a spawn or reflow animation is running on one of this layout's items. </summary>
        public bool IsAnimating => ReflowAnimationDriver.IsAnimating(this);

        /// <summary>
        /// Relayouts and slides the items that are already spawned from where they are now to their new place.
        /// Items spawned by this layout appear in place (or through the spawn animator). Calling it again while
        /// items move continues from where they are.
        /// </summary>
        public void RefreshWithAnimation(float pDuration = -1f)
        {
            _pendingReflowDuration = pDuration >= 0f ? pDuration : _reflowDuration;
            for (int i = 0; i < _spawnedRecordList.Count; i++)
            {
                ReflowItemRecord record = _spawnedRecordList[i];
                record.reflowFrom = record.rectTransform.anchoredPosition;
                record.hasPendingReflow = true;
            }
            SetDirty(false);
        }

        /// <summary> Visual (row, column) of an element for staggered animations; (-1, -1) when it is not laid out. </summary>
        internal Vector2Int GetGridCoordinate(int pEntryIndex)
        {
            if (!TryGetEntrySlot(pEntryIndex, out int slotIndex))
                return new Vector2Int(-1, -1);
            ref ReflowSlot slot = ref _slotArray[slotIndex];
            return new Vector2Int(slot.row, slot.column);
        }

        /// <summary> Starts reflow motion for items captured by <see cref="RefreshWithAnimation"/> and hands new items to the spawn animator. </summary>
        private void FinishAnimations()
        {
            for (int i = 0; i < _spawnedRecordList.Count; i++)
            {
                ReflowItemRecord record = _spawnedRecordList[i];
                if (!record.hasPendingReflow)
                    continue;
                record.hasPendingReflow = false;
                Vector2 from = record.reflowFrom - record.targetPosition;
                if (from.sqrMagnitude > ReflowUtil.EPSILON && _pendingReflowDuration > 0f && Application.isPlaying)
                    ReflowAnimationDriver.StartReflow(record, from, _pendingReflowDuration);
            }

            if (_appearedRecordList.Count == 0)
                return;
            if (_spawnAnimator != null && _spawnAnimator.isActiveAndEnabled && Application.isPlaying)
                _spawnAnimator.Play(_appearedRecordList);
            _appearedRecordList.Clear();
        }
    }
}
