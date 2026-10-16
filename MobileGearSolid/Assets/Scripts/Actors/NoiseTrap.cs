using UnityEngine;

namespace NightShift
{
    /// Noise trap placed at the player's feet: it ticks for a moment, then goes off twice,
    /// pulling nearby guards to investigate — place it and run.
    public class NoiseTrap : MonoBehaviour
    {
        public const float NoiseRadius = 9f;
        const float FirstBang = 2.5f, SecondBang = 5f, Lifetime = 7f;

        Vector2 pos;
        float t, tick;
        int bangs;
        UnityEngine.Rendering.Universal.Light2D blink;

        public static void Spawn(Transform parent, Vector2 pos)
        {
            var go = new GameObject("NoiseTrap");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var trap = go.AddComponent<NoiseTrap>();
            trap.pos = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Art.S("item_trap");
            sr.sortingOrder = Art.OrderFloorItems + 10;
            trap.blink = Level.MakeLight(go.transform, pos, new Color(1f, 0.3f, 0.2f), 0f, 1.6f);
            Sfx.Play("beep", 0.5f);
        }

        void Update()
        {
            if (Game.I.phase != Game.Phase.Playing) return;
            float dt = Time.deltaTime;
            t += dt;
            // ticking speeds up before each bang
            float rate = t < FirstBang ? Mathf.Lerp(2f, 8f, t / FirstBang) : 4f;
            tick += dt * rate;
            bool on = Mathf.Repeat(tick, 1f) > 0.5f;
            blink.intensity = on ? 1.2f : 0f;

            if (bangs == 0 && t > FirstBang) Bang();
            else if (bangs == 1 && t > SecondBang) Bang();
            else if (t > Lifetime) Destroy(gameObject);
        }

        void Bang()
        {
            bangs++;
            Sfx.Play("trap");
            Game.I.EmitNoise(pos, NoiseRadius);
        }
    }
}
