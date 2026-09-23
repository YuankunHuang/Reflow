using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Reflow.Samples
{
    /// <summary> Tab button: width follows its title, selected state recolors it. </summary>
    public sealed class ReflowSampleTabItem : ReflowItemBehaviour<string>, IReflowSelectable
    {
        public Image background;
        public TextMeshProUGUI label;
        public Button button;

        public event Action<IReflowSelectable> Clicked;

        public bool IsSelectable => Data != "Locked";

        private void Awake()
        {
            button.onClick.AddListener(() => Clicked?.Invoke(this));
        }

        protected override void OnShow(string pData)
        {
            label.text = pData;
        }

        public void SetSelected(bool pSelected)
        {
            background.color = pSelected ? ReflowSampleUI.ACCENT_COLOR : ReflowSampleUI.CARD_COLOR;
        }
    }
}
