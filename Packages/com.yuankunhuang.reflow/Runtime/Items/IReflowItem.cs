using UnityEngine;

namespace Reflow
{
    /// <summary>
    /// A pooled view for one managed element. The layout activates the GameObject before <see cref="Show"/> and
    /// deactivates it after <see cref="Hide"/>; the item only binds and unbinds data.
    /// </summary>
    public interface IReflowItem
    {
        RectTransform RectTransform { get; }

        /// <summary> Bind <paramref name="pData"/>. Called on spawn and on <see cref="ReflowLayout.UpdateElementData"/>. </summary>
        void Show(object pData);

        /// <summary> Unbind before the item goes back to the pool. </summary>
        void Hide();
    }

    /// <summary> Creates and destroys pooled items. Pass one to <see cref="ReflowLayout.InitOwner"/> to hook a UI framework. </summary>
    public interface IReflowItemFactory
    {
        IReflowItem CreateItem(GameObject pPrefab, RectTransform pParent);

        void DestroyItem(IReflowItem pItem);
    }

    /// <summary>
    /// Instantiates the prefab under the layout. The item is the prefab's <see cref="IReflowItem"/> component,
    /// or a data-less <see cref="ReflowPlainItem"/> when it has none.
    /// </summary>
    public sealed class ReflowDefaultItemFactory : IReflowItemFactory
    {
        public static readonly ReflowDefaultItemFactory INSTANCE = new ReflowDefaultItemFactory();

        public IReflowItem CreateItem(GameObject pPrefab, RectTransform pParent)
        {
            GameObject instance = Object.Instantiate(pPrefab, pParent, false);
            if (instance.TryGetComponent(out IReflowItem item))
                return item;
            return new ReflowPlainItem((RectTransform)instance.transform);
        }

        public void DestroyItem(IReflowItem pItem)
        {
            RectTransform rectTransform = pItem?.RectTransform;
            if (rectTransform == null)
                return;
            if (Application.isPlaying)
                Object.Destroy(rectTransform.gameObject);
            else
                Object.DestroyImmediate(rectTransform.gameObject);
        }
    }

    /// <summary> Item for prefabs without an <see cref="IReflowItem"/> component: shown and hidden, no data. </summary>
    public sealed class ReflowPlainItem : IReflowItem
    {
        public ReflowPlainItem(RectTransform pRectTransform)
        {
            RectTransform = pRectTransform;
        }

        public RectTransform RectTransform { get; }

        public void Show(object pData)
        {
        }

        public void Hide()
        {
        }
    }

    /// <summary> Convenience base for items with typed data. </summary>
    public abstract class ReflowItemBehaviour<TData> : MonoBehaviour, IReflowItem
    {
        private RectTransform _rectTransform;

        public RectTransform RectTransform
        {
            get
            {
                if (_rectTransform == null)
                    _rectTransform = (RectTransform)transform;
                return _rectTransform;
            }
        }

        public TData Data { get; private set; }

        void IReflowItem.Show(object pData)
        {
            Data = pData is TData data ? data : default;
            OnShow(Data);
        }

        void IReflowItem.Hide()
        {
            OnHide();
            Data = default;
        }

        protected abstract void OnShow(TData pData);

        protected virtual void OnHide()
        {
        }
    }
}
