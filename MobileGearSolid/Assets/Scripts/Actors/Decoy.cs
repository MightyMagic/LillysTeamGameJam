using UnityEngine;

namespace NightShift
{
    /// Thrown noise maker: lands, then beeps twice, pulling nearby guards to investigate.
    public class Decoy : MonoBehaviour
    {
        public const float NoiseRadius = 7.5f;
        Vector2 from, to;
        float t;
        int beeps;
        SpriteRenderer sr;
        UnityEngine.Rendering.Universal.Light2D blink;

        public static void Spawn(Transform parent, Vector2 from, Vector2 to)
        {
            var go = new GameObject("Decoy");
            go.transform.SetParent(parent, false);
            var d = go.AddComponent<Decoy>();
            d.from = from;
            d.to = to;
            d.sr = go.AddComponent<SpriteRenderer>();
            d.sr.sprite = Art.S("item_decoy");
            d.sr.sortingOrder = Art.OrderFloorItems + 10;
            d.blink = Level.MakeLight(go.transform, to, new Color(1f, 0.3f, 0.2f), 0f, 1.6f);
            go.transform.position = from;
        }

        void Update()
        {
            if (Game.I.phase != Game.Phase.Playing) return;
            t += Time.deltaTime;
            const float flight = 0.4f;
            if (t < flight)
            {
                float k = t / flight;
                transform.position = Vector2.Lerp(from, to, k) + Vector2.up * Mathf.Sin(k * Mathf.PI) * 0.8f;
                return;
            }
            transform.position = to;
            blink.intensity = Mathf.Repeat(t * 3f, 1f) > 0.5f ? 1.2f : 0f;

            if (beeps == 0 && t > 0.8f) Beep();
            else if (beeps == 1 && t > 3f) Beep();
            else if (t > 6f) Destroy(gameObject);
        }

        void Beep()
        {
            beeps++;
            Sfx.Play("beep");
            Game.I.EmitNoise(to, NoiseRadius);
        }
    }
}
