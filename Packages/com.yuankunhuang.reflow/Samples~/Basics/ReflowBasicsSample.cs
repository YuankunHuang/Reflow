using TMPro;
using UnityEngine;

namespace Reflow.Samples
{
    /// <summary>
    /// Add to an empty GameObject and press Play. A card whose height follows its text, a row of buttons whose
    /// widths follow their labels, and a grid. Clicking changes texts: every layout up the chain updates in the
    /// same frame, with one layout pass.
    /// </summary>
    public sealed class ReflowBasicsSample : MonoBehaviour
    {
        private const string MORE_TEXT = " Layouts only run when something changed, once per frame, before the frame renders.";

        private TextMeshProUGUI _body;
        private TextMeshProUGUI _counter;
        private ReflowGrid _grid;
        private int _clickCount;

        private void Start()
        {
            RectTransform canvas = ReflowSampleUI.CreateCanvas();

            // Card: fixed width, height follows the content.
            RectTransform cardRect = ReflowSampleUI.Rect("Card", canvas, new Vector2(900f, 100f));
            cardRect.anchoredPosition = new Vector2(0f, 400f);
            ReflowSampleUI.Background(cardRect, ReflowSampleUI.CARD_COLOR);
            ReflowVertical card = cardRect.gameObject.AddComponent<ReflowVertical>();
            card.FollowHeight = true;
            card.Padding = new RectOffset(40, 40, 40, 40);
            card.Spacing = 24f;
            card.CrossAxisMode = ReflowChildSizeMode.Expand;

            ReflowSampleUI.Text(cardRect, "Reflow", 56f);
            _body = ReflowSampleUI.Text(cardRect, "Measure / arrange layout for uGUI.", 34f, 820f);

            // Row of buttons: widths follow the labels, at least 160.
            RectTransform rowRect = ReflowSampleUI.Rect("Buttons", cardRect, new Vector2(100f, 100f));
            ReflowHorizontal row = rowRect.gameObject.AddComponent<ReflowHorizontal>();
            row.FollowWidth = true;
            row.FollowHeight = true;
            row.Spacing = 16f;
            ReflowSampleUI.Button(rowRect, "Add text", AddText);
            ReflowSampleUI.Button(rowRect, "Reset", ResetText);
            ReflowSampleUI.Button(rowRect, "Add cell", AddCell, 120f);
            _counter = ReflowSampleUI.Text(rowRect, "0 clicks", 30f);

            // Grid under the card.
            RectTransform gridRect = ReflowSampleUI.Rect("Grid", canvas, new Vector2(900f, 100f));
            gridRect.anchorMin = gridRect.anchorMax = new Vector2(0.5f, 0.5f);
            gridRect.pivot = new Vector2(0.5f, 1f);
            gridRect.anchoredPosition = new Vector2(0f, -100f);
            _grid = gridRect.gameObject.AddComponent<ReflowGrid>();
            _grid.CellSize = new Vector2(160f, 160f);
            _grid.Spacing = new Vector2(20f, 20f);
            _grid.ChildAlignment = TextAnchor.UpperCenter;
            _grid.FollowHeight = true;
            for (int i = 0; i < 5; i++)
                AddCell();
        }

        private void AddText()
        {
            _body.text += MORE_TEXT;
            Count();
        }

        private void ResetText()
        {
            _body.text = "Measure / arrange layout for uGUI.";
            Count();
        }

        private void AddCell()
        {
            RectTransform cell = ReflowSampleUI.Rect("Cell", _grid.transform, new Vector2(10f, 10f));
            ReflowSampleUI.Background(cell, Color.HSVToRGB(_grid.transform.childCount * 0.08f % 1f, 0.5f, 0.9f));
            Count();
        }

        private void Count()
        {
            _clickCount++;
            _counter.text = _clickCount == 1 ? "1 click" : $"{_clickCount} clicks";
        }
    }
}
