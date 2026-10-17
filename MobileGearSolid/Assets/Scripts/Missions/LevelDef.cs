using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace MobileGearSolid
{
    public enum ObjectiveKind { GetKeycard, CutPower, DownloadData, GetPrototype, Extract }
    public enum SideKind { NoKills, NoAlerts, AllIntel, IntelAtLeast, TimeLimit }
    public enum Rank { Compromised, SilentOperator, Ghost }

    public class SideTask
    {
        public SideKind kind;
        public float param;

        public SideTask(SideKind kind, float param = 0) { this.kind = kind; this.param = param; }

        public string Label
        {
            get
            {
                switch (kind)
                {
                    case SideKind.NoKills: return Loc.T("side.nokills");
                    case SideKind.NoAlerts: return Loc.T("side.noalerts");
                    case SideKind.AllIntel: return Loc.T("side.allintel");
                    case SideKind.IntelAtLeast: return Loc.F("side.intel", param);
                    case SideKind.TimeLimit: return Loc.F("side.time", $"{(int)param / 60}:{(int)param % 60:00}");
                }
                return kind.ToString();
            }
        }
    }

    public class GuardDef
    {
        public string waypoints;   // waypoint markers from the map, visited in order
        public float? look;        // fixed look direction (degrees) for stationary guards
        public float speed = 1f;   // walk speed multiplier
        public int hp;             // pistol hits to take down (0 = default Meta.GuardHp)
    }

    /// One mission = one text file in Assets/Resources/Levels/&lt;id&gt;.txt (see LEVELS.md).
    /// Texts (title, brief, threats) come from texts.txt as "&lt;id&gt;.title" etc.
    public class LevelDef
    {
        public string id;
        public List<string> map = new List<string>();
        public List<GuardDef> guards = new List<GuardDef>();
        public List<string> script = new List<string>();
        public ObjectiveKind[] objectives = { ObjectiveKind.Extract };
        public SideTask[] sides = new SideTask[0];
        public bool tutorial;
        /// Start loadout overrides; -1 = use the character's upgraded stats from the ship.
        public int hp = -1, ammo = -1, decoys = -1;
        /// Difficulty tier 1..3 for Night Raid rooms (Levels/raid/*.txt).
        public int tier = 1;

        public string Title => Loc.T(id + ".title");
        public string Brief => Loc.T(id + ".brief");
        public string Threats => Loc.T(id + ".threats");

        public bool Has(ObjectiveKind k) => System.Array.IndexOf(objectives, k) >= 0;

        public static string ObjectiveLabel(ObjectiveKind k)
        {
            switch (k)
            {
                case ObjectiveKind.GetKeycard: return Loc.T("obj.keycard");
                case ObjectiveKind.CutPower: return Loc.T("obj.power");
                case ObjectiveKind.DownloadData: return Loc.T("obj.data");
                case ObjectiveKind.GetPrototype: return Loc.T("obj.prototype");
                default: return Loc.T("obj.extract");
            }
        }

        // ------------------------------------------------------------------ campaign

        /// Mission order from Resources/Levels/campaign.txt (one level id per line).
        public static List<string> Campaign()
        {
            var list = new List<string>();
            var asset = Resources.Load<TextAsset>("Levels/campaign");
            if (asset == null) return list;
            foreach (var raw in asset.text.Replace("\r", "").Split('\n'))
            {
                string l = StripComment(raw).Trim();
                if (l.Length > 0) list.Add(l);
            }
            return list;
        }

        public static LevelDef Load(string id)
        {
            var asset = Resources.Load<TextAsset>("Levels/" + id);
            if (asset == null)
            {
                Debug.LogError("[MGS] Level not found: Resources/Levels/" + id + ".txt");
                return null;
            }
            return Parse(id, asset.text);
        }

        static string StripComment(string s)
        {
            int i = s.IndexOf('#');
            return i >= 0 ? s.Substring(0, i) : s;
        }

        public static LevelDef Parse(string id, string text)
        {
            var def = new LevelDef { id = id };
            string section = "";
            foreach (var raw in text.Replace("\r", "").Split('\n'))
            {
                string trimmed = raw.Trim();
                if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                {
                    section = trimmed.Substring(1, trimmed.Length - 2).ToLowerInvariant();
                    continue;
                }
                if (section == "map")
                {
                    // the map keeps every character (including '#', which is a wall here);
                    // a row of spaces is a valid (empty) row, a truly empty line is ignored
                    if (raw.Length > 0) def.map.Add(raw.TrimEnd());
                    continue;
                }
                string line = StripComment(raw).Trim();
                if (line.Length == 0) continue;
                switch (section)
                {
                    case "mission": ParseSetting(def, line); break;
                    case "guards": def.guards.Add(ParseGuard(line)); break;
                    case "script": def.script.Add(line); break;
                }
            }
            return def;
        }

        static void ParseSetting(LevelDef def, string line)
        {
            int eq = line.IndexOf('=');
            if (eq < 0) return;
            string key = line.Substring(0, eq).Trim().ToLowerInvariant();
            string val = line.Substring(eq + 1).Trim();
            switch (key)
            {
                case "objectives":
                {
                    var list = new List<ObjectiveKind>();
                    foreach (var p in val.Split(','))
                    {
                        switch (p.Trim().ToLowerInvariant())
                        {
                            case "keycard": list.Add(ObjectiveKind.GetKeycard); break;
                            case "power": list.Add(ObjectiveKind.CutPower); break;
                            case "data": list.Add(ObjectiveKind.DownloadData); break;
                            case "prototype": list.Add(ObjectiveKind.GetPrototype); break;
                            case "extract": list.Add(ObjectiveKind.Extract); break;
                            default: Debug.LogWarning("[MGS] Unknown objective: " + p); break;
                        }
                    }
                    if (!list.Contains(ObjectiveKind.Extract)) list.Add(ObjectiveKind.Extract);
                    def.objectives = list.ToArray();
                    break;
                }
                case "sides":
                {
                    var list = new List<SideTask>();
                    foreach (var p in val.Split(','))
                    {
                        var parts = p.Trim().ToLowerInvariant().Split(':');
                        float arg = parts.Length > 1 ? ParseF(parts[1]) : 0f;
                        switch (parts[0])
                        {
                            case "nokills": list.Add(new SideTask(SideKind.NoKills)); break;
                            case "noalerts": list.Add(new SideTask(SideKind.NoAlerts)); break;
                            case "allintel": list.Add(new SideTask(SideKind.AllIntel)); break;
                            case "intel": list.Add(new SideTask(SideKind.IntelAtLeast, arg)); break;
                            case "time": list.Add(new SideTask(SideKind.TimeLimit, arg)); break;
                            case "": break;
                            default: Debug.LogWarning("[MGS] Unknown side task: " + p); break;
                        }
                    }
                    def.sides = list.ToArray();
                    break;
                }
                case "tutorial": def.tutorial = val.ToLowerInvariant() == "true"; break;
                case "hp": def.hp = (int)ParseF(val); break;
                case "ammo": def.ammo = (int)ParseF(val); break;
                case "decoys": def.decoys = (int)ParseF(val); break;
                case "tier": def.tier = (int)ParseF(val); break;
            }
        }

        static GuardDef ParseGuard(string line)
        {
            var g = new GuardDef { waypoints = "" };
            foreach (var token in line.Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries))
            {
                int eq = token.IndexOf('=');
                if (eq < 0) { g.waypoints += token; continue; }
                string k = token.Substring(0, eq).ToLowerInvariant(), v = token.Substring(eq + 1).ToLowerInvariant();
                if (k == "look")
                    g.look = v == "up" ? 90f : v == "down" ? -90f : v == "left" ? 180f : v == "right" ? 0f : ParseF(v);
                else if (k == "speed") g.speed = ParseF(v);
                else if (k == "hp") g.hp = (int)ParseF(v);
            }
            return g;
        }

        /// Rough danger score of a level (used for Night Raid tiers and shown in the Level Editor):
        /// guards weighted by hp and patrolling, cameras, route length to the exit, minus hiding boxes.
        public float EstimateDifficulty(out int suggestedTier)
        {
            int H = map.Count, W = 0;
            foreach (var r in map) W = Mathf.Max(W, r.Length);
            char At(int x, int y) => y >= 0 && y < H && x >= 0 && x < map[y].Length ? map[y][x] : ' ';
            bool Walk(int x, int y) { char c = At(x, y); return c != ' ' && "#CXV".IndexOf(c) < 0 && "ckbuhrtwlESO$".IndexOf(c) < 0; }

            float score = 0f;
            foreach (var g in guards)
                score += (g.hp > 0 ? g.hp : Meta.GuardHp) / 2f + (g.waypoints.Length > 1 ? 0.3f : 0f);
            int cams = 0, boxes = 0;
            var start = new Vector2Int(-1, -1);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    char c = At(x, y);
                    if (c == 'C') cams++;
                    if (c == 'x') boxes++;
                    if (c == 'P') start = new Vector2Int(x, y);
                }
            score += 0.8f * cams - 0.3f * boxes;

            // shortest walk from P to the tile under any exit
            if (start.x >= 0)
            {
                var dist = new Dictionary<Vector2Int, int> { [start] = 0 };
                var q = new Queue<Vector2Int>();
                q.Enqueue(start);
                int best = 99;
                while (q.Count > 0)
                {
                    var p = q.Dequeue();
                    if (At(p.x, p.y - 1) == 'X') best = Mathf.Min(best, dist[p]);
                    foreach (var d in new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down })
                    {
                        var n = p + d;
                        if (!dist.ContainsKey(n) && Walk(n.x, n.y)) { dist[n] = dist[p] + 1; q.Enqueue(n); }
                    }
                }
                score += best * 0.03f;
            }
            suggestedTier = score < 3f ? 1 : score < 4.5f ? 2 : 3;
            return score;
        }

        static float ParseF(string s) =>
            float.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : 0f;
    }
}
