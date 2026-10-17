using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace MobileGearSolid
{
    /// Custom sounds and music: Assets/Resources/Audio/audio.txt maps a key to a file + volume:
    ///   sfx.alert   = Sfx/alert   | 1.0
    ///   music.menu  = Music/menu  | 0.7
    /// Paths are relative to Assets/Resources/Audio, without extension (.wav/.mp3/.ogg all work).
    /// A key missing from the file falls back to Sfx/&lt;id&gt; or Music/&lt;track&gt;.
    public static class AudioConfig
    {
        struct Entry { public string path; public float volume; }

        static Dictionary<string, Entry> entries;
        static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();

        static void Load()
        {
            entries = new Dictionary<string, Entry>();
            var asset = Resources.Load<TextAsset>("Audio/audio");
            if (asset == null) return;
            foreach (var raw in asset.text.Replace("\r", "").Split('\n'))
            {
                string line = raw;
                int hash = line.IndexOf('#');
                if (hash >= 0) line = line.Substring(0, hash); // comments, also at the end of a line
                line = line.Trim();
                if (line.Length == 0) continue;
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                string key = line.Substring(0, eq).Trim();
                var parts = line.Substring(eq + 1).Split('|');
                float vol = 1f;
                if (parts.Length > 1 && float.TryParse(parts[1].Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                    vol = v;
                entries[key] = new Entry { path = parts[0].Trim(), volume = Mathf.Clamp01(vol) };
            }
        }

        public static void Reload()
        {
            entries = null;
            clips.Clear();
        }

        /// Clip + volume for a key, or null if no file exists (then the built-in sound is used).
        public static AudioClip Get(string key, string defaultPath, float defaultVolume, out float volume)
        {
            if (entries == null) Load();
            string path = defaultPath;
            volume = defaultVolume;
            if (entries.TryGetValue(key, out var e))
            {
                path = e.path;
                volume = e.volume;
            }
            if (string.IsNullOrEmpty(path)) return null;
            if (!clips.TryGetValue(path, out var clip))
            {
                clip = Resources.Load<AudioClip>("Audio/" + path);
                clips[path] = clip; // cache misses too
            }
            return clip;
        }
    }
}
