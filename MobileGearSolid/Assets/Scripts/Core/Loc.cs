using System.Collections.Generic;
using UnityEngine;

namespace MobileGearSolid
{
    /// All player-facing text lives in Assets/Resources/Texts/texts.txt as "key = value" lines.
    /// Use \n inside a value for a line break and {0}, {1}... for inserted values.
    public static class Loc
    {
        static Dictionary<string, string> table;

        static void Load()
        {
            table = new Dictionary<string, string>();
            var asset = Resources.Load<TextAsset>("Texts/texts");
            if (asset == null)
            {
                Debug.LogError("[MGS] Resources/Texts/texts.txt not found");
                return;
            }
            foreach (var raw in asset.text.Replace("\r", "").Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                string key = line.Substring(0, eq).Trim();
                string value = line.Substring(eq + 1).Trim().Replace("\\n", "\n");
                table[key] = value;
            }
        }

        public static void Reload() => table = null;

        public static bool Has(string key)
        {
            if (table == null) Load();
            return table.ContainsKey(key);
        }

        /// Text for a key; a missing key is shown as #key so it is easy to spot.
        public static string T(string key)
        {
            if (table == null) Load();
            if (table.TryGetValue(key, out var v)) return v;
            Debug.LogWarning("[MGS] Missing text key: " + key);
            return "#" + key;
        }

        public static string F(string key, params object[] args)
        {
            try { return string.Format(T(key), args); }
            catch (System.FormatException) { return T(key); }
        }
    }
}
