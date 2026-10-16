using UnityEngine;

namespace MobileGearSolid
{
    /// Expanding ring that shows how far a noise carried, so players learn the hearing radius.
    public class NoiseRipple : MonoBehaviour
    {
        const float Duration = 0.6f;
        SpriteRenderer sr;
        float radius, t;

        public static void Spawn(Transform parent, Vector2 pos, float radius)
        {
            var sr = Art.MakeSprite(parent, "NoiseRipple", "ui_ring", pos, Art.OrderCones + 5);
            sr.sharedMaterial = Art.Unlit;
            var r = sr.gameObject.AddComponent<NoiseRipple>();
            r.sr = sr;
            r.radius = radius;
            sr.transform.localScale = Vector3.zero;
        }

        void Update()
        {
            t += Time.deltaTime;
            float k = t / Duration;
            if (k >= 1f) { Destroy(gameObject); return; }
            // ui_ring is 2 world units wide at scale 1
            float s = Mathf.Lerp(0.2f, radius, 1f - (1f - k) * (1f - k));
            transform.localScale = new Vector3(s, s, 1f);
            sr.color = new Color(1f, 1f, 1f, 0.45f * (1f - k));
        }
    }
}
