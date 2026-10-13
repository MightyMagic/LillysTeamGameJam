using System.Collections.Generic;
using UnityEngine;

namespace NightShift
{
    /// Tiny procedural chiptune sound set, so the prototype needs no audio assets.
    public static class Sfx
    {
        static AudioSource source;
        static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        const int Rate = 22050;

        public static void Init(GameObject host)
        {
            source = host.AddComponent<AudioSource>();
            source.playOnAwake = false;
            if (clips.Count > 0) return;

            var rnd = new System.Random(7);
            float Noise() => (float)rnd.NextDouble() * 2f - 1f;

            Add("alert", 0.45f, t => Sq(t < 0.09f ? 880 : 1320, t) * (t < 0.09f ? 0.6f : 0.6f * Fade(t, 0.09f, 0.45f)));
            Add("suspect", 0.18f, t => Sq(520, t) * 0.35f * Fade(t, 0, 0.18f));
            Add("shot", 0.14f, t => Noise() * 0.5f * Fade(t, 0, 0.14f) * (0.6f + 0.4f * Mathf.Sin(t * 900)));
            Add("gun", 0.2f, t => (Noise() * 0.7f + Sq(110, t) * 0.3f) * Fade(t, 0, 0.2f));
            Add("pickup", 0.14f, t => Sq(Mathf.Lerp(1046, 1568, t / 0.14f), t) * 0.35f * Fade(t, 0, 0.14f));
            Add("hurt", 0.22f, t => (Noise() * 0.4f + Sq(180 - t * 300, t) * 0.4f) * Fade(t, 0, 0.22f));
            Add("power", 0.7f, t => Mathf.Sin(2 * Mathf.PI * Mathf.Lerp(420, 60, t / 0.7f) * t) * 0.5f * Fade(t, 0, 0.7f));
            Add("door", 0.18f, t => Sq(150, t) * 0.35f * Fade(t, 0, 0.18f));
            Add("beep", 0.1f, t => Sq(1200, t) * 0.3f * Fade(t, 0, 0.1f));
            Add("kill", 0.18f, t => (Noise() * 0.3f + Mathf.Sin(2 * Mathf.PI * 90 * t) * 0.5f) * Fade(t, 0, 0.18f));
            Add("empty", 0.06f, t => Sq(300, t) * 0.25f);
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

        public static void Play(string id, float volume = 1f)
        {
            if (source != null && clips.TryGetValue(id, out var c)) source.PlayOneShot(c, volume);
        }
    }
}
