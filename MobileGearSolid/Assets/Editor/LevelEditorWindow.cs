using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MobileGearSolid.EditorTools
{
    /// Paint-style editor for the text level files in Assets/Resources/Levels.
    /// LMB paints the selected tile, RMB picks the tile under the cursor.
    public class LevelEditorWindow : EditorWindow
    {
        const string LevelDir = "Assets/Resources/Levels";

        string[] files = new string[0];
        int fileIndex = -1;
        string path;
        string header = "", footer = "";
        List<char[]> grid = new List<char[]>();
        int W, H, newW, newH;
        char brush = '#';
        float cell = 18f;
        bool dirty, showRoutes = true;
        Vector2 gridScroll, sideScroll;
        List<string> problems = new List<string>();
        static readonly Color[] RouteColors =
        {
            new Color(1f, 0.4f, 0.3f), new Color(0.3f, 0.8f, 1f), new Color(1f, 0.9f, 0.3f), new Color(0.6f, 1f, 0.4f),
            new Color(1f, 0.5f, 1f), new Color(1f, 0.7f, 0.3f), new Color(0.5f, 0.6f, 1f), new Color(0.4f, 1f, 0.9f),
        };

        [MenuItem("Mobile Gear Solid/Level Editor")]
        static void Open() => GetWindow<LevelEditorWindow>("Mobile Gear Solid Levels").Show();

        // The char grid can't survive a domain reload (Play Mode / recompile), but plain strings can,
        // so stash it on disable and rebuild it on enable — unsaved edits are kept too.
        [SerializeField] string gridBackup;

        void OnDisable() => gridBackup = string.Join("\n", grid.Select(r => new string(r)));

        void OnEnable()
        {
            if (path != null && grid.Count != H)
            {
                grid = string.IsNullOrEmpty(gridBackup)
                    ? new List<char[]>()
                    : gridBackup.Split('\n').Select(r => r.ToCharArray()).ToList();
                if (grid.Count != H || grid.Any(r => r.Length != W))
                {
                    // backup missing or inconsistent: reload the file from disk
                    path = null;
                    grid.Clear();
                    W = H = 0;
                }
            }
            RefreshFiles();
        }

        void RefreshFiles()
        {
            files = Directory.Exists(LevelDir)
                ? Directory.GetFiles(LevelDir, "*.txt", SearchOption.AllDirectories)
                    .Select(f => f.Replace(Path.DirectorySeparatorChar, '/').Substring(LevelDir.Length + 1))
                    .Select(f => f.Substring(0, f.Length - 4))
                    .Where(f => f != "campaign").OrderBy(f => f).ToArray()
                : new string[0];
            if (path == null && files.Length > 0)
            {
                // keep the previously selected level if it still exists
                int keep = fileIndex >= 0 && fileIndex < files.Length ? fileIndex : 0;
                Load(keep);
            }
        }

        // ------------------------------------------------------------------ file io

        void Load(int index)
        {
            fileIndex = index;
            path = Path.Combine(LevelDir, files[index] + ".txt");
            var lines = File.ReadAllText(path).Replace("\r", "").Split('\n').ToList();
            int mapStart = lines.FindIndex(l => l.Trim().ToLowerInvariant() == "[map]");
            grid.Clear();
            if (mapStart < 0)
            {
                header = string.Join("\n", lines).TrimEnd();
                footer = "";
            }
            else
            {
                int mapEnd = lines.FindIndex(mapStart + 1, l => l.Trim().StartsWith("[") && l.Trim().EndsWith("]"));
                if (mapEnd < 0) mapEnd = lines.Count;
                header = string.Join("\n", lines.Take(mapStart)).TrimEnd();
                footer = string.Join("\n", lines.Skip(mapEnd)).TrimEnd();
                foreach (var l in lines.Skip(mapStart + 1).Take(mapEnd - mapStart - 1))
                    if (l.Length > 0) grid.Add(l.TrimEnd().ToCharArray());
            }
            W = grid.Count == 0 ? 0 : grid.Max(r => r.Length);
            for (int i = 0; i < grid.Count; i++)
                if (grid[i].Length < W) grid[i] = grid[i].Concat(Enumerable.Repeat(' ', W - grid[i].Length)).ToArray();
            H = grid.Count;
            newW = W; newH = H;
            dirty = false;
            Validate();
        }

        void Save()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append(header.TrimEnd()).Append("\n\n[map]\n");
            // keep row count stable: a fully empty row would be dropped by the loader, so pad it
            foreach (var r in grid)
            {
                string row = new string(r).TrimEnd();
                sb.Append(row.Length == 0 ? " " : row).Append('\n');
            }
            sb.Append('\n').Append(footer.TrimEnd()).Append('\n');
            File.WriteAllText(path, sb.ToString().Replace("\n\n\n", "\n\n"));
            AssetDatabase.ImportAsset(path);
            dirty = false;
            Validate();
            ShowNotification(new GUIContent("Сохранено"));
        }

        void Resize(int w, int h)
        {
            w = Mathf.Clamp(w, 4, 200);
            h = Mathf.Clamp(h, 4, 200);
            var ng = new List<char[]>();
            for (int y = 0; y < h; y++)
            {
                var row = new char[w];
                for (int x = 0; x < w; x++) row[x] = y < H && x < W ? grid[y][x] : ' ';
                ng.Add(row);
            }
            grid = ng;
            W = w; H = h;
            dirty = true;
            Validate();
        }

        void CreateNew(bool raidRoom)
        {
            string prefix = raidRoom ? "raid/room_new" : "mission_new";
            string name = prefix;
            int n = 1;
            while (File.Exists(Path.Combine(LevelDir, name + ".txt"))) name = prefix + (++n);
            var map = new System.Text.StringBuilder();
            for (int y = 0; y < 12; y++)
            {
                for (int x = 0; x < 20; x++)
                    map.Append(y == 0 && x == 10 ? 'X' : y == 0 || y == 11 || x == 0 || x == 19 ? '#' : y == 9 && x == 3 ? 'P' : y == 3 && x == 5 ? '0' : y == 3 && x == 15 ? '1' : '.');
                map.Append('\n');
            }
            string head = raidRoom
                ? "# Комната «Ночного рейда»: попадает в забеги автоматически. tier — ступень сложности 1..3,\n" +
                  "# ориентируйся на автооценку сложности слева. Поставь сундук '$', чтобы игрок мог взять карточку.\n\n" +
                  "[mission]\ntier = 1\nobjectives = extract\n"
                : "# Новый уровень. Добавь строки " + name + ".title / .brief / .threats в Texts/texts.txt\n" +
                  "# и имя файла в Levels/campaign.txt, чтобы он появился в меню.\n\n" +
                  "[mission]\nobjectives = extract\nsides = nokills\n";
            File.WriteAllText(Path.Combine(LevelDir, name + ".txt"), head + "\n[map]\n" + map + "\n[guards]\n0 1\n");
            AssetDatabase.Refresh();
            RefreshFiles();
            Load(System.Array.IndexOf(files, name));
        }

        void PlayLevel()
        {
            if (dirty) Save();
            SessionState.SetString("ns_test_level", files[fileIndex]);
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene("Assets/Scenes/Game.unity");
                EditorApplication.isPlaying = true;
            }
        }

        // ------------------------------------------------------------------ validation

        void Validate()
        {
            problems.Clear();
            if (H == 0) return;
            const string walls = "#CXV", blockers = "ckbuhrtwlESO";
            bool Walk(int x, int y) => x >= 0 && y >= 0 && x < W && y < H && grid[y][x] != ' ' && walls.IndexOf(grid[y][x]) < 0 && blockers.IndexOf(grid[y][x]) < 0;

            var starts = new List<Vector2Int>();
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    if (grid[y][x] == 'P') starts.Add(new Vector2Int(x, y));
            if (starts.Count == 0) { problems.Add("Нет старта игрока (P)"); return; }
            if (starts.Count > 1) problems.Add("Несколько стартов P — используется последний");

            var seen = new HashSet<Vector2Int> { starts[0] };
            var q = new Queue<Vector2Int>(seen);
            while (q.Count > 0)
            {
                var c = q.Dequeue();
                foreach (var d in new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down })
                {
                    var n = c + d;
                    if (!seen.Contains(n) && Walk(n.x, n.y)) { seen.Add(n); q.Enqueue(n); }
                }
            }

            bool hasExit = false;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    char c = grid[y][x];
                    var v = new Vector2Int(x, y);
                    if ("KTRMWx".IndexOf(c) >= 0 || Level.WaypointChars.IndexOf(c) >= 0)
                    {
                        if (!seen.Contains(v)) problems.Add($"'{c}' ({x},{y}) недостижим от старта");
                    }
                    if ("ESOX".IndexOf(c) >= 0)
                    {
                        bool ok = new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down }.Any(d => seen.Contains(v + d));
                        if (!ok) problems.Add($"'{c}' ({x},{y}) не подойти вплотную");
                        if (c == 'X')
                        {
                            hasExit = true;
                            if (!(y + 1 < H && Walk(x, y + 1))) problems.Add($"Выход X ({x},{y}) должен быть в стене с полом СНИЗУ");
                        }
                    }
                }
            if (!hasExit) problems.Add("Нет выхода (X)");

            var marks = new HashSet<char>();
            foreach (var r in grid) foreach (var c in r) marks.Add(c);
            foreach (var route in GuardRoutes())
                foreach (var c in route)
                    if (!marks.Contains(c)) problems.Add($"Маршрут охраны: точки '{c}' нет на карте");
        }

        List<string> GuardRoutes()
        {
            var routes = new List<string>();
            bool inGuards = false;
            foreach (var raw in footer.Split('\n'))
            {
                string l = raw.Trim();
                if (l.StartsWith("[")) { inGuards = l.ToLowerInvariant() == "[guards]"; continue; }
                int hash = l.IndexOf('#');
                if (hash >= 0) l = l.Substring(0, hash).Trim();
                if (!inGuards || l.Length == 0) continue;
                routes.Add(string.Concat(l.Split(' ', '\t').Where(t => t.Length > 0 && !t.Contains("="))));
            }
            return routes;
        }

        // ------------------------------------------------------------------ gui

        void OnGUI()
        {
            DrawToolbar();
            if (path == null) { EditorGUILayout.HelpBox("В " + LevelDir + " нет уровней.", MessageType.Info); return; }

            EditorGUILayout.BeginHorizontal();
            DrawSidebar();
            DrawGrid();
            EditorGUILayout.EndHorizontal();
        }

        void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            int sel = EditorGUILayout.Popup(fileIndex, files, EditorStyles.toolbarPopup, GUILayout.Width(180));
            if (sel != fileIndex && (!dirty || EditorUtility.DisplayDialog("Несохранённые изменения", "Переключиться без сохранения?", "Да", "Нет")))
                Load(sel);
            if (GUILayout.Button("Новый", EditorStyles.toolbarButton, GUILayout.Width(60))) CreateNew(false);
            if (GUILayout.Button("Новая комната рейда", EditorStyles.toolbarButton, GUILayout.Width(140))) CreateNew(true);
            GUI.enabled = path != null;
            if (GUILayout.Button(dirty ? "Сохранить *" : "Сохранить", EditorStyles.toolbarButton, GUILayout.Width(90))) Save();
            if (GUILayout.Button("Откатить", EditorStyles.toolbarButton, GUILayout.Width(70))) Load(fileIndex);
            if (GUILayout.Button("▶ Сохранить и играть", EditorStyles.toolbarButton, GUILayout.Width(150))) PlayLevel();
            GUI.enabled = true;
            GUILayout.FlexibleSpace();
            showRoutes = GUILayout.Toggle(showRoutes, "Маршруты", EditorStyles.toolbarButton);
            GUILayout.Label("Масштаб", GUILayout.Width(55));
            cell = GUILayout.HorizontalSlider(cell, 8f, 32f, GUILayout.Width(100));
            EditorGUILayout.EndHorizontal();
        }

        void DrawSidebar()
        {
            sideScroll = EditorGUILayout.BeginScrollView(sideScroll, GUILayout.Width(300));
            EditorGUILayout.LabelField("Кисть: '" + brush + "'   (ПКМ по карте — пипетка)", EditorStyles.boldLabel);

            foreach (var (ch, color, label) in Level.Legend)
            {
                var r = EditorGUILayout.GetControlRect(GUILayout.Height(18));
                if (brush == ch) EditorGUI.DrawRect(r, new Color(0.3f, 0.5f, 0.9f, 0.35f));
                EditorGUI.DrawRect(new Rect(r.x + 2, r.y + 2, 14, 14), color);
                GUI.Label(new Rect(r.x + 22, r.y, r.width - 22, r.height), $"{(ch == ' ' ? "␣" : ch.ToString())}   {label}");
                if (Event.current.type == EventType.MouseDown && r.Contains(Event.current.mousePosition)) { brush = ch; Event.current.Use(); }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Точки маршрутов / меток", EditorStyles.boldLabel);
            int perRow = 7, i = 0;
            EditorGUILayout.BeginHorizontal();
            foreach (char c in Level.WaypointChars)
            {
                if (GUILayout.Toggle(brush == c, c.ToString(), "Button", GUILayout.Width(36))) brush = c;
                if (++i % perRow == 0) { EditorGUILayout.EndHorizontal(); EditorGUILayout.BeginHorizontal(); }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"Размер: {W} × {H}", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            newW = EditorGUILayout.IntField(newW, GUILayout.Width(50));
            newH = EditorGUILayout.IntField(newH, GUILayout.Width(50));
            if (GUILayout.Button("Применить")) Resize(newW, newH);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Настройки миссии (до [map])", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            header = EditorGUILayout.TextArea(header, GUILayout.MinHeight(120));
            EditorGUILayout.LabelField("Охрана и сценарий (после [map])", EditorStyles.boldLabel);
            footer = EditorGUILayout.TextArea(footer, GUILayout.MinHeight(160));
            if (EditorGUI.EndChangeCheck()) { dirty = true; Validate(); }

            EditorGUILayout.Space();
            var est = LevelDef.Parse(files[fileIndex], header + "\n[map]\n" + string.Join("\n", grid.Select(r => new string(r))) + "\n" + footer);
            float score = est.EstimateDifficulty(out int tier);
            string tierNote = files[fileIndex].StartsWith("raid/") && est.tier != tier ? $"  ⚠ в файле tier = {est.tier}" : "";
            EditorGUILayout.HelpBox($"Оценка сложности: {score:0.0} → ступень {tier}{tierNote}\n(до 3.0 — ступень 1, до 4.5 — ступень 2, выше — ступень 3)", MessageType.None);
            if (problems.Count == 0) EditorGUILayout.HelpBox("Проверка: всё достижимо ✓", MessageType.Info);
            else EditorGUILayout.HelpBox(string.Join("\n", problems.Take(12)), MessageType.Warning);
            EditorGUILayout.HelpBox("Формат файлов и команды сценария описаны в LEVELS.md в корне проекта.", MessageType.None);
            EditorGUILayout.EndScrollView();
        }

        void DrawGrid()
        {
            var area = GUILayoutUtility.GetRect(10, 10, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            if (grid.Count != H) { H = grid.Count; W = H == 0 ? 0 : grid.Max(r => r.Length); }
            gridScroll = GUI.BeginScrollView(area, gridScroll, new Rect(0, 0, W * cell + 4, H * cell + 4));
            var colors = Level.Legend.ToDictionary(l => l.ch, l => l.color);
            var label = new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.Max(7, (int)(cell * 0.6f)) };
            label.normal.textColor = Color.black;

            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    char c = grid[y][x];
                    var r = new Rect(x * cell, y * cell, cell - 1, cell - 1);
                    Color col = colors.TryGetValue(c, out var cc) ? cc : Level.WaypointChars.IndexOf(c) >= 0 ? new Color(0.55f, 0.58f, 0.52f) : Color.magenta;
                    EditorGUI.DrawRect(r, col);
                    if (c != '#' && c != '.' && c != ' ' && cell >= 12)
                    {
                        label.normal.textColor = Level.WaypointChars.IndexOf(c) >= 0 ? new Color(0.1f, 0.2f, 0.9f) : col.grayscale > 0.45f ? Color.black : Color.white;
                        GUI.Label(r, c.ToString(), label);
                    }
                }

            if (showRoutes) DrawRoutes();
            HandleMouse();
            GUI.EndScrollView();
        }

        void DrawRoutes()
        {
            var pos = new Dictionary<char, Vector2>();
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    if (Level.WaypointChars.IndexOf(grid[y][x]) >= 0) pos[grid[y][x]] = new Vector2((x + 0.5f) * cell, (y + 0.5f) * cell);
            var routes = GuardRoutes();
            for (int i = 0; i < routes.Count; i++)
            {
                Handles.color = RouteColors[i % RouteColors.Length];
                var pts = routes[i].Where(pos.ContainsKey).Select(c => (Vector3)pos[c]).ToList();
                if (pts.Count > 1) pts.Add(pts[0]);
                for (int k = 0; k + 1 < pts.Count; k++) Handles.DrawAAPolyLine(3f, pts[k], pts[k + 1]);
                if (pts.Count > 0) Handles.DrawSolidDisc(pts[0], Vector3.forward, cell * 0.25f);
            }
        }

        void HandleMouse()
        {
            var e = Event.current;
            if (e.type != EventType.MouseDown && e.type != EventType.MouseDrag) return;
            int x = Mathf.FloorToInt(e.mousePosition.x / cell), y = Mathf.FloorToInt(e.mousePosition.y / cell);
            if (x < 0 || y < 0 || x >= W || y >= H) return;
            if (e.button == 1) brush = grid[y][x];
            else if (e.button == 0 && grid[y][x] != brush)
            {
                grid[y][x] = brush;
                dirty = true;
                Validate();
            }
            e.Use();
            Repaint();
        }
    }
}
