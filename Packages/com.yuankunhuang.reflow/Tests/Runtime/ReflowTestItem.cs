using System;
using UnityEngine;

namespace Reflow.Tests
{
    /// <summary> Data for <see cref="ReflowTestItem"/>: an id and an optional size the item gives itself on Show. </summary>
    public sealed class ReflowTestData
    {
        public int id;
        public Vector2 size;

        public ReflowTestData(int pId, Vector2 pSize = default)
        {
            id = pId;
            size = pSize;
        }
    }

    /// <summary> Item used by the tests: counts calls, resizes itself when the data carries a size, can be selected. </summary>
    public sealed class ReflowTestItem : MonoBehaviour, IReflowItem, IReflowSelectable
    {
        public object data;
        public int showCount;
        public int hideCount;
        public bool isSelected;
        public bool selectable = true;

        public event Action<IReflowSelectable> Clicked;

        public RectTransform RectTransform => (RectTransform)transform;

        public bool IsSelectable => selectable;

        public int Id => data is ReflowTestData testData ? testData.id : -1;

        public void Show(object pData)
        {
            data = pData;
            showCount++;
            if (pData is ReflowTestData testData && testData.size != Vector2.zero)
                RectTransform.sizeDelta = testData.size;
        }

        public void Hide()
        {
            hideCount++;
        }

        public void SetSelected(bool pSelected)
        {
            isSelected = pSelected;
        }

        public void Click()
        {
            Clicked?.Invoke(this);
        }
    }
}
