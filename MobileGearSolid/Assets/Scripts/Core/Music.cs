using UnityEngine;

namespace MobileGearSolid
{
    /// Looping music with a short crossfade. Tracks: "menu", "level", "base" (see Resources/Audio/audio.txt).
    /// If a track file is missing, that screen is simply silent.
    public class Music : MonoBehaviour
    {
        const float FadeTime = 1.2f;
        static Music instance;

        AudioSource a, b;          // a = current, b = fading out
        float targetVolume;
        string current;

        public static void Init(GameObject host)
        {
            if (instance != null) return;
            instance = host.AddComponent<Music>();
            instance.a = MakeSource(host);
            instance.b = MakeSource(host);
        }

        static AudioSource MakeSource(GameObject host)
        {
            var s = host.AddComponent<AudioSource>();
            s.loop = true;
            s.playOnAwake = false;
            s.volume = 0f;
            s.ignoreListenerPause = true;
            return s;
        }

        public static void Play(string track)
        {
            if (instance == null || instance.current == track) return;
            instance.current = track;
            var clip = AudioConfig.Get("music." + track, "Music/" + track, 0.7f, out float vol);

            // swap: the old track fades out on b, the new one fades in on a
            var old = instance.a;
            instance.a = instance.b;
            instance.b = old;
            instance.targetVolume = vol;
            instance.a.Stop();
            instance.a.clip = clip;
            instance.a.volume = 0f;
            if (clip != null) instance.a.Play();
        }

        void Update()
        {
            float step = Time.unscaledDeltaTime / FadeTime;
            a.volume = Mathf.MoveTowards(a.volume, a.clip != null ? targetVolume : 0f, step * Mathf.Max(targetVolume, 0.01f));
            b.volume = Mathf.MoveTowards(b.volume, 0f, step);
            if (b.isPlaying && b.volume <= 0f) b.Stop();
        }
    }
}
