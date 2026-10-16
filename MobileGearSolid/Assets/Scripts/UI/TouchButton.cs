using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NightShift
{
    /// Round action button that fires on press (not release) for responsive mobile controls.
    /// When highlighted (tutorial) it pulses amber to show what to press next.
    public class TouchButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public Image ring, icon, glow;
        public Text caption;
        public bool highlighted;
        Action onPress;
        bool active = true, pressed;

        public static TouchButton Create(Transform parent, string iconSprite, string keyHint, Vector2 pos, float size, Action onPress)
        {
            var ring = UIKit.Img(parent, "TouchButton", new Color(1, 1, 1, 0.75f), Art.S("ui_ring"));
            ring.rectTransform.At(new Vector2(1, 0), new Vector2(0.5f, 0.5f), pos, new Vector2(size, size));
            var b = ring.gameObject.AddComponent<TouchButton>();
            b.ring = ring;
            b.onPress = onPress;
            b.glow = UIKit.Img(ring.transform, "Glow", new Color(1f, 0.8f, 0.3f, 0f), Art.S("ui_disc"));
            b.glow.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * 1.3f, size * 1.3f));
            b.glow.raycastTarget = false;
            b.icon = UIKit.Img(ring.transform, "Icon", Color.white, Art.S(iconSprite));
            b.icon.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * 0.55f, size * 0.55f));
            b.icon.raycastTarget = false;
            var hint = UIKit.Label(ring.transform, keyHint, 22, UIKit.Dim, TextAnchor.MiddleCenter);
            hint.rectTransform.At(new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0, -4), new Vector2(200, 30));
            b.caption = UIKit.Label(ring.transform, "", 28, UIKit.TextCol, TextAnchor.MiddleCenter);
            b.caption.rectTransform.At(new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0, 6), new Vector2(380, 40));
            return b;
        }

        public void SetActive(bool isActive) => active = isActive;

        void Update()
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 7f);
            if (highlighted)
            {
                ring.color = Color.Lerp(new Color(1f, 0.85f, 0.35f, 1f), Color.white, pulse);
                icon.color = Color.white;
                glow.color = new Color(1f, 0.8f, 0.3f, 0.15f + 0.3f * pulse);
            }
            else
            {
                ring.color = new Color(1, 1, 1, active ? 0.85f : 0.3f);
                icon.color = new Color(1, 1, 1, active ? 1f : 0.35f);
                glow.color = new Color(1f, 0.8f, 0.3f, 0f);
            }
            float scale = pressed ? 0.9f : highlighted ? 1f + 0.08f * pulse : 1f;
            transform.localScale = Vector3.one * scale;
        }

        public void OnPointerDown(PointerEventData e)
        {
            pressed = true;
            onPress?.Invoke();
        }

        public void OnPointerUp(PointerEventData e) => pressed = false;
    }
}
