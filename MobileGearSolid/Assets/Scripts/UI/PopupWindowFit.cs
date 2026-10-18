using UnityEngine;
using UnityEngine.UI;

namespace MobileGearSolid
{
    /// Fixed popup buttons; only the window's height follows its actual content.
    public sealed class PopupWindowFit : MonoBehaviour
    {
        public const float ButtonHeight = 114;
        RectTransform content, window;
        public static void Attach(RectTransform content, RectTransform window)
        {
            var fit = content.gameObject.AddComponent<PopupWindowFit>();
            fit.content = content; fit.window = window;
        }
        void LateUpdate()
        {
            // First give wrapped text its real width; then grow only blocks whose copy needs more room.
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            float height = 80, spacing = 14;
            int count = 0;
            for (int i = 0; i < content.childCount; i++)
            {
                var child = content.GetChild(i) as RectTransform;
                if (child == null || !child.gameObject.activeSelf) continue;
                var button = child.GetComponent<Button>();
                var row = child.GetComponent<HorizontalLayoutGroup>();
                if (button != null || (row != null && child.name == "Buttons"))
                {
                    child.Size(ButtonHeight);
                    child.GetComponent<LayoutElement>().flexibleHeight = 0;
                    if (row != null)
                        for (int j = 0; j < child.childCount; j++)
                        {
                            var item = child.GetChild(j).GetComponent<Button>();
                            if (item == null) continue;
                            item.Size(ButtonHeight); item.GetComponent<LayoutElement>().flexibleHeight = 0;
                        }
                }
                var text = child.GetComponent<Text>();
                var size = child.GetComponent<LayoutElement>();
                if (text != null && size != null && text.preferredHeight > size.preferredHeight + .5f)
                    text.Size(Mathf.Ceil(text.preferredHeight));
                height += Mathf.Max(0, LayoutUtility.GetMinHeight(child), LayoutUtility.GetPreferredHeight(child));
                count++;
            }
            height += Mathf.Max(0, count - 1) * spacing;
            if (Mathf.Abs(window.sizeDelta.y - height) > .1f)
                window.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        }
    }
}
