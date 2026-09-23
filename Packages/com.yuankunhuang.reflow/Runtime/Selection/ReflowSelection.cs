using System;
using UnityEngine;

namespace Reflow
{
    /// <summary>
    /// Single selection over any Reflow layout's elements (tabs, pickers). Items implement
    /// <see cref="IReflowSelectable"/>; spawned items get their state on spawn, clicks select, and the selected
    /// index follows inserts, removals and reorders. Structural changes never raise <see cref="SelectionChanged"/>;
    /// removing the selected element clears the selection silently.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ReflowLayout))]
    [AddComponentMenu("Layout/Reflow/Selection")]
    public sealed class ReflowSelection : MonoBehaviour
    {
        [Tooltip("Selected index after the element list is cleared (no event). -1 = none.")]
        [SerializeField] private int _initialIndex = -1;
        [Tooltip("Clicking the selected item clears the selection.")]
        [SerializeField] private bool _allowToggleOff;

        private ReflowLayout _layout;
        private int _selectedIndex = -1;
        private bool _isInitialized;
        private Action<IReflowSelectable> _onItemClickedAction;
        private Action<int, IReflowItem> _onSpawnedAction;
        private Action<int, IReflowItem> _onDespawnedAction;
        private Action<ReflowElementChange> _onElementsChangedAction;

        /// <summary> (previous, current). Raised by clicks and by <see cref="Select"/> with notify. </summary>
        public event Action<int, int> SelectionChanged;

        /// <summary>
        /// Return false to swallow a click (e.g. to confirm first and call <see cref="Select"/> later).
        /// </summary>
        public Func<int, bool> ClickFilter { get; set; }

        /// <summary> Elements with the same key show as selected together (e.g. copies in a looping list). Null: the index. </summary>
        public Func<int, int> SelectionKey { get; set; }

        public int SelectedIndex
        {
            get
            {
                EnsureInitialized();
                return _selectedIndex;
            }
        }

        public bool AllowToggleOff
        {
            get => _allowToggleOff;
            set => _allowToggleOff = value;
        }

        /// <summary> Selected index after the list is cleared. Set while the list is empty, it also becomes the current selection. </summary>
        public int InitialIndex
        {
            get => _initialIndex;
            set
            {
                _initialIndex = value;
                EnsureInitialized();
                if (Layout == null || Layout.ElementCount == 0)
                    _selectedIndex = value;
            }
        }

        public ReflowLayout Layout
        {
            get
            {
                if (_layout == null)
                    TryGetComponent(out _layout);
                return _layout;
            }
        }

        /// <summary> Selects element <paramref name="pIndex"/> (-1 clears). Returns false when nothing changed. </summary>
        public bool Select(int pIndex, bool pNotify = true)
        {
            EnsureInitialized();
            if (pIndex < -1 || (Layout != null && pIndex >= Layout.ElementCount))
                throw new ArgumentOutOfRangeException(nameof(pIndex));
            return SetSelection(pIndex, pNotify);
        }

        public bool ClearSelection(bool pNotify = true)
        {
            return Select(-1, pNotify);
        }

        /// <summary> Re-applies the selected state to every spawned item. </summary>
        public void RefreshItems()
        {
            ReflowLayout layout = Layout;
            if (layout == null)
                return;
            for (int i = 0; i < layout.SpawnedRecordCount; i++)
            {
                if (layout.GetSpawnedItem(i, out int index) is IReflowSelectable item)
                    item.SetSelected(IsSelected(index));
            }
        }

        private bool IsSelected(int pIndex)
        {
            if (_selectedIndex < 0 || pIndex < 0)
                return false;
            if (SelectionKey == null)
                return pIndex == _selectedIndex;
            return SelectionKey(pIndex) == SelectionKey(_selectedIndex);
        }

        private bool SetSelection(int pIndex, bool pNotify)
        {
            int previous = _selectedIndex;
            if (previous == pIndex)
                return false;
            _selectedIndex = pIndex;
            RefreshItems();
            if (pNotify)
                SelectionChanged?.Invoke(previous, pIndex);
            return true;
        }

        private void OnItemClicked(IReflowSelectable pItem)
        {
            ReflowLayout layout = Layout;
            if (layout == null || !(pItem is IReflowItem layoutItem) || !layout.TryGetElementIndex(layoutItem, out int index))
                return;
            if (!pItem.IsSelectable)
                return;
            if (ClickFilter != null && !ClickFilter(index))
                return;
            if (index == _selectedIndex)
            {
                if (_allowToggleOff)
                    SetSelection(-1, true);
                return;
            }
            SetSelection(index, true);
        }

        private void OnElementSpawned(int pIndex, IReflowItem pItem)
        {
            if (!(pItem is IReflowSelectable item))
                return;
            item.Clicked -= _onItemClickedAction;
            item.Clicked += _onItemClickedAction;
            item.SetSelected(IsSelected(pIndex));
        }

        private void OnElementDespawned(int pIndex, IReflowItem pItem)
        {
            if (pItem is IReflowSelectable item)
                item.Clicked -= _onItemClickedAction;
        }

        private void OnElementsChanged(ReflowElementChange pChange)
        {
            if (pChange.kind == ReflowElementChangeKind.Cleared)
            {
                _selectedIndex = _initialIndex;
                return;
            }

            // An index past the end (the initial index waiting for the list to fill) is not an element yet.
            int countBefore = Layout.ElementCount;
            if (pChange.kind == ReflowElementChangeKind.Inserted)
                countBefore -= pChange.count;
            else if (pChange.kind == ReflowElementChangeKind.Removed)
                countBefore += pChange.count;
            if (_selectedIndex >= countBefore)
                return;
            _selectedIndex = pChange.MapIndex(_selectedIndex);
        }

        private void EnsureInitialized()
        {
            if (_isInitialized)
                return;
            _isInitialized = true;
            _selectedIndex = _initialIndex;
        }

        private void OnEnable()
        {
            EnsureInitialized();
            ReflowLayout layout = Layout;
            if (layout == null)
                return;
            _onItemClickedAction ??= OnItemClicked;
            _onSpawnedAction ??= OnElementSpawned;
            _onDespawnedAction ??= OnElementDespawned;
            _onElementsChangedAction ??= OnElementsChanged;
            layout.ElementSpawned += _onSpawnedAction;
            layout.ElementDespawned += _onDespawnedAction;
            layout.ElementsChanged += _onElementsChangedAction;
            for (int i = 0; i < layout.SpawnedRecordCount; i++)
            {
                IReflowItem item = layout.GetSpawnedItem(i, out int index);
                OnElementSpawned(index, item);
            }
        }

        private void OnDisable()
        {
            ReflowLayout layout = _layout;
            if (layout == null)
                return;
            layout.ElementSpawned -= _onSpawnedAction;
            layout.ElementDespawned -= _onDespawnedAction;
            layout.ElementsChanged -= _onElementsChangedAction;
            for (int i = 0; i < layout.SpawnedRecordCount; i++)
            {
                if (layout.GetSpawnedItem(i, out _) is IReflowSelectable item)
                    item.Clicked -= _onItemClickedAction;
            }
        }
    }
}
