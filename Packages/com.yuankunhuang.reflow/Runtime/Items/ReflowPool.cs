using System;
using System.Collections.Generic;
using UnityEngine;

namespace Reflow
{
    /// <summary> Free item records per pool key (the prefab unless a key is given). </summary>
    internal sealed class ReflowPool
    {
        private readonly Dictionary<object, Stack<ReflowItemRecord>> _freeDict = new Dictionary<object, Stack<ReflowItemRecord>>();
        private IReflowItemFactory _factory = ReflowDefaultItemFactory.INSTANCE;

        public int CreatedCount { get; private set; }
        public int FreeCount { get; private set; }

        public void SetFactory(IReflowItemFactory pFactory)
        {
            _factory = pFactory ?? ReflowDefaultItemFactory.INSTANCE;
        }

        /// <summary> A free record for <paramref name="pKey"/>, or a new one. Released records may still be active. </summary>
        public ReflowItemRecord Acquire(GameObject pPrefab, object pKey, ReflowLayout pOwner, out bool pIsNew)
        {
            if (_freeDict.TryGetValue(pKey, out Stack<ReflowItemRecord> stack) && stack.Count > 0)
            {
                ReflowItemRecord record = stack.Pop();
                record.isInPool = false;
                FreeCount--;
                pIsNew = false;
                return record;
            }

            if (pPrefab == null)
                throw new ArgumentNullException(nameof(pPrefab), "[Reflow] Element prefab is null.");
            IReflowItem item = _factory.CreateItem(pPrefab, pOwner.RectTransform);
            if (item == null || item.RectTransform == null)
                throw new InvalidOperationException($"[Reflow] Factory returned no item for prefab '{pPrefab.name}'.");

            CreatedCount++;
            ReflowStats.CreateCount++;
            pIsNew = true;
            return new ReflowItemRecord(item, pKey, pOwner);
        }

        public void Release(ReflowItemRecord pRecord)
        {
            if (!_freeDict.TryGetValue(pRecord.poolKey, out Stack<ReflowItemRecord> stack))
            {
                stack = new Stack<ReflowItemRecord>();
                _freeDict.Add(pRecord.poolKey, stack);
            }
            pRecord.isInPool = true;
            stack.Push(pRecord);
            FreeCount++;
        }

        /// <summary> Destroys every free item. Items in use are untouched. </summary>
        public void DestroyFree(Action<ReflowItemRecord> pOnDestroy)
        {
            foreach (KeyValuePair<object, Stack<ReflowItemRecord>> pair in _freeDict)
            {
                Stack<ReflowItemRecord> stack = pair.Value;
                while (stack.Count > 0)
                {
                    ReflowItemRecord record = stack.Pop();
                    pOnDestroy?.Invoke(record);
                    _factory.DestroyItem(record.item);
                    CreatedCount--;
                }
            }
            FreeCount = 0;
        }
    }
}
