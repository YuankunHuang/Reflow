using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Reflow.Samples
{
    /// <summary> Grid cell that can be selected by clicking. </summary>
    public sealed class ReflowSampleCellItem : ReflowItemBehaviour<int>, IReflowSelectable
    {
        public Image background;
        public TextMeshProUGUI label;
        public Button button;

        public event Action<IReflowSelectable> Clicked;

        public bool IsSelectable => true;

        private void Awake()
        {
            button.onClick.AddListener(() => Clicked?.Invoke(this));
        }

        protected override void OnShow(int pData)
        {
            label.text = (pData + 1).ToString();
        }

        public void SetSelected(bool pSelected)
        {
            background.color = pSelected ? ReflowSampleUI.ACCENT_COLOR : ReflowSampleUI.CARD_COLOR;
        }
    }
}
