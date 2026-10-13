using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NightShift
{
    /// Floating left-thumb stick: appears where the touch starts inside its zone.
    public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        RectTransform zone, baseRt, knob;
        Vector2 home;
        const float Radius = 110f;

        public static VirtualJoystick Create(Transform parent)
        {
            var zoneImg = UIKit.Img(parent, "JoystickZone", new Color(0, 0, 0, 0));
            zoneImg.rectTransform.anchorMin = new Vector2(0, 0);
            zoneImg.rectTransform.anchorMax = new Vector2(0.42f, 0.62f);
            zoneImg.rectTransform.offsetMin = zoneImg.rectTransform.offsetMax = Vector2.zero;
            var j = zoneImg.gameObject.AddComponent<VirtualJoystick>();
            j.zone = zoneImg.rectTransform;

            var b = UIKit.Img(j.zone, "Base", new Color(1, 1, 1, 0.35f), Art.S("ui_ring"));
            j.baseRt = b.rectTransform.At(Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(230, 230), new Vector2(300, 300));
            b.raycastTarget = false;
            var k = UIKit.Img(j.baseRt, "Knob", new Color(1, 1, 1, 0.55f), Art.S("ui_disc"));
            j.knob = k.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120, 120));
            k.raycastTarget = false;
            j.home = j.baseRt.anchoredPosition;
            return j;
        }

        Vector2 Local(PointerEventData e)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(zone, e.position, e.pressEventCamera, out var local);
            return local - zone.rect.min; // relative to bottom-left, matching the base anchor
        }

        public void OnPointerDown(PointerEventData e)
        {
            baseRt.anchoredPosition = Local(e);
            OnDrag(e);
        }

        public void OnDrag(PointerEventData e)
        {
            Vector2 d = Local(e) - baseRt.anchoredPosition;
            Vector2 v = Vector2.ClampMagnitude(d / Radius, 1f);
            knob.anchoredPosition = v * Radius;
            GameInput.Joystick = v;
        }

        public void OnPointerUp(PointerEventData e)
        {
            knob.anchoredPosition = Vector2.zero;
            baseRt.anchoredPosition = home;
            GameInput.Joystick = Vector2.zero;
        }

        void OnDisable()
        {
            if (knob != null) knob.anchoredPosition = Vector2.zero;
            if (baseRt != null) baseRt.anchoredPosition = home;
            GameInput.Joystick = Vector2.zero;
        }
    }
}
