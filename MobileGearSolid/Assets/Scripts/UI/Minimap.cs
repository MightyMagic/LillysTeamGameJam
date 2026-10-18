using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MobileGearSolid
{
    /// Level schematic under the objective panel: walls and rooms, the current camera room,
    /// the player, the main objective and loot / side-mission icons. Tap to open it large
    /// (the game keeps running); tap anywhere to close.
    public class Minimap : MonoBehaviour, IPointerClickHandler
    {
        Game game;
        RectTransform self, smallParent, bigParent, mapRt, iconLayer;
        RawImage map;
        Texture2D tex;
        Image background, catcher, playerDot;
        Text title;
        readonly Image[] roomEdges = new Image[4];
        readonly List<Image> icons = new List<Image>();
        int W, H, used;
        bool expanded;
        Vector2 smallPos;

        const float SmallWidth = 300f, SmallMaxHeight = 210f;

        public static Minimap Create(Game game, RectTransform hud, RectTransform root)
        {
            var bg = UIKit.Img(hud, "Minimap", new Color(0, 0, 0, 0.65f));
            var m = bg.gameObject.AddComponent<Minimap>();
            m.game = game;
            m.self = bg.rectTransform;
            m.background = bg;
            m.smallParent = hud;
            m.bigParent = root;
            m.self.anchorMin = m.self.anchorMax = m.self.pivot = new Vector2(0, 1);

            m.map = UIKit.Rect(m.self, "Map").gameObject.AddComponent<RawImage>();
            m.mapRt = m.map.rectTransform;
            m.mapRt.Fill(6f);
            m.map.raycastTarget = false;
            m.iconLayer = UIKit.Rect(m.mapRt, "Icons").Fill();
            for (int i = 0; i < 4; i++)
            {
                m.roomEdges[i] = UIKit.Img(m.iconLayer, "RoomEdge", new Color(1f, 0.82f, 0.3f, 0.9f));
                m.roomEdges[i].raycastTarget = false;
            }
            m.playerDot = UIKit.Img(m.iconLayer, "Player", new Color(0.4f, 1f, 0.5f), Art.S("ui_disc"));
            m.playerDot.raycastTarget = false;

            m.title = UIKit.Label(m.self, Loc.T("map.title"), 26, UIKit.Amber, TextAnchor.LowerLeft);
            m.title.rectTransform.At(new Vector2(0, 1), new Vector2(0, 0), new Vector2(4, 6), new Vector2(900, 36));
            m.title.gameObject.SetActive(false);

            // full-screen tap catcher used while the big map is open
            m.catcher = UIKit.Img(root, "MinimapCatcher", new Color(0, 0, 0, 0.5f));
            m.catcher.rectTransform.Fill();
            m.catcher.gameObject.AddComponent<Button>().onClick.AddListener(m.Collapse);
            m.catcher.gameObject.SetActive(false);
            m.gameObject.SetActive(false);
            return m;
        }

        // ------------------------------------------------------------------ building

        public void Build(Level level, Vector2 position)
        {
            Clear();
            W = level.W;
            H = level.H;
            tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    px[y * W + x] = TileColor(level.At(x, y));
            tex.SetPixels32(px);
            tex.Apply();
            map.texture = tex;
            smallPos = position;
            Layout();
            gameObject.SetActive(true);
        }

        static Color32 TileColor(char c)
        {
            if (c == ' ') return new Color32(0, 0, 0, 0);
            if (c == 'X') return new Color32(90, 230, 120, 255);
            if (Level.IsWallChar(c)) return new Color32(150, 166, 156, 255);
            if (c == 'L') return new Color32(220, 80, 70, 255);
            if (c == 'D') return new Color32(90, 100, 80, 255);
            if (Level.PropChars.IndexOf(c) >= 0 || "ESO$".IndexOf(c) >= 0) return new Color32(52, 62, 56, 255);
            return new Color32(26, 36, 32, 235);
        }

        public void Clear()
        {
            Collapse();
            if (tex != null) Destroy(tex);
            tex = null;
            map.texture = null;
            gameObject.SetActive(false);
        }

        public void SetPosition(Vector2 position)
        {
            smallPos = position;
            if (!expanded) Layout();
        }

        void Layout()
        {
            if (W == 0) return;
            float aspect = W / (float)H;
            if (expanded)
            {
                self.SetParent(bigParent, false);
                self.SetAsLastSibling();
                self.anchorMin = self.anchorMax = self.pivot = new Vector2(0.5f, 0.5f);
                float w = 1500f, h = w / aspect;
                if (h > 800f) { h = 800f; w = h * aspect; }
                self.sizeDelta = new Vector2(w + 12, h + 12);
                self.anchoredPosition = new Vector2(0, -20);
            }
            else
            {
                self.SetParent(smallParent, false);
                self.anchorMin = self.anchorMax = self.pivot = new Vector2(0, 1);
                float w = SmallWidth, h = w / aspect;
                if (h > SmallMaxHeight) { h = SmallMaxHeight; w = h * aspect; }
                self.sizeDelta = new Vector2(w + 12, h + 12);
                self.anchoredPosition = smallPos;
            }
            title.gameObject.SetActive(expanded);
        }

        // ------------------------------------------------------------------ interaction

        public void OnPointerClick(PointerEventData e)
        {
            if (expanded) Collapse();
            else Expand();
        }

        public void Expand()
        {
            if (W == 0 || game.phase != Game.Phase.Playing) return;
            expanded = true;
            catcher.gameObject.SetActive(true);
            catcher.transform.SetAsLastSibling();
            Layout();
            Sfx.Play("click");
        }

        public void Collapse()
        {
            if (!expanded) return;
            expanded = false;
            catcher.gameObject.SetActive(false);
            Layout();
        }

        // ------------------------------------------------------------------ live markers

        Vector2 ToLocal(Vector2 world)
        {
            var r = mapRt.rect;
            return new Vector2((world.x / W - 0.5f) * r.width, (world.y / H - 0.5f) * r.height);
        }

        void Update()
        {
            if (W == 0 || game.Player == null) return;
            if (expanded && game.phase != Game.Phase.Playing) Collapse();
            float scale = expanded ? 1.7f : 1f;
            float t = Time.unscaledTime;

            // current camera room
            Rect? room = null;
            if (game.CameraAreaId != null && game.Def != null)
                foreach (var a in game.Def.cameraAreas)
                    if (a.id == game.CameraAreaId) room = a.bounds;
            foreach (var e in roomEdges) e.gameObject.SetActive(room.HasValue);
            if (room.HasValue)
            {
                Vector2 min = ToLocal(room.Value.min), max = ToLocal(room.Value.max);
                float th = expanded ? 3f : 2f;
                Edge(roomEdges[0], new Vector2(min.x, max.y), new Vector2(max.x - min.x, th));
                Edge(roomEdges[1], new Vector2(min.x, min.y + th), new Vector2(max.x - min.x, th));
                Edge(roomEdges[2], new Vector2(min.x, max.y), new Vector2(th, max.y - min.y));
                Edge(roomEdges[3], new Vector2(max.x - th, max.y), new Vector2(th, max.y - min.y));
            }

            used = 0;
            foreach (var it in game.Interactables)
            {
                string sprite = IconFor(it, out Color tint);
                if (sprite == null) continue;
                Place(sprite, tint, it.Pos, 20f * scale);
            }
            var target = game.ObjectiveTarget();
            if (target.HasValue)
                Place("ui_target", Color.white, target.Value + Vector2.up * (0.8f + Mathf.Sin(t * 5f) * 0.2f), 22f * scale);
            for (int i = used; i < icons.Count; i++) icons[i].gameObject.SetActive(false);

            float pulse = 1f + Mathf.Sin(t * 6f) * 0.15f;
            playerDot.rectTransform.sizeDelta = Vector2.one * 14f * scale * pulse;
            playerDot.rectTransform.anchoredPosition = ToLocal(game.Player.Pos);
            playerDot.transform.SetAsLastSibling();
        }

        static void Edge(Image img, Vector2 topLeft, Vector2 size)
        {
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = topLeft;
            rt.sizeDelta = size;
        }

        /// Which interactables show on the map: loot, side missions and objective objects.
        static string IconFor(Interactable it, out Color tint)
        {
            tint = Color.white;
            if (!it.Available && !(it is ExitDoor)) return null;
            switch (it)
            {
                case Pickup p:
                    switch (p.kind)
                    {
                        case PickupKind.Keycard: return "item_keycard";
                        case PickupKind.Intel: return "item_intel";
                        case PickupKind.Ammo: return "item_ammo";
                        case PickupKind.Medkit: return "item_medkit";
                        case PickupKind.Decoy: return "item_trap";
                        default: return "prop_prototype";
                    }
                case Chest _: return "prop_chest";
                case DataTerminal t: return t.IsObjective ? "prop_terminal_on" : null;
                case PowerPanel _: return "prop_panel_on";
                case ExitDoor _: tint = new Color(0.5f, 1f, 0.6f); return "tile_exit";
                case Vent _: tint = new Color(0.7f, 0.9f, 1f); return "prop_vent";
                case HideBox _: return "prop_hidebox";
                default: return null;
            }
        }

        void Place(string sprite, Color tint, Vector2 world, float size)
        {
            if (used >= icons.Count)
            {
                var img = UIKit.Img(iconLayer, "Icon", Color.white);
                img.raycastTarget = false;
                img.preserveAspect = true;
                icons.Add(img);
            }
            var icon = icons[used++];
            icon.gameObject.SetActive(true);
            icon.sprite = Art.S(sprite);
            icon.color = tint;
            icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            icon.rectTransform.sizeDelta = Vector2.one * size;
            icon.rectTransform.anchoredPosition = ToLocal(world);
        }
    }
}
