using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Reflow.Editor
{
    /// <summary> GameObject/UI/Reflow: ready-made scroll lists, layouts and fitted text. </summary>
    internal static class ReflowMenu
    {
        private const string ROOT = "GameObject/UI/Reflow/";
        private const int PRIORITY = 2100;

        [MenuItem(ROOT + "Vertical Scroll List", false, PRIORITY)]
        private static void CreateVerticalScrollList(MenuCommand pCommand)
        {
            CreateScrollView(pCommand, "Vertical Scroll List", true, pContent =>
            {
                ReflowVertical layout = pContent.gameObject.AddComponent<ReflowVertical>();
                layout.FollowHeight = true;
                layout.CrossAxisMode = ReflowChildSizeMode.Expand;
            });
        }

        [MenuItem(ROOT + "Horizontal Scroll List", false, PRIORITY + 1)]
        private static void CreateHorizontalScrollList(MenuCommand pCommand)
        {
            CreateScrollView(pCommand, "Horizontal Scroll List", false, pContent =>
            {
                ReflowHorizontal layout = pContent.gameObject.AddComponent<ReflowHorizontal>();
                layout.FollowWidth = true;
                layout.CrossAxisMode = ReflowChildSizeMode.Expand;
            });
        }

        [MenuItem(ROOT + "Scroll Grid", false, PRIORITY + 2)]
        private static void CreateScrollGrid(MenuCommand pCommand)
        {
            CreateScrollView(pCommand, "Scroll Grid", true, pContent =>
            {
                ReflowGrid layout = pContent.gameObject.AddComponent<ReflowGrid>();
                layout.FollowHeight = true;
            });
        }

        [MenuItem(ROOT + "Vertical Layout", false, PRIORITY + 20)]
        private static void CreateVerticalLayout(MenuCommand pCommand)
        {
            RectTransform rectTransform = CreateRect("Vertical Layout", GetParent(pCommand), new Vector2(200f, 100f));
            rectTransform.gameObject.AddComponent<ReflowVertical>().FollowHeight = true;
            Finish(rectTransform.gameObject);
        }

        [MenuItem(ROOT + "Horizontal Layout", false, PRIORITY + 21)]
        private static void CreateHorizontalLayout(MenuCommand pCommand)
        {
            RectTransform rectTransform = CreateRect("Horizontal Layout", GetParent(pCommand), new Vector2(100f, 60f));
            rectTransform.gameObject.AddComponent<ReflowHorizontal>().FollowWidth = true;
            Finish(rectTransform.gameObject);
        }

        [MenuItem(ROOT + "Fitted Text", false, PRIORITY + 22)]
        private static void CreateFittedText(MenuCommand pCommand)
        {
            RectTransform rectTransform = CreateRect("Fitted Text", GetParent(pCommand), new Vector2(160f, 40f));
            TextMeshProUGUI text = rectTransform.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = "Text";
            ReflowFitter fitter = rectTransform.gameObject.AddComponent<ReflowFitter>();
            fitter.HorizontalFit = ReflowFitter.FitMode.Preferred;
            fitter.VerticalFit = ReflowFitter.FitMode.Preferred;
            Finish(rectTransform.gameObject);
        }

        private static void CreateScrollView(MenuCommand pCommand, string pName, bool pVertical, System.Action<RectTransform> pAddLayout)
        {
            RectTransform root = CreateRect(pName, GetParent(pCommand), pVertical ? new Vector2(400f, 600f) : new Vector2(600f, 200f));
            root.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.25f);
            ReflowScrollRect scrollRect = root.gameObject.AddComponent<ReflowScrollRect>();

            RectTransform viewport = CreateRect("Viewport", root, Vector2.zero);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.sizeDelta = Vector2.zero;
            viewport.pivot = new Vector2(0f, 1f);
            viewport.gameObject.AddComponent<RectMask2D>();

            RectTransform content = CreateRect("Content", viewport, Vector2.zero);
            if (pVertical)
            {
                content.anchorMin = new Vector2(0f, 1f);
                content.anchorMax = new Vector2(1f, 1f);
                content.pivot = new Vector2(0.5f, 1f);
            }
            else
            {
                content.anchorMin = new Vector2(0f, 0f);
                content.anchorMax = new Vector2(0f, 1f);
                content.pivot = new Vector2(0f, 0.5f);
            }
            content.sizeDelta = Vector2.zero;
            content.anchoredPosition = Vector2.zero;
            pAddLayout(content);

            scrollRect.viewport = viewport;
            scrollRect.content = content;
            scrollRect.horizontal = !pVertical;
            scrollRect.vertical = pVertical;
            scrollRect.movementType = ScrollRect.MovementType.Elastic;
            scrollRect.scrollSensitivity = 20f;
            Finish(root.gameObject);
        }

        private static RectTransform CreateRect(string pName, Transform pParent, Vector2 pSize)
        {
            GameObject gameObject = new GameObject(pName, typeof(RectTransform));
            gameObject.layer = LayerMask.NameToLayer("UI");
            RectTransform rectTransform = (RectTransform)gameObject.transform;
            if (pParent != null)
                rectTransform.SetParent(pParent, false);
            rectTransform.sizeDelta = pSize;
            return rectTransform;
        }

        /// <summary> The selected / context object if it is under a canvas, else a (new) canvas. </summary>
        private static Transform GetParent(MenuCommand pCommand)
        {
            GameObject context = pCommand.context as GameObject;
            if (context != null && context.GetComponentInParent<Canvas>() != null)
                return context.transform;

            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvasObject.layer = LayerMask.NameToLayer("UI");
                canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                Undo.RegisterCreatedObjectUndo(canvasObject, "Create Canvas");
                canvas = canvasObject.GetComponent<Canvas>();
            }
            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                Undo.RegisterCreatedObjectUndo(eventSystem, "Create EventSystem");
            }
            return canvas.transform;
        }

        private static void Finish(GameObject pGameObject)
        {
            Undo.RegisterCreatedObjectUndo(pGameObject, "Create " + pGameObject.name);
            Selection.activeGameObject = pGameObject;
        }
    }
}
