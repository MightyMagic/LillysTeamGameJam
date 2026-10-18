using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

namespace MobileGearSolid
{
    /// Builds a level from the [map] section of a level file and owns grid queries,
    /// pathfinding and the power state. The legend below is shared with the Level Editor window.
    public class Level : MonoBehaviour
    {
        public const int ObstacleMask = 1 << 0; // Default layer: walls, props, closed doors
        public const int CharacterLayer = 2;    // Ignore Raycast: characters never block sight

        public int W, H;
        char[,] map;
        bool[,] blocked;
        public readonly Dictionary<char, Vector2> waypoints = new Dictionary<char, Vector2>();
        readonly Dictionary<Vector2Int, LockedDoor> doors = new Dictionary<Vector2Int, LockedDoor>();
        public Vector2 playerStart;
        public Vector2 exitSpawn;
        public PowerPanel panel;

        public bool PowerOn { get; private set; } = true;
        public float PowerTimeLeft { get; private set; }
        public const float PowerOffDuration = 35f;

        readonly List<Light2D> lamps = new List<Light2D>();
        readonly List<Light2D> emergency = new List<Light2D>();
        readonly List<SpriteRenderer> lampTubes = new List<SpriteRenderer>();
        readonly Dictionary<string, Tile> tiles = new Dictionary<string, Tile>();

        /// Map legend: character, editor colour, description (shown in Mobile Gear Solid > Level Editor).
        public static readonly (char ch, Color color, string label)[] Legend =
        {
            (' ', new Color(0, 0, 0), "пусто (за пределами)"),
            ('#', new Color(0.25f, 0.27f, 0.3f), "стена"),
            ('.', new Color(0.55f, 0.58f, 0.52f), "пол"),
            ('D', new Color(0.75f, 0.68f, 0.35f), "проём / дверь"),
            ('L', new Color(0.9f, 0.3f, 0.3f), "дверь по пропуску"),
            ('X', new Color(0.3f, 0.95f, 0.4f), "выход (в стене)"),
            ('V', new Color(0.35f, 0.35f, 0.4f), "вентиляция (в стене, декор)"),
            ('C', new Color(1f, 0.2f, 0.2f), "камера (в стене, смотрит в сторону пола)"),
            ('^', new Color(0.95f, 0.95f, 0.7f), "лампа на потолке"),
            ('P', new Color(0.3f, 0.6f, 1f), "старт игрока"),
            ('x', new Color(1f, 0.8f, 0.4f), "коробка, чтобы прятаться"),
            ('$', new Color(0.3f, 1f, 1f), "сундук с карточками (рейд)"),
            ('K', new Color(1f, 0.55f, 0.5f), "пропуск"),
            ('T', new Color(0.9f, 0.8f, 0.5f), "разведданные"),
            ('R', new Color(0.6f, 0.7f, 0.4f), "патроны"),
            ('M', new Color(1f, 1f, 1f), "аптечка"),
            ('W', new Color(0.7f, 0.5f, 0.9f), "шумовая ловушка (подбор)"),
            ('E', new Color(1f, 0.9f, 0.2f), "электрощит"),
            ('S', new Color(0.3f, 0.9f, 0.6f), "терминал с данными"),
            ('O', new Color(0.3f, 0.8f, 0.8f), "прототип"),
            ('c', new Color(0.55f, 0.4f, 0.25f), "ящик"),
            ('k', new Color(0.65f, 0.5f, 0.32f), "картонная коробка (декор)"),
            ('b', new Color(0.6f, 0.25f, 0.2f), "красная бочка"),
            ('u', new Color(0.3f, 0.4f, 0.5f), "синяя бочка"),
            ('h', new Color(0.45f, 0.5f, 0.55f), "стеллаж"),
            ('r', new Color(0.15f, 0.2f, 0.25f), "серверная стойка"),
            ('t', new Color(0.5f, 0.55f, 0.55f), "стол с компьютером"),
            ('w', new Color(0.35f, 0.3f, 0.25f), "оружейная стойка"),
            ('l', new Color(0.35f, 0.45f, 0.38f), "шкафчик"),
            ('g', new Color(0.45f, 0.47f, 0.45f), "решётка в полу (декор)"),
            ('y', new Color(0.8f, 0.7f, 0.2f), "жёлто-чёрная разметка (декор)"),
        };

        public const string WallChars = "#CXV";
        public const string PropChars = "ckbuhrtwl";
        /// Waypoints / markers used by [guards] routes and [script] commands (they are floor tiles).
        public const string WaypointChars = "0123456789ABFGHIJNQUYZ";

        public static bool IsWallChar(char c) => WallChars.IndexOf(c) >= 0;

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < W && y < H;
        public char At(int x, int y) => InBounds(x, y) ? map[x, y] : ' ';
        bool IsFloor(int x, int y) { char c = At(x, y); return c != ' ' && !IsWallChar(c); }

        public static Vector2Int Cell(Vector2 p) => new Vector2Int(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y));
        public static Vector2 Center(Vector2Int c) => new Vector2(c.x + 0.5f, c.y + 0.5f);
        public static Vector2 Center(int x, int y) => new Vector2(x + 0.5f, y + 0.5f);

        // ------------------------------------------------------------------ building

        public void Build(LevelDef mission)
        {
            var lines = mission.map;
            H = lines.Count;
            W = 0;
            foreach (var l in lines) W = Mathf.Max(W, l.Length);
            map = new char[W, H];
            blocked = new bool[W, H];
            for (int row = 0; row < H; row++)
                for (int x = 0; x < W; x++)
                    map[x, H - 1 - row] = x < lines[row].Length ? lines[row][x] : ' ';

            var grid = new GameObject("Grid", typeof(Grid));
            grid.transform.SetParent(transform, false);
            var floorTm = MakeTilemap(grid.transform, "Floor", Art.OrderFloor, false);
            var wallTm = MakeTilemap(grid.transform, "Walls", Art.OrderWalls, true);

            var exits = new List<Vector2>();
            int intelCount = 0;

            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    char c = map[x, y];
                    if (c == ' ') { blocked[x, y] = true; continue; }
                    var cell = new Vector3Int(x, y, 0);

                    if (IsWallChar(c))
                    {
                        blocked[x, y] = true;
                        bool face = IsFloor(x, y - 1);
                        string spr = c == 'X' ? "tile_exit" : c == 'V' ? "tile_vent"
                            : face ? "tile_wall_" + WallVariant(x, y) : "tile_wall_top";
                        wallTm.SetTile(cell, GetTile(spr, true, false));
                        if (c == 'C') SpawnCamera(x, y);
                        if (c == 'X') exits.Add(Center(x, y));
                        continue;
                    }

                    // floor layer
                    string floor = "tile_floor_" + FloorVariant(x, y);
                    bool rotate = false;
                    if (c == 'D' || c == 'L')
                    {
                        floor = "tile_door";
                        rotate = IsFloor(x - 1, y) && IsFloor(x + 1, y) && !(IsFloor(x, y - 1) && IsFloor(x, y + 1));
                    }
                    else if (c == 'g') floor = "tile_grate";
                    else if (c == 'y') floor = "tile_hazard";
                    floorTm.SetTile(cell, GetTile(floor, false, rotate));

                    Vector2 center = Center(x, y);
                    if (PropChars.IndexOf(c) >= 0)
                    {
                        SpawnProp(PropSprite(c), x, y);
                    }
                    else if (WaypointChars.IndexOf(c) >= 0)
                    {
                        waypoints[c] = center;
                    }
                    else switch (c)
                    {
                        case 'P': playerStart = center; break;
                        case '^': SpawnLamp(x, y); break;
                        case 'L':
                        {
                            var d = new GameObject("KeycardDoor").AddComponent<LockedDoor>();
                            d.transform.SetParent(transform, false);
                            d.Init(this, x, y);
                            doors[new Vector2Int(x, y)] = d;
                            break;
                        }
                        case 'E':
                            blocked[x, y] = true;
                            panel = new GameObject("PowerPanel").AddComponent<PowerPanel>();
                            panel.transform.SetParent(transform, false);
                            panel.Init(this, x, y);
                            break;
                        case 'S':
                        {
                            blocked[x, y] = true;
                            var t = new GameObject("DataTerminal").AddComponent<DataTerminal>();
                            t.transform.SetParent(transform, false);
                            t.Init(x, y, mission.Has(ObjectiveKind.DownloadData));
                            break;
                        }
                        case 'O':
                            blocked[x, y] = true;
                            SpawnPickup(PickupKind.Prototype, x, y);
                            break;
                        case 'K': SpawnPickup(PickupKind.Keycard, x, y); break;
                        case 'T': SpawnPickup(PickupKind.Intel, x, y); intelCount++; break;
                        case 'R': SpawnPickup(PickupKind.Ammo, x, y); break;
                        case 'M': SpawnPickup(PickupKind.Medkit, x, y); break;
                        case 'W': SpawnPickup(PickupKind.Decoy, x, y); break;
                        case '$':
                        {
                            blocked[x, y] = true;
                            var chest = new GameObject("Chest").AddComponent<Chest>();
                            chest.transform.SetParent(transform, false);
                            chest.Init(center);
                            break;
                        }
                        case 'x':
                        {
                            var box = new GameObject("HideBox").AddComponent<HideBox>();
                            box.transform.SetParent(transform, false);
                            box.Init(center);
                            break;
                        }
                    }
                }
            }

            if (exits.Count > 0)
            {
                Vector2 e = Vector2.zero;
                foreach (var p in exits) e += p;
                e /= exits.Count;
                var exit = new GameObject("ExitDoor").AddComponent<ExitDoor>();
                exit.transform.SetParent(transform, false);
                exit.Init(e);
                exitSpawn = e + Vector2.down;
            }

            Game.I.Mission.intelTotal = intelCount;
        }

        Tilemap MakeTilemap(Transform parent, string name, int order, bool collide)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var tm = go.AddComponent<Tilemap>();
            var tr = go.AddComponent<TilemapRenderer>();
            tr.sortingOrder = order;
            if (collide)
            {
                go.AddComponent<TilemapCollider2D>();
                go.layer = 0;
            }
            return tm;
        }

        Tile GetTile(string sprite, bool collider, bool rotate)
        {
            string key = sprite + (rotate ? "_r" : "") + (collider ? "_c" : "");
            if (tiles.TryGetValue(key, out var t)) return t;
            t = ScriptableObject.CreateInstance<Tile>();
            t.sprite = Art.S(sprite);
            t.colliderType = collider ? Tile.ColliderType.Grid : Tile.ColliderType.None;
            t.flags = TileFlags.LockColor | TileFlags.LockTransform;
            if (rotate) t.transform = Matrix4x4.Rotate(Quaternion.Euler(0, 0, 90));
            tiles[key] = t;
            return t;
        }

        static int Hash(int x, int y) => ((x * 73856093) ^ (y * 19349663)) & 0x7fffffff;

        static int FloorVariant(int x, int y)
        {
            int h = Hash(x, y) % 100;
            return h < 55 ? 0 : h < 72 ? 1 : h < 84 ? 2 : h < 92 ? 4 : 3;
        }

        static int WallVariant(int x, int y)
        {
            int h = Hash(x + 7, y * 3) % 100;
            return h < 35 ? 0 : h < 65 ? 1 : h < 82 ? 2 : h < 93 ? 3 : 4;
        }

        static string PropSprite(char c)
        {
            switch (c)
            {
                case 'c': return "prop_crate";
                case 'k': return "prop_box";
                case 'b': return "prop_barrel_red";
                case 'u': return "prop_barrel_blue";
                case 'h': return "prop_shelf";
                case 'r': return "prop_rack";
                case 't': return "prop_desk";
                case 'w': return "prop_weaponrack";
                default: return "prop_locker";
            }
        }

        void SpawnProp(string sprite, int x, int y)
        {
            blocked[x, y] = true;
            var sr = Art.MakeSprite(transform, sprite, sprite, new Vector3(x + 0.5f, y + 0.02f), Art.SortY(y));
            var col = sr.gameObject.AddComponent<BoxCollider2D>();
            col.size = new Vector2(0.92f, 0.84f);
            col.offset = new Vector2(0, 0.46f);
        }

        void SpawnPickup(PickupKind kind, int x, int y)
        {
            var p = new GameObject("Pickup_" + kind).AddComponent<Pickup>();
            p.transform.SetParent(transform, false);
            p.Init(kind, Center(x, y));
        }

        void SpawnCamera(int x, int y)
        {
            Vector2 dir = IsFloor(x, y - 1) ? Vector2.down : IsFloor(x + 1, y) ? Vector2.right
                        : IsFloor(x - 1, y) ? Vector2.left : Vector2.up;
            var cam = new GameObject("SecurityCamera").AddComponent<SecurityCamera>();
            cam.transform.SetParent(transform, false);
            cam.Init(Center(x, y), dir);
            Game.I.Cameras.Add(cam);
        }

        void SpawnLamp(int x, int y)
        {
            var lamp = MakeLight(transform, Center(x, y), new Color(0.85f, 0.95f, 0.78f), 0.8f, 6f);
            lamp.pointLightInnerRadius = 1.2f;
            lamp.falloffIntensity = 0.45f;
            lamps.Add(lamp);
            // dim red emergency light at every other lamp only: a power cut must really darken the level
            if (lamps.Count % 2 == 1)
            {
                var em = MakeLight(transform, Center(x, y), new Color(1f, 0.12f, 0.08f), 0.28f, 3.2f);
                em.enabled = false;
                emergency.Add(em);
            }

            // fluorescent tube on the nearest wall face above the lamp
            for (int yy = y + 1; yy < H; yy++)
            {
                if (!IsWallChar(At(x, yy))) continue;
                if (IsFloor(x, yy - 1))
                    lampTubes.Add(Art.MakeSprite(transform, "LampTube", "prop_lamp", new Vector3(x + 0.5f, yy + 0.62f), Art.OrderWallDecor));
                break;
            }
        }

        public static Light2D MakeLight(Transform parent, Vector2 pos, Color color, float intensity, float radius)
        {
            var go = new GameObject("Light");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var l = go.AddComponent<Light2D>();
            l.lightType = Light2D.LightType.Point;
            l.color = color;
            l.intensity = intensity;
            l.pointLightOuterRadius = radius;
            l.pointLightInnerRadius = 0.3f;
            l.falloffIntensity = 0.65f;
            return l;
        }

        // ------------------------------------------------------------------ power

        public void CutPower()
        {
            if (!PowerOn) return;
            PowerOn = false;
            PowerTimeLeft = PowerOffDuration;
            foreach (var l in lamps) l.enabled = false;
            foreach (var l in emergency) l.enabled = true;
            foreach (var t in lampTubes) t.color = new Color(0.3f, 0.3f, 0.32f);
            Sfx.Play("power");
            Game.I.OnPowerChanged(false);
        }

        public void RestorePower()
        {
            if (PowerOn) return;
            PowerOn = true;
            foreach (var l in lamps) l.enabled = true;
            foreach (var l in emergency) l.enabled = false;
            foreach (var t in lampTubes) t.color = Color.white;
            Sfx.Play("door");
            Game.I.OnPowerChanged(true);
        }

        void Update()
        {
            if (Game.I.phase != Game.Phase.Playing || PowerOn) return;
            PowerTimeLeft -= Time.deltaTime;
            if (PowerTimeLeft <= 0) RestorePower();
        }

        // ------------------------------------------------------------------ doors

        public void OpenDoorsNear(Vector2 pos)
        {
            foreach (var d in doors.Values)
                if (Vector2.Distance(d.Pos, pos) < 1.6f) d.SetOpen();
        }

        // ------------------------------------------------------------------ pathfinding

        public bool Walkable(int x, int y)
        {
            if (!InBounds(x, y) || blocked[x, y]) return false;
            if (map[x, y] == 'L' && doors.TryGetValue(new Vector2Int(x, y), out var d)) return d.IsOpen;
            return true;
        }

        Vector2Int NearestWalkable(Vector2Int c)
        {
            if (Walkable(c.x, c.y)) return c;
            for (int r = 1; r <= 4; r++)
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) == r && Walkable(c.x + dx, c.y + dy))
                            return new Vector2Int(c.x + dx, c.y + dy);
            return c;
        }

        /// A random walkable tile centre within `radius` tiles of pos (falls back to pos).
        public Vector2 RandomWalkableNear(Vector2 pos, int radius)
        {
            var c = Cell(pos);
            for (int i = 0; i < 20; i++)
            {
                int x = c.x + Random.Range(-radius, radius + 1), y = c.y + Random.Range(-radius, radius + 1);
                if (Walkable(x, y)) return Center(x, y);
            }
            return pos;
        }

        static readonly int[] DX = { 1, -1, 0, 0, 1, 1, -1, -1 };
        static readonly int[] DY = { 0, 0, 1, -1, 1, -1, 1, -1 };

        /// A* over the tile grid (8-way, no corner cutting). Returns world-space points; empty if unreachable.
        public List<Vector2> FindPath(Vector2 from, Vector2 to)
        {
            var result = new List<Vector2>();
            var s = NearestWalkable(Cell(from));
            var g = NearestWalkable(Cell(to));
            if (!Walkable(s.x, s.y) || !Walkable(g.x, g.y)) return result;
            if (s == g) { result.Add(to); return result; }

            int n = W * H;
            var cost = new float[n];
            var parent = new int[n];
            var closed = new bool[n];
            for (int i = 0; i < n; i++) { cost[i] = float.MaxValue; parent[i] = -1; }
            var open = new List<int>();
            var f = new float[n];
            int start = s.y * W + s.x, goal = g.y * W + g.x;
            cost[start] = 0;
            f[start] = Heur(s, g);
            open.Add(start);

            while (open.Count > 0)
            {
                int bi = 0;
                for (int i = 1; i < open.Count; i++) if (f[open[i]] < f[open[bi]]) bi = i;
                int cur = open[bi];
                open[bi] = open[open.Count - 1];
                open.RemoveAt(open.Count - 1);
                if (cur == goal) break;
                if (closed[cur]) continue;
                closed[cur] = true;
                int cx = cur % W, cy = cur / W;
                for (int k = 0; k < 8; k++)
                {
                    int nx = cx + DX[k], ny = cy + DY[k];
                    if (!Walkable(nx, ny)) continue;
                    if (k >= 4 && (!Walkable(cx + DX[k], cy) || !Walkable(cx, cy + DY[k]))) continue;
                    int ni = ny * W + nx;
                    if (closed[ni]) continue;
                    float nc = cost[cur] + (k >= 4 ? 1.4142f : 1f);
                    if (nc < cost[ni])
                    {
                        cost[ni] = nc;
                        parent[ni] = cur;
                        f[ni] = nc + Heur(new Vector2Int(nx, ny), g);
                        open.Add(ni);
                    }
                }
            }

            if (parent[goal] < 0) return result;
            for (int c = goal; c != start; c = parent[c]) result.Add(Center(c % W, c / W));
            result.Reverse();
            result[result.Count - 1] = Walkable(Cell(to).x, Cell(to).y) ? to : result[result.Count - 1];
            return result;
        }

        static float Heur(Vector2Int a, Vector2Int b)
        {
            int dx = Mathf.Abs(a.x - b.x), dy = Mathf.Abs(a.y - b.y);
            return Mathf.Max(dx, dy) + 0.4142f * Mathf.Min(dx, dy);
        }
    }
}
