using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace MobileGearSolid
{
    /// Runs the [script] section of a level file, one command per line (see LEVELS.md):
    ///   say &lt;speaker&gt; &lt;textKey&gt;      visual-novel dialogue, pauses the game until tapped
    ///   objective &lt;textKey&gt;          replaces the HUD objective text
    ///   toast &lt;textKey&gt;              short message above the controls
    ///   highlight shoot|interact|trap|none   blinks an on-screen button
    ///   checkpoint &lt;marker&gt;          where the player respawns if caught (tutorial)
    ///   wait kill [n] | wait reach &lt;marker&gt; [radius] | wait hide | wait trap | wait vent | wait time &lt;sec&gt;
    ///   end                          finish the level successfully
    public class ScriptRunner : MonoBehaviour
    {
        List<string> lines;
        int index;
        string[] waiting;     // current blocking "wait" command, split into words
        float waitTimer;
        int trapsAtWait, ventsAtWait;
        bool inDialog;
        public Vector2 Checkpoint { get; private set; }

        /// Where the script wants the player to go right now (for the HUD pointer), or null.
        public Vector2? Target
        {
            get
            {
                if (waiting == null || waiting.Length < 2) return null;
                var g = Game.I;
                string kind = waiting[1].ToLowerInvariant();
                if (kind == "reach" && waiting.Length > 2 && g.Level.waypoints.TryGetValue(waiting[2][0], out var pt)) return pt;
                if (kind == "hide")
                {
                    // nearest free box
                    Vector2? best = null;
                    float bd = float.MaxValue;
                    foreach (var it in g.Interactables)
                        if (it is HideBox b && b.Available && Vector2.Distance(b.Pos, g.Player.Pos) < bd) { bd = Vector2.Distance(b.Pos, g.Player.Pos); best = b.Pos; }
                    return best;
                }
                if (kind == "vent")
                {
                    Vector2? best = null;
                    float bd = float.MaxValue;
                    foreach (var it in g.Interactables)
                        if (it is Vent v && Vector2.Distance(v.Pos, g.Player.Pos) < bd) { bd = Vector2.Distance(v.Pos, g.Player.Pos); best = v.Pos; }
                    return best;
                }
                if (kind == "kill")
                {
                    Vector2? best = null;
                    float bd = float.MaxValue;
                    foreach (var gd in g.Guards)
                        if (!gd.Dead && Vector2.Distance(gd.Pos, g.Player.Pos) < bd) { bd = Vector2.Distance(gd.Pos, g.Player.Pos); best = gd.Pos; }
                    return best;
                }
                return null;
            }
        }

        public void Init(List<string> script, Vector2 start)
        {
            lines = script;
            Checkpoint = start;
        }

        void Update()
        {
            var g = Game.I;
            if (lines == null || g.phase != Game.Phase.Playing || inDialog) return;

            if (waiting != null)
            {
                if (!WaitDone(waiting)) return;
                waiting = null;
                g.UI.Highlight(null);
            }

            while (index < lines.Count && waiting == null && !inDialog && g.phase == Game.Phase.Playing)
                Execute(lines[index++]);
        }

        void Execute(string line)
        {
            var w = line.Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
            if (w.Length == 0) return;
            var g = Game.I;
            switch (w[0].ToLowerInvariant())
            {
                case "say":
                    if (w.Length < 3) break;
                    inDialog = true;
                    g.ShowDialog(w[1], Loc.T(w[2]), () => inDialog = false);
                    break;
                case "objective":
                    g.Mission.scriptObjective = w.Length > 1 ? Loc.T(w[1]) : null;
                    break;
                case "toast":
                    if (w.Length > 1) g.Toast(Loc.T(w[1]));
                    break;
                case "highlight":
                    g.UI.Highlight(w.Length > 1 && w[1] != "none" ? w[1] : null);
                    break;
                case "checkpoint":
                    if (w.Length > 1 && g.Level.waypoints.TryGetValue(w[1][0], out var cp)) Checkpoint = cp;
                    break;
                case "wait":
                    waiting = w;
                    waitTimer = 0f;
                    trapsAtWait = g.Mission.trapsUsed;
                    ventsAtWait = g.Mission.ventsUsed;
                    break;
                case "end":
                    g.CompleteMission();
                    break;
                default:
                    Debug.LogWarning("[MGS] Unknown script command: " + line);
                    break;
            }
        }

        bool WaitDone(string[] w)
        {
            var g = Game.I;
            var p = g.Player;
            string kind = w.Length > 1 ? w[1].ToLowerInvariant() : "";
            switch (kind)
            {
                case "kill":
                    return g.Mission.kills >= (w.Length > 2 ? Int(w[2]) : 1);
                case "reach":
                {
                    if (w.Length < 3 || !g.Level.waypoints.TryGetValue(w[2][0], out var pt)) return true;
                    float r = w.Length > 3 ? Float(w[3]) : 1.5f;
                    return Vector2.Distance(p.Pos, pt) <= r;
                }
                case "hide":
                    return p.InBox;
                case "trap":
                    return g.Mission.trapsUsed > trapsAtWait;
                case "vent":
                    return g.Mission.ventsUsed > ventsAtWait;
                case "time":
                    waitTimer += Time.deltaTime;
                    return waitTimer >= (w.Length > 2 ? Float(w[2]) : 1f);
                default:
                    Debug.LogWarning("[MGS] Unknown wait: " + string.Join(" ", w));
                    return true;
            }
        }

        static int Int(string s) => int.TryParse(s, out var v) ? v : 1;
        static float Float(string s) => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 1f;
    }
}
