using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Reflow.Tests
{
    /// <summary> Builds UI for tests under a fixed-size root and cleans it up. </summary>
    public static class ReflowTestUtil
    {
        public const float TOLERANCE = 0.01f;
        private static readonly Vector3[] CORNER_ARRAY = new Vector3[4];
        private static readonly List<GameObject> _createdList = new List<GameObject>();

        /// <summary> A canvas with a 1000x1000 root in the middle; everything else goes under the root. </summary>
        public static RectTransform CreateRoot()
        {
            GameObject canvasObject = new GameObject("ReflowTestCanvas", typeof(RectTransform), typeof(Canvas));
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            _createdList.Add(canvasObject);
            RectTransform root = CreateRect("Root", canvasObject.transform, new Vector2(1000f, 1000f));
            // Settle anything queued by earlier tests so counters start clean.
            ReflowScheduler.Flush();
            return root;
        }

        public static void DestroyAll()
        {
            for (int i = 0; i < _createdList.Count; i++)
            {
                if (_createdList[i] != null)
                    Object.DestroyImmediate(_createdList[i]);
            }
            _createdList.Clear();
            ReflowScheduler.Flush();
        }

        public static RectTransform CreateRect(string pName, Transform pParent, Vector2 pSize)
        {
            GameObject gameObject = new GameObject(pName, typeof(RectTransform));
            RectTransform rectTransform = (RectTransform)gameObject.transform;
            rectTransform.SetParent(pParent, false);
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.sizeDelta = pSize;
            return rectTransform;
        }

        public static T CreateLayout<T>(Transform pParent, Vector2 pSize, string pName = null) where T : ReflowLayout
        {
            RectTransform rectTransform = CreateRect(pName ?? typeof(T).Name, pParent, pSize);
            return rectTransform.gameObject.AddComponent<T>();
        }

        public static TextMeshProUGUI CreateText(Transform pParent, string pText, Vector2 pSize, float pFontSize = 24f)
        {
            RectTransform rectTransform = CreateRect("Text", pParent, pSize);
            TextMeshProUGUI text = rectTransform.gameObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = pFontSize;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.text = pText;
            return text;
        }

        public static ReflowFitter CreateFittedText(Transform pParent, string pText, ReflowFitter.FitMode pHorizontal, ReflowFitter.FitMode pVertical, float pFontSize = 24f)
        {
            TextMeshProUGUI text = CreateText(pParent, pText, new Vector2(50f, 20f), pFontSize);
            ReflowFitter fitter = text.gameObject.AddComponent<ReflowFitter>();
            fitter.HorizontalFit = pHorizontal;
            fitter.VerticalFit = pVertical;
            return fitter;
        }

        /// <summary> Inactive template used as an element prefab (Instantiate works on scene objects too). </summary>
        public static GameObject CreateItemTemplate(Vector2 pSize, string pName = "ItemTemplate")
        {
            GameObject template = new GameObject(pName, typeof(RectTransform));
            _createdList.Add(template);
            template.SetActive(false);
            ((RectTransform)template.transform).sizeDelta = pSize;
            template.AddComponent<ReflowTestItem>();
            return template;
        }

        /// <summary> A ScrollRect with a viewport of <paramref name="pViewSize"/> and a content that follows its layout. </summary>
        public static T CreateScrollList<T>(Transform pParent, Vector2 pViewSize, bool pVertical, out ReflowScrollRect pScrollRect) where T : ReflowLayout
        {
            RectTransform root = CreateRect("Scroll", pParent, pViewSize);
            pScrollRect = root.gameObject.AddComponent<ReflowScrollRect>();
            RectTransform viewport = CreateRect("Viewport", root, Vector2.zero);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.sizeDelta = Vector2.zero;
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

            pScrollRect.viewport = viewport;
            pScrollRect.content = content;
            pScrollRect.vertical = pVertical;
            pScrollRect.horizontal = !pVertical;
            pScrollRect.movementType = ScrollRect.MovementType.Clamped;
            pScrollRect.inertia = false;

            T layout = content.gameObject.AddComponent<T>();
            if (pVertical)
                layout.FollowHeight = true;
            else
                layout.FollowWidth = true;
            return layout;
        }

        /// <summary> Child rect in its parent's space measured from the parent's top-left corner (x right, y down). </summary>
        public static Rect GetRectInParent(RectTransform pChild)
        {
            RectTransform parent = (RectTransform)pChild.parent;
            pChild.GetWorldCorners(CORNER_ARRAY);
            Vector3 bottomLeft = parent.InverseTransformPoint(CORNER_ARRAY[0]);
            Vector3 topRight = parent.InverseTransformPoint(CORNER_ARRAY[2]);
            Rect parentRect = parent.rect;
            return Rect.MinMaxRect(bottomLeft.x - parentRect.xMin, parentRect.yMax - topRight.y, topRight.x - parentRect.xMin, parentRect.yMax - bottomLeft.y);
        }

        public static void AssertRect(RectTransform pChild, float pX, float pY, float pWidth, float pHeight, string pMessage = null)
        {
            Rect rect = GetRectInParent(pChild);
            string message = $"{pMessage ?? pChild.name}: expected ({pX}, {pY}, {pWidth}, {pHeight}) got ({rect.x:F2}, {rect.y:F2}, {rect.width:F2}, {rect.height:F2})";
            Assert.AreEqual(pX, rect.x, TOLERANCE, message);
            Assert.AreEqual(pY, rect.y, TOLERANCE, message);
            Assert.AreEqual(pWidth, rect.width, TOLERANCE, message);
            Assert.AreEqual(pHeight, rect.height, TOLERANCE, message);
        }

        public static void AssertSize(RectTransform pRect, float pWidth, float pHeight, string pMessage = null)
        {
            Vector2 size = pRect.rect.size;
            string message = $"{pMessage ?? pRect.name}: expected size ({pWidth}, {pHeight}) got ({size.x:F2}, {size.y:F2})";
            Assert.AreEqual(pWidth, size.x, TOLERANCE, message);
            Assert.AreEqual(pHeight, size.y, TOLERANCE, message);
        }
    }
}
