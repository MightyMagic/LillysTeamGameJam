using System.Collections.Generic;
using UnityEngine;

namespace MobileGearSolid
{
    /// Tiny procedural chiptune sound set, so the prototype needs no audio assets.
    public static class Sfx
    {
        static AudioSource source;
        static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        const int Rate = 44100;

        public static void Init(GameObject host)
        {
            source = host.AddComponent<AudioSource>();
            source.playOnAwake = false;
            // Clips are created at runtime, so Unity destroys them when Play Mode stops. With domain
            // reload disabled this static dictionary survives and would hold dead clips: rebuild every time.
            foreach (var c in clips.Values)
                if (c != null) { if (Application.isPlaying) Object.Destroy(c); else Object.DestroyImmediate(c); }
            clips.Clear();

            var rnd = new System.Random(7);
            float Noise() => (float)rnd.NextDouble() * 2f - 1f;

            Add("alert", 0.45f, t => Sq(t < 0.09f ? 880 : 1320, t) * (t < 0.09f ? 0.6f : 0.6f * Fade(t, 0.09f, 0.45f)));
            Add("suspect", 0.18f, t => Sq(520, t) * 0.35f * Fade(t, 0, 0.18f));
            // Suppressed pistol: a short band-limited "pfft" of gas, a dull body thump,
            // then the metallic clack of the slide cycling ~30 ms later.
            float lp1 = 0f, lp2 = 0f, air = 0f;
            Add("shot", 0.2f, t =>
            {
                float n = Noise();
                lp1 += (n - lp1) * 0.35f;          // tame the harsh top end
                lp2 += (lp1 - lp2) * 0.06f;        // remove lows -> band-passed hiss
                float hiss = (lp1 - lp2) * 1.6f * Mathf.Exp(-t * 60f);
                float attack = Mathf.Clamp01(t / 0.0015f);
                float thump = Mathf.Sin(2 * Mathf.PI * Mathf.Lerp(150f, 70f, t / 0.1f) * t) * 0.45f * Mathf.Exp(-t * 45f);
                float tc = t - 0.032f;
                float clack = tc < 0 ? 0f :
                    (Mathf.Sin(2 * Mathf.PI * 2650f * tc) + 0.6f * Mathf.Sin(2 * Mathf.PI * 4170f * tc) + 0.3f * Mathf.Sin(2 * Mathf.PI * 1830f * tc))
                    * 0.16f * Mathf.Exp(-tc * 130f);
                air += (n - air) * 0.04f;
                float tail = air * 0.5f * Mathf.Exp(-t * 20f);
                return ((hiss + thump) * attack + clack + tail) * 0.8f;
            });
            Add("trap", 0.35f, t => (Noise() * 0.35f * Mathf.Exp(-t * 25f) + Sq(t < 0.12f ? 1100 : 820, t) * 0.3f) * Fade(t, 0.1f, 0.35f));
            Add("gun", 0.2f, t => (Noise() * 0.7f + Sq(110, t) * 0.3f) * Fade(t, 0, 0.2f));
            Add("pickup", 0.14f, t => Sq(Mathf.Lerp(1046, 1568, t / 0.14f), t) * 0.35f * Fade(t, 0, 0.14f));
            Add("hurt", 0.22f, t => (Noise() * 0.4f + Sq(180 - t * 300, t) * 0.4f) * Fade(t, 0, 0.22f));
            Add("power", 0.7f, t => Mathf.Sin(2 * Mathf.PI * Mathf.Lerp(420, 60, t / 0.7f) * t) * 0.5f * Fade(t, 0, 0.7f));
            Add("door", 0.18f, t => Sq(150, t) * 0.35f * Fade(t, 0, 0.18f));
            Add("beep", 0.1f, t => Sq(1200, t) * 0.3f * Fade(t, 0, 0.1f));
            Add("kill", 0.18f, t => (Noise() * 0.3f + Mathf.Sin(2 * Mathf.PI * 90 * t) * 0.5f) * Fade(t, 0, 0.18f));
            Add("empty", 0.06f, t => Sq(300, t) * 0.25f);
            Add("blip", 0.025f, t => Sq(940, t) * 0.08f);
            // low-health heartbeat: "lub-dub"
            Add("heart", 0.42f, t =>
            {
                float a = Mathf.Sin(2 * Mathf.PI * 58f * t) * Mathf.Exp(-t * 22f);
                float t2 = t - 0.18f;
                float b = t2 < 0 ? 0f : Mathf.Sin(2 * Mathf.PI * 50f * t2) * 0.7f * Mathf.Exp(-t2 * 22f);
                return (a + b) * 0.9f;
            });
            Add("codec", 0.5f, t => Sq(Mathf.Repeat(t, 0.125f) < 0.0625f ? 1480 : 1975, t) * 0.18f * Fade(t, 0.3f, 0.5f));
            Add("click", 0.05f, t => Sq(700, t) * 0.25f * Fade(t, 0, 0.05f));
            Add("success", 0.6f, t => Sq(t < 0.15f ? 523 : t < 0.3f ? 659 : 784, t) * 0.3f * Fade(t, 0.3f, 0.6f));
            Add("fail", 0.7f, t => Sq(t < 0.2f ? 392 : t < 0.4f ? 330 : 262, t) * 0.3f * Fade(t, 0.4f, 0.7f));
        }

        static float Sq(float f, float t) => Mathf.Sin(2 * Mathf.PI * f * t) >= 0 ? 1f : -1f;
        static float Fade(float t, float start, float end) => t < start ? 1f : Mathf.Clamp01(1f - (t - start) / (end - start));

        static void Add(string id, float duration, System.Func<float, float> fn)
        {
            int n = Mathf.CeilToInt(duration * Rate);
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = fn(i / (float)Rate);
            var clip = AudioClip.Create(id, n, 1, Rate, false);
            clip.SetData(data, 0);
            clips[id] = clip;
        }

        /// Plays a custom file from Resources/Audio (see audio.txt) if there is one, otherwise the built-in sound.
        public static void Play(string id, float volume = 1f)
        {
            if (source == null) return;
            var custom = AudioConfig.Get("sfx." + id, "Sfx/" + id, 1f, out float customVolume);
            if (custom != null) source.PlayOneShot(custom, volume * customVolume);
            else if (clips.TryGetValue(id, out var c)) source.PlayOneShot(c, volume);
        }
    }
}
