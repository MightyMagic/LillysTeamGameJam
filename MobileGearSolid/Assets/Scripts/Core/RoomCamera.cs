using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace MobileGearSolid
{
    /// Authored camera areas with a framed follow-camera fallback. World coordinates, origin at the map's bottom-left.
    /// Areas change framing only: the level and mission objects are never recreated.
    public class CameraArea
    {
        public string id, mode;
        public Rect bounds;
        public static CameraArea Parse(string line)
        {
            var w = line.Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
            if (w.Length != 6 || (w[1] != "fixed" && w[1] != "x" && w[1] != "y" && w[1] != "follow"))
                return Invalid(line);
            var n = new float[4];
            for (int i = 0; i < n.Length; i++)
                if (!float.TryParse(w[i + 2], NumberStyles.Float, CultureInfo.InvariantCulture, out n[i]) ||
                    float.IsNaN(n[i]) || float.IsInfinity(n[i])) return Invalid(line);
            if (n[2] <= 0 || n[3] <= 0) return Invalid(line);
            return new CameraArea { id = w[0], mode = w[1], bounds = new Rect(n[0], n[1], n[2], n[3]) };
        }

        static CameraArea Invalid(string line)
        {
            Debug.LogWarning("[MGS] Invalid camera area: " + line);
            return null;
        }

        public Rect Frame(Vector2 player)
        {
            // Keep the original proportions; don't zoom the narrow passage into giant tiles.
            float w = Mathf.Max(13f, bounds.width + 0.8f);
            float h = Mathf.Max(10f, bounds.height + 0.8f);
            Vector2 c = bounds.center;
            if (mode == "x") { w = 13f; h = Mathf.Max(6.5f, bounds.height + 0.8f); c.x = Track(player.x, bounds.xMin - 0.4f, bounds.xMax + 0.4f, w); }
            if (mode == "y") { h = 10f; c.y = Track(player.y, bounds.yMin - 0.4f, bounds.yMax + 0.4f, h); }
            if (mode == "follow")
            {
                w = 13f; h = 10f;
                c.x = Track(player.x, bounds.xMin - 0.4f, bounds.xMax + 0.4f, w);
                c.y = Track(player.y, bounds.yMin - 0.4f, bounds.yMax + 0.4f, h);
            }
            return new Rect(c - new Vector2(w, h) * 0.5f, new Vector2(w, h));
        }

        static float Track(float p, float min, float max, float span) =>
            max - min <= span ? (min + max) * 0.5f : Mathf.Clamp(p, min + span * 0.5f, max - span * 0.5f);
    }

    /// Camera-only controller. It has no access to enemies, progression or mission timers.
    public class RoomCamera
    {
        readonly List<CameraArea> areas;
        CameraArea current;
        public string AreaId => current != null ? current.id : null;
        public bool Active => current != null;
        public RoomCamera(List<CameraArea> areas) { this.areas = areas; }

        // New maps made in the level editor also use the framed view before areas are authored.
        public static RoomCamera ForLevel(LevelDef def)
        {
            if (def.cameraAreas.Count > 0) return new RoomCamera(def.cameraAreas);
            int width = 1;
            foreach (var row in def.map) width = Mathf.Max(width, row.Length);
            return new RoomCamera(new List<CameraArea> {
                new CameraArea { mode = "follow", bounds = new Rect(0, 0, width, Mathf.Max(1, def.map.Count)) }
            });
        }

        // A small inset prevents repeated switches while standing on a doorway boundary.
        public bool Select(Vector2 player)
        {
            CameraArea candidate = null;
            foreach (var area in areas)
                if (area.bounds.Contains(player)) { candidate = area; break; }
            // Missing data must not eject a framed mission into full-screen mode.
            // Normal gameplay is covered by authored areas (validated by the Editor checks).
            if (candidate == null || candidate == current) return false;
            if (candidate != null && current != null)
            {
                var inner = candidate.bounds;
                inner.xMin += 0.18f; inner.yMin += 0.18f;
                inner.xMax -= 0.18f; inner.yMax -= 0.18f;
                if (!inner.Contains(player)) return false;
            }
            current = candidate;
            return true;
        }

        public Rect Frame(Vector2 player) => current.Frame(player);

        // Letterbox only as needed, keeping the entire room between HUD and touch controls.
        public static Rect Viewport(Rect frame, int screenWidth, int screenHeight)
        {
            float screenAspect = screenWidth / (float)Mathf.Max(1, screenHeight);
            var available = new Rect(0.02f, 0.19f, 0.96f, 0.67f);
            float w = available.width, h = w * screenAspect * frame.height / frame.width;
            if (h > available.height) { h = available.height; w = h * frame.width / frame.height / screenAspect; }
            return new Rect(available.center - new Vector2(w, h) * 0.5f, new Vector2(w, h));
        }
    }
}
