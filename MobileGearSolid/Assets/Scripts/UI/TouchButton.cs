using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NightShift
{
    /// Round action button that fires on press (not release) for responsive mobile controls.
    public class TouchButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public Image ring, icon;
        public Text caption;
        Action onPress;

        public static TouchButton Create(Transform parent, string iconSprite, string keyHint, Vector2 pos, float size, Action onPress)
        {
            var ring = UIKit.Img(parent, "TouchButton", new Color(1, 1, 1, 0.75f), Art.S("ui_ring"));
            ring.rectTransform.At(new Vector2(1, 0), new Vector2(0.5f, 0.5f), pos, new Vector2(size, size));
            var b = ring.gameObject.AddComponent<TouchButton>();
            b.ring = ring;
            b.onPress = onPress;
            b.icon = UIKit.Img(ring.transform, "Icon", Color.white, Art.S(iconSprite));
            b.icon.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * 0.55f, size * 0.55f));
            b.icon.raycastTarget = false;
            var hint = UIKit.Label(ring.transform, keyHint, 22, UIKit.Dim, TextAnchor.MiddleCenter);
            hint.rectTransform.At(new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0, -4), new Vector2(200, 30));
            b.caption = UIKit.Label(ring.transform, "", 28, UIKit.TextCol, TextAnchor.MiddleCenter);
            b.caption.rectTransform.At(new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0, 6), new Vector2(360, 40));
            return b;
        }

        public void SetActive(bool active)
        {
            ring.color = new Color(1, 1, 1, active ? 0.85f : 0.3f);
            icon.color = new Color(1, 1, 1, active ? 1f : 0.35f);
        }

        public void OnPointerDown(PointerEventData e)
        {
            transform.localScale = Vector3.one * 0.9f;
            onPress?.Invoke();
        }

        public void OnPointerUp(PointerEventData e) => transform.localScale = Vector3.one;
    }
}
