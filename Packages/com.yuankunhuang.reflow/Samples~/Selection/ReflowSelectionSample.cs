using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Reflow.Samples
{
    /// <summary>
    /// Add to an empty GameObject and press Play. A tab bar from data: widths follow titles, the "Locked" tab
    /// cannot be selected, "Settings" asks first (ClickFilter), and the page shows the selected tab.
    /// </summary>
    public sealed class ReflowSelectionSample : MonoBehaviour
    {
        private static readonly string[] TAB_ARRAY = { "Home", "Inventory", "Locked", "Friends", "Settings" };

        private ReflowSelection _selection;
        private TextMeshProUGUI _page;
        private int _pendingTab = -1;

        private void Start()
        {
            RectTransform canvas = ReflowSampleUI.CreateCanvas();
            RectTransform column = ReflowSampleUI.Rect("Column", canvas, new Vector2(1000f, 100f));
            ReflowVertical columnLayout = column.gameObject.AddComponent<ReflowVertical>();
            columnLayout.FollowHeight = true;
            columnLayout.Spacing = 24f;
            columnLayout.ChildAlignment = TextAnchor.UpperCenter;

            RectTransform bar = ReflowSampleUI.Rect("Tabs", column, new Vector2(100f, 100f));
            ReflowHorizontal barLayout = bar.gameObject.AddComponent<ReflowHorizontal>();
            barLayout.FollowWidth = true;
            barLayout.FollowHeight = true;
            barLayout.Spacing = 8f;
            _selection = bar.gameObject.AddComponent<ReflowSelection>();
            _selection.InitialIndex = 0;
            _selection.ClickFilter = ConfirmSettings;
            _selection.SelectionChanged += (pPrevious, pCurrent) => ShowPage(pCurrent);

            _page = ReflowSampleUI.Text(column, "", 40f);
            ReflowSampleUI.Button(column, "Confirm pending tab", ConfirmPending);

            GameObject template = CreateTabTemplate();
            foreach (string tab in TAB_ARRAY)
                barLayout.AddElement(template, tab);
            ShowPage(_selection.SelectedIndex);
        }

        private bool ConfirmSettings(int pIndex)
        {
            if (TAB_ARRAY[pIndex] != "Settings")
                return true;
            _pendingTab = pIndex;
            _page.text = "Settings needs a confirm: press the button below.";
            return false;
        }

        private void ConfirmPending()
        {
            if (_pendingTab >= 0)
                _selection.Select(_pendingTab);
            _pendingTab = -1;
        }

        private void ShowPage(int pIndex)
        {
            _page.text = pIndex >= 0 ? $"Page: {TAB_ARRAY[pIndex]}" : "No tab";
        }

        private static GameObject CreateTabTemplate()
        {
            RectTransform root = ReflowSampleUI.Template("Tab", new Vector2(120f, 80f));
            Image background = ReflowSampleUI.Background(root, ReflowSampleUI.CARD_COLOR);
            Button button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            ReflowHorizontal layout = root.gameObject.AddComponent<ReflowHorizontal>();
            layout.WidthPolicy = new ReflowSizePolicy { follow = true, min = 120f };
            layout.FollowHeight = true;
            layout.Padding = new RectOffset(28, 28, 18, 18);
            layout.ChildAlignment = TextAnchor.MiddleCenter;

            ReflowSampleTabItem item = root.gameObject.AddComponent<ReflowSampleTabItem>();
            item.background = background;
            item.label = ReflowSampleUI.Text(root, "", 32f);
            item.button = button;
            return root.gameObject;
        }
    }
}
