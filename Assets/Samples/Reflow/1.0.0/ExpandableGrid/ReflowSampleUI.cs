using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Reflow.Samples
{
    /// <summary> Builds sample UI from code, so the samples need no scenes or prefabs. </summary>
    public static class ReflowSampleUI
    {
        public static readonly Color PANEL_COLOR = new Color(0.16f, 0.18f, 0.22f, 1f);
        public static readonly Color CARD_COLOR = new Color(0.24f, 0.27f, 0.33f, 1f);
        public static readonly Color ACCENT_COLOR = new Color(0.25f, 0.55f, 0.95f, 1f);

        public static RectTransform CreateCanvas(string pName = "Sample Canvas")
        {
            GameObject canvasObject = new GameObject(pName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;
            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            return (RectTransform)canvasObject.transform;
        }

        public static RectTransform Rect(string pName, Transform pParent, Vector2 pSize)
        {
            GameObject gameObject = new GameObject(pName, typeof(RectTransform));
            RectTransform rectTransform = (RectTransform)gameObject.transform;
            rectTransform.SetParent(pParent, false);
            rectTransform.sizeDelta = pSize;
            return rectTransform;
        }

        public static Image Background(Component pTarget, Color pColor)
        {
            Image image = pTarget.gameObject.AddComponent<Image>();
            image.color = pColor;
            return image;
        }

        /// <summary> A TMP text that sizes itself: both axes, or only the height when <paramref name="pWrapWidth"/> is set. </summary>
        public static TextMeshProUGUI Text(Transform pParent, string pText, float pFontSize = 32f, float pWrapWidth = 0f)
        {
            RectTransform rectTransform = Rect("Text", pParent, new Vector2(pWrapWidth > 0f ? pWrapWidth : 100f, 40f));
            TextMeshProUGUI text = rectTransform.gameObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = pFontSize;
            text.color = Color.white;
            text.text = pText;
            text.raycastTarget = false;
            ReflowFitter fitter = rectTransform.gameObject.AddComponent<ReflowFitter>();
            fitter.HorizontalFit = pWrapWidth > 0f ? ReflowFitter.FitMode.Unconstrained : ReflowFitter.FitMode.Preferred;
            fitter.VerticalFit = ReflowFitter.FitMode.Preferred;
            return text;
        }

        /// <summary> A button whose width follows its label, never narrower than <paramref name="pMinWidth"/>. </summary>
        public static Button Button(Transform pParent, string pLabel, Action pOnClick, float pMinWidth = 160f)
        {
            RectTransform rectTransform = Rect("Button", pParent, new Vector2(pMinWidth, 80f));
            Image image = Background(rectTransform, ACCENT_COLOR);
            Button button = rectTransform.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => pOnClick());

            ReflowHorizontal layout = rectTransform.gameObject.AddComponent<ReflowHorizontal>();
            layout.WidthPolicy = new ReflowSizePolicy { follow = true, min = pMinWidth };
            layout.FollowHeight = true;
            layout.Padding = new RectOffset(28, 28, 16, 16);
            layout.ChildAlignment = TextAnchor.MiddleCenter;
            Text(rectTransform, pLabel, 30f);
            return button;
        }

        /// <summary> A ScrollRect filling <paramref name="pParent"/> with a content that carries layout <typeparamref name="T"/>. </summary>
        public static T ScrollView<T>(Transform pParent, bool pVertical, out ReflowScrollRect pScrollRect) where T : ReflowLayout
        {
            RectTransform root = Rect("Scroll View", pParent, Vector2.zero);
            Stretch(root);
            Background(root, PANEL_COLOR);
            pScrollRect = root.gameObject.AddComponent<ReflowScrollRect>();

            RectTransform viewport = Rect("Viewport", root, Vector2.zero);
            Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();

            RectTransform content = Rect("Content", viewport, Vector2.zero);
            content.anchorMin = pVertical ? new Vector2(0f, 1f) : Vector2.zero;
            content.anchorMax = pVertical ? Vector2.one : new Vector2(0f, 1f);
            content.pivot = pVertical ? new Vector2(0.5f, 1f) : new Vector2(0f, 0.5f);
            content.sizeDelta = Vector2.zero;

            pScrollRect.viewport = viewport;
            pScrollRect.content = content;
            pScrollRect.horizontal = !pVertical;
            pScrollRect.vertical = pVertical;
            pScrollRect.scrollSensitivity = 40f;

            T layout = content.gameObject.AddComponent<T>();
            if (pVertical)
                layout.FollowHeight = true;
            else
                layout.FollowWidth = true;
            return layout;
        }

        public static void Stretch(RectTransform pRect, float pInset = 0f)
        {
            pRect.anchorMin = Vector2.zero;
            pRect.anchorMax = Vector2.one;
            pRect.offsetMin = new Vector2(pInset, pInset);
            pRect.offsetMax = new Vector2(-pInset, -pInset);
        }

        /// <summary> An inactive template to pass to AddElement as the item prefab. </summary>
        public static RectTransform Template(string pName, Vector2 pSize)
        {
            RectTransform template = Rect(pName, null, pSize);
            template.gameObject.SetActive(false);
            return template;
        }
    }
}
