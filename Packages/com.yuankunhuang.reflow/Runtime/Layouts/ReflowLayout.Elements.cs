using System;
using System.Collections.Generic;
using UnityEngine;

namespace Reflow
{
    public abstract partial class ReflowLayout
    {
        private ReflowEntry[] _entryArray = new ReflowEntry[16];
        private ReflowEntry[] _entryScratchArray = Array.Empty<ReflowEntry>();
        private int[] _oldToNewArray = Array.Empty<int>();
        private int _entryCount;

        private readonly ReflowPool _pool = new ReflowPool();
        private readonly HashSet<Transform> _managedTransformSet = new HashSet<Transform>();
        private readonly List<ReflowItemRecord> _spawnedRecordList = new List<ReflowItemRecord>();
        private readonly List<ReflowItemRecord> _deactivateList = new List<ReflowItemRecord>();
        private readonly List<ReflowItemRecord> _appearedRecordList = new List<ReflowItemRecord>();
        private bool _isCreatingItem;

        /// <summary> After <see cref="IReflowItem.Show"/>. Subscribe extra item events here. </summary>
        public event Action<int, IReflowItem> ElementSpawned;

        /// <summary> Before <see cref="IReflowItem.Hide"/>. Unsubscribe here. </summary>
        public event Action<int, IReflowItem> ElementDespawned;

        /// <summary> Elements were inserted, removed, reordered or cleared. Use <see cref="ReflowElementChange.MapIndex"/> to remap stored indices. </summary>
        public event Action<ReflowElementChange> ElementsChanged;

        public int ElementCount => _entryCount;

        public int SpawnedCount => _spawnedRecordList.Count;

        /// <summary> Items created by the pool so far (in use plus free). </summary>
        public int PooledItemCount => _pool.CreatedCount;

        /// <summary> Sets the factory that creates and destroys items. Optional: the default instantiates the prefab. </summary>
        public void InitOwner(IReflowItemFactory pFactory)
        {
            _pool.SetFactory(pFactory);
        }

        /// <param name="pPrefab"> Item prefab. Its <see cref="IReflowItem"/> component receives <paramref name="pData"/>. </param>
        /// <param name="pSizeHint"> Size before the item is spawned (and the size in Fixed mode). Zero on an axis: the prefab's size. </param>
        /// <param name="pPoolKey"> Items with the same key share a pool. Default: the prefab. </param>
        public void AddElement(GameObject pPrefab, object pData = null, Vector2 pSizeHint = default, object pPoolKey = null)
        {
            InsertElement(_entryCount, pPrefab, pData, pSizeHint, pPoolKey);
        }

        public void InsertElement(int pIndex, GameObject pPrefab, object pData = null, Vector2 pSizeHint = default, object pPoolKey = null)
        {
            ThrowIfLayingOut();
            if (pPrefab == null)
                throw new ArgumentNullException(nameof(pPrefab));
            if (pIndex < 0 || pIndex > _entryCount)
                throw new ArgumentOutOfRangeException(nameof(pIndex));

            EnsureEntryCapacity(_entryCount + 1);
            if (pIndex < _entryCount)
            {
                Array.Copy(_entryArray, pIndex, _entryArray, pIndex + 1, _entryCount - pIndex);
                ShiftSpawnedIndices(pIndex, 1);
            }
            _entryArray[pIndex] = new ReflowEntry
            {
                prefab = pPrefab,
                poolKey = pPoolKey ?? pPrefab,
                data = pData,
                sizeHint = pSizeHint,
                slotIndex = -1
            };
            _entryCount++;

            RaiseElementsChanged(new ReflowElementChange(ReflowElementChangeKind.Inserted, pIndex, 1));
            SetDirty(false);
        }

        public void RemoveElement(int pIndex)
        {
            RemoveElements(pIndex, 1);
        }

        public void RemoveElements(int pIndex, int pCount)
        {
            ThrowIfLayingOut();
            if (pCount <= 0)
                return;
            if (pIndex < 0 || pIndex + pCount > _entryCount)
                throw new ArgumentOutOfRangeException(nameof(pIndex));

            for (int i = pIndex; i < pIndex + pCount; i++)
            {
                if (_entryArray[i].record != null)
                    Despawn(i);
            }
            FlushDeactivations();

            int tail = _entryCount - pIndex - pCount;
            if (tail > 0)
                Array.Copy(_entryArray, pIndex + pCount, _entryArray, pIndex, tail);
            Array.Clear(_entryArray, _entryCount - pCount, pCount);
            _entryCount -= pCount;
            ShiftSpawnedIndices(pIndex + pCount, -pCount);

            RaiseElementsChanged(new ReflowElementChange(ReflowElementChangeKind.Removed, pIndex, pCount));
            SetDirty(false);
        }

        /// <summary>
        /// Reorders elements without rebinding: <paramref name="pNewOrder"/>[newIndex] = oldIndex. Spawned items keep
        /// their data and move to the new positions.
        /// </summary>
        public void ReorderElements(IReadOnlyList<int> pNewOrder, bool pKeepScrollAnchor = true)
        {
            ThrowIfLayingOut();
            if (pNewOrder == null || pNewOrder.Count != _entryCount)
                throw new ArgumentException("[Reflow] New order must list every element once.", nameof(pNewOrder));

            ReflowScrollAnchor anchor = pKeepScrollAnchor ? CaptureScrollAnchor() : ReflowScrollAnchor.None;

            int count = _entryCount;
            if (_oldToNewArray.Length < count)
                _oldToNewArray = new int[Mathf.NextPowerOfTwo(count)];
            if (_entryScratchArray.Length < _entryArray.Length)
                _entryScratchArray = new ReflowEntry[_entryArray.Length];
            for (int i = 0; i < count; i++)
                _oldToNewArray[i] = -1;
            for (int newIndex = 0; newIndex < count; newIndex++)
            {
                int oldIndex = pNewOrder[newIndex];
                if (oldIndex < 0 || oldIndex >= count || _oldToNewArray[oldIndex] >= 0)
                    throw new ArgumentException("[Reflow] New order must list every element once.", nameof(pNewOrder));
                _oldToNewArray[oldIndex] = newIndex;
                _entryScratchArray[newIndex] = _entryArray[oldIndex];
            }

            ReflowEntry[] swapArray = _entryArray;
            _entryArray = _entryScratchArray;
            _entryScratchArray = swapArray;
            Array.Clear(_entryScratchArray, 0, count);

            for (int i = 0; i < _spawnedRecordList.Count; i++)
            {
                ReflowItemRecord record = _spawnedRecordList[i];
                record.entryIndex = _oldToNewArray[record.entryIndex];
            }

            RaiseElementsChanged(new ReflowElementChange(ReflowElementChangeKind.Reordered, 0, count, _oldToNewArray));
            SetDirty(false);

            if (anchor.IsValid)
            {
                anchor.index = _oldToNewArray[anchor.index];
                RestoreScrollAnchor(anchor);
            }
        }

        /// <summary> Replaces an element's data; a spawned item is shown again with it. </summary>
        public void UpdateElementData(int pIndex, object pData)
        {
            CheckIndex(pIndex);
            ref ReflowEntry entry = ref _entryArray[pIndex];
            entry.data = pData;
            ReflowItemRecord record = entry.record;
            if (record != null)
            {
                try
                {
                    record.item.Show(pData);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                }
            }
            SetDirty(false);
        }

        /// <summary> Size used before the element is spawned, and in Fixed mode. </summary>
        public void SetElementSizeHint(int pIndex, Vector2 pSizeHint)
        {
            CheckIndex(pIndex);
            ref ReflowEntry entry = ref _entryArray[pIndex];
            entry.sizeHint = pSizeHint;
            entry.hasMeasured = false;
            SetDirty(false);
        }

        /// <summary> A hidden element keeps its index and data but takes no space and is not spawned. </summary>
        public void SetElementHidden(int pIndex, bool pHidden)
        {
            CheckIndex(pIndex);
            if (_entryArray[pIndex].hidden == pHidden)
                return;
            _entryArray[pIndex].hidden = pHidden;
            SetDirty(false);
        }

        public bool IsElementHidden(int pIndex)
        {
            CheckIndex(pIndex);
            return _entryArray[pIndex].hidden;
        }

        public object GetElementData(int pIndex)
        {
            CheckIndex(pIndex);
            return _entryArray[pIndex].data;
        }

        public bool TryGetElementData<T>(int pIndex, out T pData)
        {
            if (pIndex >= 0 && pIndex < _entryCount && _entryArray[pIndex].data is T data)
            {
                pData = data;
                return true;
            }
            pData = default;
            return false;
        }

        /// <summary> Removes every element; spawned items go back to the pool. </summary>
        public void Clear()
        {
            ThrowIfLayingOut();
            for (int i = _spawnedRecordList.Count - 1; i >= 0; i--)
                Despawn(_spawnedRecordList[i].entryIndex);
            FlushDeactivations();
            Array.Clear(_entryArray, 0, _entryCount);
            _entryCount = 0;
            RaiseElementsChanged(new ReflowElementChange(ReflowElementChangeKind.Cleared, 0, 0));
            SetDirty(false);
        }

        /// <summary> Destroys pooled items that are not in use. </summary>
        public void ClearPool()
        {
            FlushDeactivations();
            _pool.DestroyFree(pRecord =>
            {
                _managedTransformSet.Remove(pRecord.rectTransform);
                ReflowAnimationDriver.Complete(pRecord);
            });
        }

        /// <summary> Every spawned item is hidden and spawned again (Show and the spawn events run again). </summary>
        public void RebindAll()
        {
            ThrowIfLayingOut();
            for (int i = _spawnedRecordList.Count - 1; i >= 0; i--)
                Despawn(_spawnedRecordList[i].entryIndex);
            FlushDeactivations();
            SetDirty(false);
        }

        /// <summary> Elements count as never spawned again, so a spawn animator plays for them on their next spawn. </summary>
        public void ResetSpawnHistory()
        {
            for (int i = 0; i < _entryCount; i++)
                _entryArray[i].spawnedOnce = false;
            SetDirty(false);
        }

        internal int SpawnedRecordCount => _spawnedRecordList.Count;

        /// <summary> The <paramref name="pSpawnedIndex"/>-th spawned item (no particular order) and its element index. </summary>
        internal IReflowItem GetSpawnedItem(int pSpawnedIndex, out int pElementIndex)
        {
            ReflowItemRecord record = _spawnedRecordList[pSpawnedIndex];
            pElementIndex = record.entryIndex;
            return record.item;
        }

        /// <summary> Index of the element <paramref name="pItem"/> shows, if it is spawned here. </summary>
        public bool TryGetElementIndex(IReflowItem pItem, out int pIndex)
        {
            for (int i = 0; i < _spawnedRecordList.Count; i++)
            {
                ReflowItemRecord record = _spawnedRecordList[i];
                if (record.item == pItem)
                {
                    pIndex = record.entryIndex;
                    return true;
                }
            }
            pIndex = -1;
            return false;
        }

        #region Spawning

        private void Spawn(int pEntryIndex)
        {
            ref ReflowEntry entry = ref _entryArray[pEntryIndex];
            _suppressChildDirtyDepth++;
            try
            {
                _isCreatingItem = true;
                ReflowItemRecord record;
                try
                {
                    record = _pool.Acquire(entry.prefab, entry.poolKey, this, out bool isNew);
                    if (isNew)
                        _managedTransformSet.Add(record.rectTransform);
                }
                finally
                {
                    _isCreatingItem = false;
                }

                record.entryIndex = pEntryIndex;
                record.spawnedListIndex = _spawnedRecordList.Count;
                _spawnedRecordList.Add(record);
                entry.record = record;
                _isTrackerDirty = true;
                ReflowStats.SpawnCount++;

                record.ResetForSpawn();
                if (!record.gameObject.activeSelf)
                    record.gameObject.SetActive(true);
                try
                {
                    record.item.Show(entry.data);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                }

                entry.natural = record.rectTransform.rect.size;
                entry.hasNatural = true;
                entry.hasWritten = false;
                if (!entry.spawnedOnce)
                {
                    entry.spawnedOnce = true;
                    _appearedRecordList.Add(record);
                }

                try
                {
                    ElementSpawned?.Invoke(pEntryIndex, record.item);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                }
            }
            finally
            {
                _suppressChildDirtyDepth--;
            }
        }

        private void Despawn(int pEntryIndex)
        {
            ref ReflowEntry entry = ref _entryArray[pEntryIndex];
            ReflowItemRecord record = entry.record;
            if (record == null)
                return;

            _suppressChildDirtyDepth++;
            try
            {
                try
                {
                    ElementDespawned?.Invoke(pEntryIndex, record.item);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                }
                try
                {
                    record.item.Hide();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                }
            }
            finally
            {
                _suppressChildDirtyDepth--;
            }

            ReflowAnimationDriver.Complete(record);
            record.hasPendingReflow = false;
            entry.record = null;
            entry.hasNatural = false;
            entry.hasWritten = false;

            int last = _spawnedRecordList.Count - 1;
            int index = record.spawnedListIndex;
            if (index >= 0 && index <= last)
            {
                ReflowItemRecord moved = _spawnedRecordList[last];
                _spawnedRecordList[index] = moved;
                moved.spawnedListIndex = index;
                _spawnedRecordList.RemoveAt(last);
            }
            record.spawnedListIndex = -1;
            record.entryIndex = -1;

            _pool.Release(record);
            // Deactivated at the end of the pass, so an item reused in the same pass is never toggled off and on.
            _deactivateList.Add(record);
            ReflowStats.DespawnCount++;
        }

        private void FlushDeactivations()
        {
            if (_deactivateList.Count == 0)
                return;
            _suppressChildDirtyDepth++;
            try
            {
                for (int i = 0; i < _deactivateList.Count; i++)
                {
                    ReflowItemRecord record = _deactivateList[i];
                    if (record.isInPool && record.gameObject != null && record.gameObject.activeSelf)
                        record.gameObject.SetActive(false);
                }
            }
            finally
            {
                _suppressChildDirtyDepth--;
                _deactivateList.Clear();
            }
        }

        #endregion

        private void ShiftSpawnedIndices(int pFrom, int pDelta)
        {
            for (int i = 0; i < _spawnedRecordList.Count; i++)
            {
                ReflowItemRecord record = _spawnedRecordList[i];
                if (record.entryIndex >= pFrom)
                    record.entryIndex += pDelta;
            }
        }

        private void RaiseElementsChanged(ReflowElementChange pChange)
        {
            // Data changes keep the scroll position; a pending measurement anchor no longer applies.
            _measureAnchor = ReflowScrollAnchor.None;
            try
            {
                OnElementsChangedInternal(pChange);
                ElementsChanged?.Invoke(pChange);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }

        private void EnsureEntryCapacity(int pCapacity)
        {
            if (_entryArray.Length >= pCapacity)
                return;
            ReflowEntry[] newArray = new ReflowEntry[Mathf.NextPowerOfTwo(pCapacity)];
            Array.Copy(_entryArray, newArray, _entryCount);
            _entryArray = newArray;
        }

        private void CheckIndex(int pIndex)
        {
            if (pIndex < 0 || pIndex >= _entryCount)
                throw new ArgumentOutOfRangeException(nameof(pIndex), $"[Reflow] Element index {pIndex} out of range (count {_entryCount}).");
        }

        private void ThrowIfLayingOut()
        {
            if (IsLayingOut)
                throw new InvalidOperationException("[Reflow] Elements cannot be added, removed or reordered from a layout callback (spawn / show). Do it after the layout.");
        }
    }
}
