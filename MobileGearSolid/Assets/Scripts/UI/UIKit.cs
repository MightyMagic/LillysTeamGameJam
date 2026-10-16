using System;
using UnityEngine;
using UnityEngine.UI;

namespace MobileGearSolid
{
    /// Minimal helpers for building uGUI from code.
    public static class UIKit
    {
        public static readonly Color Green = new Color(0.55f, 1f, 0.6f);
        public static readonly Color Amber = new Color(1f, 0.78f, 0.3f);
        public static readonly Color Red = new Color(1f, 0.32f, 0.26f);
        public static readonly Color TextCol = new Color(0.86f, 0.9f, 0.86f);
        public static readonly Color Dim = new Color(0.55f, 0.62f, 0.57f);
        public static readonly Color PanelCol = new Color(0.03f, 0.05f, 0.04f, 0.94f);
        public static readonly Color ButtonCol = new Color(0.1f, 0.18f, 0.12f, 0.95f);

        public static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static RectTransform Fill(this RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
            return rt;
        }

        public static RectTransform At(this RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static Image Img(Transform parent, string name, Color c, Sprite sprite = null)
        {
            var rt = Rect(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = c;
            img.sprite = sprite;
            return img;
        }

        public static Text Label(Transform parent, string text, int size, Color c, TextAnchor align = TextAnchor.MiddleLeft)
        {
            var rt = Rect(parent, "Text");
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Art.Font;
            t.text = text;
            t.fontSize = size;
            t.color = c;
            t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.supportRichText = true;
            var o = rt.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0, 0, 0, 0.8f);
            o.effectDistance = new Vector2(2, -2);
            return t;
        }

        public static Button Btn(Transform parent, string text, Action onClick, int fontSize = 34, bool enabled = true)
        {
            var img = Img(parent, "Button", enabled ? ButtonCol : new Color(0.08f, 0.09f, 0.09f, 0.9f));
            var o = img.gameObject.AddComponent<Outline>();
            o.effectColor = enabled ? new Color(0.45f, 0.9f, 0.5f, 0.8f) : new Color(0.3f, 0.3f, 0.3f, 0.6f);
            o.effectDistance = new Vector2(3, -3);
            var b = img.gameObject.AddComponent<Button>();
            b.interactable = enabled;
            var colors = b.colors;
            colors.highlightedColor = new Color(1.3f, 1.3f, 1.3f);
            colors.pressedColor = new Color(0.7f, 0.7f, 0.7f);
            b.colors = colors;
            var label = Label(img.transform, text, fontSize, enabled ? TextCol : Dim, TextAnchor.MiddleCenter);
            label.rectTransform.Fill(10f);
            b.onClick.AddListener(() =>
            {
                Sfx.Play("click");
                onClick?.Invoke();
            });
            return b;
        }

        public static LayoutElement Size(this Component c, float height, float width = -1f)
        {
            var le = c.gameObject.GetComponent<LayoutElement>();
            if (le == null) le = c.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            le.minHeight = height;
            if (width > 0) le.preferredWidth = width;
            return le;
        }

        public static RectTransform Column(Transform parent, string name, float spacing)
        {
            var rt = Rect(parent, name);
            var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.childControlHeight = true;
            v.childControlWidth = true;
            v.childForceExpandHeight = false;
            v.childForceExpandWidth = true;
            return rt;
        }

        public static RectTransform Row(Transform parent, string name, float spacing)
        {
            var rt = Rect(parent, name);
            var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.childControlHeight = true;
            h.childControlWidth = true;
            h.childForceExpandHeight = true;
            h.childForceExpandWidth = true;
            return rt;
        }

        public static void Clear(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(t.GetChild(i).gameObject);
        }

        public static string Time(float seconds)
        {
            int s = Mathf.FloorToInt(seconds);
            return $"{s / 60:00}:{s % 60:00}";
        }
    }
}
