using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MobileGearSolid.EditorTools
{
    public static class RoomCameraChecks
    {
        [MenuItem("Mobile Gear Solid/Check Room Camera Framing")]
        public static void Run()
        {
            int checks = 0, maps = 0, crossings = 0;
            void Check(bool condition, string label) { if (!condition) throw new System.Exception(label); checks++; }
            Loc.Reload();
            foreach (string guid in AssetDatabase.FindAssets("t:TextAsset", new[] { "Assets/Resources/Levels" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".txt") || path.EndsWith("/campaign.txt")) continue;
                string id = path.Substring("Assets/Resources/Levels/".Length);
                id = id.Substring(0, id.Length - 4);
                var d = LevelDef.Load(id);
                Check(d != null && d.map.Count > 0, id + " map loads");
                maps++;
                var cells = new HashSet<Vector2Int>();
                for (int row = 0; row < d.map.Count; row++)
                    for (int x = 0; x < d.map[row].Length; x++)
                    {
                        char c = d.map[row][x];
                        if (c == ' ' || Level.IsWallChar(c)) continue;
                        cells.Add(new Vector2Int(x, d.map.Count - row - 1));
                    }
                Check(cells.Count > 0, id + " has cells");
                var rc = RoomCamera.ForLevel(d);
                foreach (var cell in cells)
                {
                    Vector2 p = (Vector2)cell + Vector2.one * 0.5f;
                    if (d.cameraAreas.Count > 0)
                        Check(d.cameraAreas.Exists(a => a.bounds.Contains(p)), id + " cell covered " + cell);
                    rc.Select(p);
                    Check(rc.Active && rc.Frame(p).Contains(p), id + " player stays in frame " + cell);
                    foreach (var dir in new[] { Vector2Int.right, Vector2Int.up })
                    {
                        if (!cells.Contains(cell + dir)) continue;
                        Vector2 next = p + (Vector2)dir;
                        var edge = RoomCamera.ForLevel(d);
                        edge.Select(p);
                        string before = edge.AreaId;
                        edge.Select(next);
                        Check(edge.Active && edge.Frame(next).Contains(next), id + " adjacent cell framed " + cell);
                        if (before != edge.AreaId) crossings++;
                    }
                }
                foreach (var area in d.cameraAreas)
                {
                    Check(area.id != null && !Loc.T("room." + area.id).StartsWith("#"), id + " room label " + area.id);
                    foreach (var size in new[] { new Vector2Int(1920,1080), new Vector2Int(2400,1080), new Vector2Int(2560,1600), new Vector2Int(2048,1536) })
                    {
                        var f = area.Frame(area.bounds.center);
                        var v = RoomCamera.Viewport(f, size.x, size.y);
                        Check(v.xMin >= 0 && v.xMax <= 1 && v.yMin >= .189f && v.yMax <= .861f, id + " frame in safe strip " + size);
                        float visibleWidth = f.height * size.x * v.width / (size.y * v.height);
                        Check(Mathf.Abs(visibleWidth - f.width) < .01f, id + " aspect preserved " + size);
                    }
                }
            }
            Check(maps >= 16, "campaign and all twelve raid maps checked");
            Check(crossings > 0, "camera boundaries exercised");
            var mission = LevelDef.Load("mission_02");
            var rooms = RoomCamera.ForLevel(mission);
            Check(rooms.Select(new Vector2(3.5f,7.5f)) && rooms.AreaId == "warehouse", "warehouse spawn");
            var first = rooms.Frame(new Vector2(3.5f,7.5f));
            Check(first == rooms.Frame(new Vector2(9.5f,2.5f)), "fixed room stays fixed");
            Check(!rooms.Select(new Vector2(13.05f,7.5f)) && rooms.AreaId == "warehouse", "boundary hysteresis");
            Check(rooms.Select(new Vector2(13.3f,7.5f)) && rooms.AreaId == "passage", "corridor entry");
            Check(rooms.Select(new Vector2(18.5f,10.3f)) && rooms.AreaId == "hall", "central passage entry");
            Check(rooms.Frame(new Vector2(18.5f,11)).center.y < rooms.Frame(new Vector2(18.5f,21)).center.y, "vertical tracking");
            Check(rooms.Select(new Vector2(27,13.5f)) && rooms.AreaId == "east", "east passage stays framed");
            Check(rooms.Select(new Vector2(30,20)) && rooms.AreaId == "server", "server stays framed");
            Check(!rooms.Select(new Vector2(-1,-1)) && rooms.AreaId == "server", "missing data never resets viewport");
            var draft = new LevelDef();
            for (int y = 0; y < 24; y++) draft.map.Add(new string('.', 40));
            var fallback = RoomCamera.ForLevel(draft);
            fallback.Select(new Vector2(2,2));
            var lower = fallback.Frame(new Vector2(2,2));
            fallback.Select(new Vector2(37,21));
            var upper = fallback.Frame(new Vector2(37,21));
            Check(fallback.Active && fallback.AreaId == null && lower.width == 13 && lower.height == 10, "new map has framed fallback without room label");
            Check(upper.center.x > lower.center.x && upper.center.y > lower.center.y && upper.Contains(new Vector2(37,21)), "fallback tracks both axes");
            Check(CameraArea.Parse("test follow 0 0 40 24") != null, "follow authoring parses");
            Debug.Log($"[RoomCameraChecks] PASS: {checks} checks; {maps} maps; {crossings} adjacent camera crossings");
        }
    }
}
