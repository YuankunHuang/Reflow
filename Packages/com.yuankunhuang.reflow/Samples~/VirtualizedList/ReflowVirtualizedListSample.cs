using System.Text;
using TMPro;
using UnityEngine;

namespace Reflow.Samples
{
    /// <summary>
    /// Add to an empty GameObject and press Play. 10,000 rows of different heights; only the visible ones exist.
    /// Rows fade in the first time they appear. The counter shows how few items are alive.
    /// </summary>
    public sealed class ReflowVirtualizedListSample : MonoBehaviour
    {
        private const int ROW_COUNT = 10000;
        private static readonly Vector2 ROW_SIZE_HINT = new Vector2(0f, 120f);
        private static readonly string[] WORD_ARRAY = { "layout", "measure", "arrange", "pool", "frame", "viewport", "scroll", "text", "virtual", "item" };

        private ReflowVertical _list;
        private ReflowScrollRect _scrollRect;
        private TextMeshProUGUI _stats;
        private GameObject _template;
        private int _nextId;
        private readonly StringBuilder _builder = new StringBuilder();

        private void Start()
        {
            RectTransform canvas = ReflowSampleUI.CreateCanvas();

            RectTransform page = ReflowSampleUI.Rect("Page", canvas, Vector2.zero);
            ReflowSampleUI.Stretch(page, 40f);
            ReflowVertical pageLayout = page.gameObject.AddComponent<ReflowVertical>();
            pageLayout.Spacing = 20f;
            pageLayout.CrossAxisMode = ReflowChildSizeMode.Expand;
            pageLayout.MainAxisMode = ReflowChildSizeMode.Expand;

            // Toolbar keeps its height; the list takes the rest of the page.
            RectTransform toolbar = ReflowSampleUI.Rect("Toolbar", page, new Vector2(100f, 100f));
            toolbar.gameObject.AddComponent<ReflowElement>().FlexibleHeight = 0f;
            ReflowHorizontal toolbarLayout = toolbar.gameObject.AddComponent<ReflowHorizontal>();
            toolbarLayout.FollowHeight = true;
            toolbarLayout.Spacing = 16f;
            ReflowSampleUI.Button(toolbar, "Jump to 5000", () => _scrollRect.ScrollToElement(_list, 5000, ReflowScrollAlignment.Center, 0.4f));
            ReflowSampleUI.Button(toolbar, "Insert on top", InsertOnTop);
            ReflowSampleUI.Button(toolbar, "Remove first", () => _list.RemoveElement(0));
            _stats = ReflowSampleUI.Text(toolbar, "", 28f);

            RectTransform listArea = ReflowSampleUI.Rect("List Area", page, new Vector2(100f, 100f));
            _list = ReflowSampleUI.ScrollView<ReflowVertical>(listArea, true, out _scrollRect);
            _list.Padding = new RectOffset(16, 16, 16, 16);
            _list.Spacing = 12f;
            _list.CrossAxisMode = ReflowChildSizeMode.Expand;
            _list.ViewportMargin = 100f;
            _list.gameObject.AddComponent<ReflowSpawnAnimator>().SpawnEffect = ReflowSpawnAnimator.Effect.FadeSlide;

            _template = CreateRowTemplate();
            Random.InitState(7);
            for (int i = 0; i < ROW_COUNT; i++)
                _list.AddElement(_template, NewRow(), ROW_SIZE_HINT);
        }

        private void Update()
        {
            _stats.text = $"{_list.ElementCount} rows, {_list.SpawnedCount} spawned, {_list.PooledItemCount} items";
        }

        private void InsertOnTop()
        {
            // Keep what the user is looking at in place while a row is added above it.
            ReflowScrollAnchor anchor = _list.CaptureScrollAnchor();
            _list.InsertElement(0, _template, NewRow(), ROW_SIZE_HINT);
            if (anchor.IsValid)
                _list.RestoreScrollAnchor(new ReflowScrollAnchor { index = anchor.index + 1, offset = anchor.offset });
        }

        private ReflowSampleRow NewRow()
        {
            _builder.Clear();
            int words = Random.Range(3, 40);
            for (int i = 0; i < words; i++)
                _builder.Append(WORD_ARRAY[Random.Range(0, WORD_ARRAY.Length)]).Append(' ');
            return new ReflowSampleRow { id = _nextId++, text = _builder.ToString() };
        }

        private static GameObject CreateRowTemplate()
        {
            RectTransform root = ReflowSampleUI.Template("Row", new Vector2(600f, 120f));
            ReflowSampleUI.Background(root, ReflowSampleUI.CARD_COLOR);
            ReflowVertical layout = root.gameObject.AddComponent<ReflowVertical>();
            layout.FollowHeight = true;
            layout.Padding = new RectOffset(24, 24, 20, 20);
            layout.CrossAxisMode = ReflowChildSizeMode.Expand;
            ReflowSampleRowItem item = root.gameObject.AddComponent<ReflowSampleRowItem>();
            item.label = ReflowSampleUI.Text(root, "", 30f, 500f);
            return root.gameObject;
        }
    }
}
