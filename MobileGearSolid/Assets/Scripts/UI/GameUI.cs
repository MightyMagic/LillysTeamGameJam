using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MobileGearSolid
{
    /// All screens: main menu, briefing, HUD with touch controls, dialogue, pause, results.
    /// Every visible string comes from Resources/Texts/texts.txt via Loc.
    public class GameUI : MonoBehaviour
    {
        Game game;
        RectTransform root, menu, ship, briefing, hud, dialog, cards, pause, result;

        // HUD widgets
        Text objectiveText, timerText, alertText, bannerText, toastText, progressLabel;
        RectTransform progressRoot;
        Image progressFill;
        TouchButton shootBtn, interactBtn, decoyBtn;
        VirtualJoystick joystick;
        float bannerTime, toastTime;
        bool compactHUD;
        RectTransform objectivePanel, statusPanel, heartsRow, counters, pauseButton;
        readonly Image[] frameBars = new Image[4];
        Text roomLabel;

        // dialogue widgets
        TutorialRadioUI tutorialRadio;
        Image dialogTapCatcher;
        Text ActiveDialogText => tutorialRadio.Visible ? tutorialRadio.Replica : dialogText;
        Text ActiveDialogHint => tutorialRadio.Visible ? tutorialRadio.Hint : dialogHint;
        Text dialogName, dialogText, dialogHint;
        Image dialogPortrait;
        RectTransform dialogPortraitFrame, dialogBox;
        string dialogFull;
        float dialogChars;
        int dialogShown;

        const float CharsPerSecond = 48f;

        public static GameUI Create(Game game)
        {
            var go = new GameObject("UI", typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();

            var ui = go.AddComponent<GameUI>();
            ui.game = game;
            ui.root = (RectTransform)go.transform;
            // Black bars clear the unused viewport area, including during a change of room aspect.
            for (int i = 0; i < ui.frameBars.Length; i++)
            {
                ui.frameBars[i] = UIKit.Img(ui.root, "CameraFrame", Color.black);
                ui.frameBars[i].raycastTarget = false;
                ui.frameBars[i].gameObject.SetActive(false);
            }
            ui.menu = UIKit.Rect(ui.root, "Menu").Fill();
            ui.ship = UIKit.Rect(ui.root, "Ship").Fill();
            ui.briefing = UIKit.Rect(ui.root, "Briefing").Fill();
            ui.hud = UIKit.Rect(ui.root, "HUD").Fill();
            ui.BuildHUD();
            ui.dialog = UIKit.Rect(ui.root, "Dialog").Fill();
            ui.BuildDialog();
            ui.tutorialRadio = TutorialRadioUI.Create(ui.dialog, ui.hud);
            ui.cards = UIKit.Rect(ui.root, "Cards").Fill();
            ui.pause = UIKit.Rect(ui.root, "Pause").Fill();
            ui.result = UIKit.Rect(ui.root, "Result").Fill();
            return ui;
        }

        void Show(RectTransform screen)
        {
            if (screen != dialog) tutorialRadio.SetVisible(false);
            if (minimap != null)
            {
                if (screen != hud) minimap.Collapse();
                minimap.gameObject.SetActive(screen == hud && minimapBuilt);
            }
            menu.gameObject.SetActive(screen == menu);
            ship.gameObject.SetActive(screen == ship);
            briefing.gameObject.SetActive(screen == briefing);
            cards.gameObject.SetActive(screen == cards);
            hud.gameObject.SetActive(screen == hud || screen == pause || screen == dialog);
            dialog.gameObject.SetActive(screen == dialog);
            pause.gameObject.SetActive(screen == pause);
            result.gameObject.SetActive(screen == result);
            bool controls = screen == hud;
            joystick.gameObject.SetActive(controls);
            shootBtn.gameObject.SetActive(controls);
            interactBtn.gameObject.SetActive(controls);
            decoyBtn.gameObject.SetActive(controls);
        }

        static RectTransform Window(RectTransform parent, Vector2 size, bool dimBackground)
        {
            UIKit.Clear(parent);
            if (dimBackground) UIKit.Img(parent, "Dim", new Color(0, 0, 0, 0.55f)).rectTransform.Fill();
            var win = UIKit.Img(parent, "Window", UIKit.PanelCol);
            var o = win.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0.35f, 0.7f, 0.4f, 0.7f);
            o.effectDistance = new Vector2(4, -4);
            win.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
            var col = UIKit.Column(win.transform, "Content", 14f);
            col.Fill(40f);
            return col;
        }

        // ------------------------------------------------------------------ menu

        public void ShowMenu()
        {
            Show(menu);
            UIKit.Clear(menu);
            UIKit.Img(menu, "Bg", new Color(0.02f, 0.03f, 0.03f, 1f)).rectTransform.Fill();
            for (int i = 0; i < 54; i++) // scanlines
                UIKit.Img(menu, "Scan", new Color(0.3f, 1f, 0.4f, 0.025f)).rectTransform
                    .At(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -i * 20), new Vector2(1920, 3));

            var title = UIKit.Label(menu, Loc.T("menu.title"), 104, UIKit.Green, TextAnchor.MiddleCenter);
            title.fontStyle = FontStyle.Bold;
            title.rectTransform.At(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -50), new Vector2(1800, 130));
            UIKit.Label(menu, Loc.T("menu.tagline"), 32, UIKit.Dim, TextAnchor.MiddleCenter)
                .rectTransform.At(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -175), new Vector2(1800, 50));

            // missions
            var left = UIKit.Column(menu, "Missions", 14f);
            left.At(new Vector2(0.5f, 1f), new Vector2(1f, 1f), new Vector2(-40, -250), new Vector2(820, 700));
            UIKit.Label(left, Loc.T("menu.missions"), 40, UIKit.Amber).Size(54);
            var campaign = game.Campaign;
            for (int i = 0; i < campaign.Count; i++)
            {
                int idx = i;
                string id = campaign[i];
                bool unlocked = Progress.IsUnlocked(campaign, i);
                string sub = !unlocked ? Loc.T("menu.locked")
                           : !Progress.IsDone(id) ? Loc.T(i == 0 ? "menu.start_here" : "menu.not_done")
                           : Loc.T("menu.completed");
                UIKit.Btn(left, $"{i:00}  {Loc.T(id + ".title")}\n<size=24>{sub}</size>", () => game.StartMission(idx), 34, unlocked).Size(112);
            }

            // right column: the two big entry points of the meta layer
            var right = UIKit.Column(menu, "Modes", 22f);
            right.At(new Vector2(0.5f, 1f), new Vector2(0f, 1f), new Vector2(40, -250), new Vector2(820, 640));
            UIKit.Label(right, Loc.T("menu.modes"), 40, UIKit.Amber).Size(54);
            var raid = UIKit.Btn(right, Loc.T("menu.raid") + "\n<size=26>" +
                (Progress.BestRaid > 0 ? Loc.F("menu.raid_best", Progress.BestRaid, RunState.RoomCount) : Loc.T("menu.raid_sub")) + "</size>",
                game.StartRaid, 46);
            raid.Size(190);
            raid.GetComponent<Image>().color = new Color(0.32f, 0.1f, 0.08f, 0.95f);
            var ship = UIKit.Btn(right, Loc.T("menu.ship") + "\n<size=26>" + Loc.F("menu.ship_sub", Progress.Credits) + "</size>", game.ShowShip, 46);
            ship.Size(190);
            ship.GetComponent<Image>().color = new Color(0.08f, 0.16f, 0.3f, 0.95f);
            UIKit.Label(right, Loc.T("menu.modes_hint"), 26, UIKit.Dim).Size(110);

            UIKit.Label(menu, Loc.T("hint.controls") + "\n" + Loc.T("hint.controls_touch"), 24, UIKit.Dim, TextAnchor.MiddleCenter)
                .rectTransform.At(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 24), new Vector2(1800, 80));
            var reset = UIKit.Btn(menu, Loc.T("menu.reset"), () => { Progress.ResetAll(); ShowMenu(); }, 22);
            reset.GetComponent<RectTransform>().At(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24, -24), new Vector2(260, 56));
        }

        // ------------------------------------------------------------------ briefing

        public void ShowBriefing(LevelDef def)
        {
            Show(briefing);
            var c = Window(briefing, new Vector2(1400, 900), true);
            UIKit.Label(c, Loc.F("brief.header", game.MissionIndex.ToString("00"), def.Title.ToUpperInvariant()), 50, UIKit.Green).Size(70);
            UIKit.Label(c, def.Brief, 30, UIKit.TextCol).Size(120);

            var sb = new StringBuilder($"<color=#ffd060>{Loc.T("brief.objectives")}</color>\n");
            int n = 1;
            foreach (var o in def.objectives) sb.Append($"  {n++}. {LevelDef.ObjectiveLabel(o)}\n");
            sb.Append($"<color=#ffd060>{Loc.T("brief.sides")}</color>\n");
            foreach (var s in def.sides) sb.Append($"  • {s.Label}\n");
            sb.Append($"<color=#ffd060>{Loc.T("brief.threats")}</color>\n  " + def.Threats);
            UIKit.Label(c, sb.ToString(), 28, UIKit.TextCol).Size(390);
            UIKit.Label(c, Loc.T("brief.legend"), 24, UIKit.Dim).Size(70);

            var row = UIKit.Row(c, "Buttons", 30f);
            row.Size(100);
            UIKit.Btn(row, Loc.T("button.back"), game.ShowMenu, 36);
            UIKit.Btn(row, Loc.T("button.start"), game.BeginPlay, 40);
        }

        // ------------------------------------------------------------------ HUD

        // HUD v2: big pixel icons, current objective, objective pointer, damage overlay
        Text missionText, sideText, ammoText, trapText, intelText;
        Image[] hearts;
        GameObject keycardIcon, boxHint;
        RectTransform pointerArrow, pointerMarker;
        Text pointerDistance;
        Image damageOverlay;
        RectTransform hudCards;
        int hudCardCount = -1;
        float damageFlash, heartbeatTimer;

        const int MaxHearts = 6;
        // keep the pointer clear of the panels (top) and the touch controls (bottom / right)
        const float PointerLeft = 110f, PointerRight = 330f, PointerTop = 230f, PointerBottom = 300f;

        void BuildHUD()
        {
            // red vignette under everything else in the HUD: hit flash + low-health pulse
            damageOverlay = UIKit.Img(hud, "DamageOverlay", new Color(1f, 0.05f, 0.03f, 0f), MakeVignette());
            damageOverlay.rectTransform.Fill();
            damageOverlay.raycastTarget = false;

            // --- objective panel (top-left)
            var objBg = UIKit.Img(hud, "ObjectiveBg", new Color(0, 0, 0, 0.6f));
            objectivePanel = objBg.rectTransform;
            objBg.rectTransform.At(new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -20), new Vector2(760, 246));
            var accent = UIKit.Img(objBg.transform, "Accent", UIKit.Amber);
            accent.rectTransform.anchorMin = new Vector2(0, 0);
            accent.rectTransform.anchorMax = new Vector2(0, 1);
            accent.rectTransform.offsetMin = Vector2.zero;
            accent.rectTransform.offsetMax = new Vector2(6, 0);
            missionText = UIKit.Label(objBg.transform, "", 26, UIKit.Amber, TextAnchor.UpperLeft);
            missionText.rectTransform.At(new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -12), new Vector2(720, 34));
            objectiveText = UIKit.Label(objBg.transform, "", 38, Color.white, TextAnchor.UpperLeft);
            objectiveText.fontStyle = FontStyle.Bold;
            objectiveText.rectTransform.At(new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -48), new Vector2(720, 96));
            sideText = UIKit.Label(objBg.transform, "", 30, UIKit.Dim, TextAnchor.LowerLeft);
            sideText.lineSpacing = 1.05f;
            sideText.rectTransform.At(new Vector2(0, 0), new Vector2(0, 0), new Vector2(20, 12), new Vector2(720, 84));

            // --- status panel (top-right): hearts row + ammo / traps / intel / keycard
            var stBg = UIKit.Img(hud, "StatusBg", new Color(0, 0, 0, 0.6f));
            statusPanel = stBg.rectTransform;
            stBg.rectTransform.At(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-126, -20), new Vector2(540, 176));
            heartsRow = UIKit.Rect(stBg.transform, "Hearts");
            heartsRow.At(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-16, -12), new Vector2(510, 72));
            hearts = new Image[MaxHearts];
            for (int i = 0; i < MaxHearts; i++)
            {
                hearts[i] = UIKit.Img(heartsRow, "Heart", Color.white, Art.S("ui_heart"));
                hearts[i].rectTransform.At(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-i * 76, 0), new Vector2(72, 72));
                hearts[i].raycastTarget = false;
            }
            counters = UIKit.Rect(stBg.transform, "Counters");
            counters.At(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-16, 12), new Vector2(510, 72));
            float x = 0;
            intelText = Counter(counters, "item_intel", ref x);
            trapText = Counter(counters, "item_trap", ref x);
            ammoText = Counter(counters, "ui_ammo", ref x);
            keycardIcon = UIKit.Img(counters, "Keycard", Color.white, Art.S("item_keycard")).gameObject;
            keycardIcon.GetComponent<Image>().rectTransform.At(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-x, 0), new Vector2(64, 64));

            // active run cards under the status panel
            hudCards = UIKit.Rect(hud, "ActiveCards");
            hudCards.At(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-126, -204), new Vector2(540, 52));
            var hl = hudCards.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.childAlignment = TextAnchor.MiddleRight;
            hl.spacing = 6;
            hl.childControlWidth = hl.childControlHeight = false;
            hl.childForceExpandWidth = hl.childForceExpandHeight = false;

            var pauseBtn = UIKit.Btn(hud, "II", game.Pause, 40);
            pauseButton = pauseBtn.GetComponent<RectTransform>();
            pauseBtn.GetComponent<RectTransform>().At(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-20, -20), new Vector2(90, 90));

            timerText = UIKit.Label(hud, "", 30, UIKit.Dim, TextAnchor.MiddleCenter);
            timerText.rectTransform.At(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -16), new Vector2(300, 40));
            alertText = UIKit.Label(hud, "", 52, UIKit.Red, TextAnchor.MiddleCenter);
            alertText.fontStyle = FontStyle.Bold;
            alertText.rectTransform.At(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -56), new Vector2(900, 70));
            bannerText = UIKit.Label(hud, "", 40, UIKit.Red, TextAnchor.MiddleCenter);
            bannerText.rectTransform.At(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -140), new Vector2(1300, 60));
            toastText = UIKit.Label(hud, "", 34, UIKit.Green, TextAnchor.MiddleCenter);
            toastText.rectTransform.At(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 330), new Vector2(1100, 50));
            boxHint = UIKit.Label(hud, Loc.T("hud.in_box"), 30, UIKit.Amber, TextAnchor.MiddleCenter).gameObject;
            boxHint.GetComponent<RectTransform>().At(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 390), new Vector2(1100, 44));

            // --- objective pointer: marker over the target when visible, edge arrow when off-screen
            pointerMarker = UIKit.Img(hud, "ObjectiveMarker", Color.white, Art.S("ui_target")).rectTransform;
            pointerMarker.sizeDelta = new Vector2(56, 56);
            pointerMarker.GetComponent<Image>().raycastTarget = false;
            pointerArrow = UIKit.Img(hud, "ObjectiveArrow", Color.white, Art.S("ui_arrow")).rectTransform;
            pointerArrow.sizeDelta = new Vector2(80, 80);
            pointerArrow.GetComponent<Image>().raycastTarget = false;
            pointerDistance = UIKit.Label(hud, "", 26, new Color(1f, 0.85f, 0.3f), TextAnchor.MiddleCenter);
            pointerDistance.rectTransform.sizeDelta = new Vector2(160, 36);
            foreach (var rt in new[] { pointerMarker, pointerArrow, pointerDistance.rectTransform })
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);

            progressRoot = UIKit.Img(hud, "Progress", new Color(0, 0, 0, 0.7f)).rectTransform
                .At(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 240), new Vector2(620, 34));
            progressFill = UIKit.Img(progressRoot, "Fill", UIKit.Green);
            progressFill.rectTransform.anchorMin = Vector2.zero;
            progressFill.rectTransform.anchorMax = new Vector2(0, 1);
            progressFill.rectTransform.offsetMin = new Vector2(4, 4);
            progressFill.rectTransform.offsetMax = new Vector2(-4, -4);
            progressLabel = UIKit.Label(progressRoot, "", 26, UIKit.TextCol, TextAnchor.MiddleCenter);
            progressLabel.rectTransform.At(new Vector2(0.5f, 1), new Vector2(0.5f, 0), new Vector2(0, 4), new Vector2(620, 36));
            progressRoot.gameObject.SetActive(false);

            roomLabel = UIKit.Label(hud, "", 26, UIKit.Dim, TextAnchor.MiddleCenter);
            roomLabel.rectTransform.At(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -112), new Vector2(600, 36));
            roomLabel.gameObject.SetActive(false);
            joystick = VirtualJoystick.Create(root);
            shootBtn = TouchButton.Create(root, "ui_gun", Loc.T("key.shoot"), new Vector2(-170, 190), 210, GameInput.PressShoot);
            interactBtn = TouchButton.Create(root, "ui_hand", Loc.T("key.interact"), new Vector2(-420, 130), 190, GameInput.PressInteract);
            decoyBtn = TouchButton.Create(root, "ui_decoy", Loc.T("key.trap"), new Vector2(-150, 420), 140, GameInput.PressDecoy);
            minimap = Minimap.Create(game, hud, root);
        }

        Minimap minimap;
        public Minimap Minimap => minimap;
        bool minimapBuilt;
        // right under the objective panel
        Vector2 MinimapPos => new Vector2(20, compactHUD ? -142 : -280);

        public void BuildMinimap(Level level)
        {
            minimap.Build(level, MinimapPos);
            minimapBuilt = true;
        }

        public void ClearMinimap()
        {
            minimapBuilt = false;
            minimap.Clear();
        }

        /// Shared compact layout for every playable level.
        public void ConfigureCompactHUD(bool enabled)
        {
            compactHUD = enabled;
            foreach (var b in new[] { shootBtn, interactBtn, decoyBtn }) b.keyHint.gameObject.SetActive(!enabled);
            objectivePanel.sizeDelta = enabled ? new Vector2(720, 112) : new Vector2(760, 246);
            missionText.fontSize = enabled ? 22 : 26;
            objectiveText.fontSize = enabled ? 32 : 38;
            objectiveText.rectTransform.anchoredPosition = new Vector2(20, enabled ? -42 : -48);
            objectiveText.rectTransform.sizeDelta = new Vector2(enabled ? 680 : 720, enabled ? 64 : 96);
            sideText.gameObject.SetActive(!enabled); // contracts remain in briefing and results
            statusPanel.sizeDelta = enabled ? new Vector2(460, 112) : new Vector2(540, 176);
            statusPanel.anchoredPosition = new Vector2(enabled ? -112 : -126, -20);
            heartsRow.localScale = Vector3.one * (enabled ? 0.56f : 1f);
            heartsRow.anchoredPosition = new Vector2(-16, enabled ? -4 : -12);
            counters.localScale = Vector3.one * (enabled ? 0.68f : 1f);
            counters.anchoredPosition = new Vector2(-16, enabled ? 2 : 12);
            pauseButton.sizeDelta = Vector2.one * (enabled ? 80 : 90);
            // Preserve the original floating stick size, home position and touch area.
            LayoutButton(shootBtn, new Vector2(-170, 190), 210);
            LayoutButton(interactBtn, new Vector2(-420, 130), 190);
            LayoutButton(decoyBtn, new Vector2(-150, 420), 140);
            progressRoot.anchoredPosition = new Vector2(0, enabled ? 174 : 240);
            toastText.rectTransform.anchoredPosition = new Vector2(0, enabled ? 222 : 330);
            boxHint.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, enabled ? 270 : 390);
            bannerText.fontSize = enabled ? 30 : 40;
            bannerText.rectTransform.anchoredPosition = new Vector2(0, enabled ? -154 : -140);
            alertText.fontSize = enabled ? 30 : 52;
            alertText.rectTransform.anchoredPosition = new Vector2(0, -56);
            if (!enabled) roomLabel.gameObject.SetActive(false);
            minimap.SetPosition(MinimapPos);
        }

        static void LayoutButton(TouchButton button, Vector2 pos, float size)
        {
            ((RectTransform)button.transform).anchoredPosition = pos;
            ((RectTransform)button.transform).sizeDelta = Vector2.one * size;
            button.icon.rectTransform.sizeDelta = Vector2.one * size * 0.55f;
            button.glow.rectTransform.sizeDelta = Vector2.one * size * 1.3f;
            button.caption.fontSize = 26;
        }

        public void SetCameraFrame(Rect viewport, string areaId)
        {
            bool framed = viewport != new Rect(0, 0, 1, 1);
            SetBar(frameBars[0], Vector2.zero, new Vector2(viewport.xMin, 1), framed);
            SetBar(frameBars[1], new Vector2(viewport.xMax, 0), Vector2.one, framed);
            SetBar(frameBars[2], new Vector2(viewport.xMin, 0), new Vector2(viewport.xMax, viewport.yMin), framed);
            SetBar(frameBars[3], new Vector2(viewport.xMin, viewport.yMax), new Vector2(viewport.xMax, 1), framed);
            roomLabel.gameObject.SetActive(areaId != null && !tutorialRadio.Visible);
            if (areaId != null) roomLabel.text = Loc.T("room." + areaId);
        }

        static void SetBar(Image bar, Vector2 min, Vector2 max, bool visible)
        {
            bar.gameObject.SetActive(visible);
            bar.rectTransform.anchorMin = min;
            bar.rectTransform.anchorMax = max;
            bar.rectTransform.offsetMin = bar.rectTransform.offsetMax = Vector2.zero;
        }

        /// Icon + big number, laid out right-to-left inside `parent`.
        static Text Counter(RectTransform parent, string sprite, ref float x)
        {
            var value = UIKit.Label(parent, "0", 44, Color.white, TextAnchor.MiddleLeft);
            value.fontStyle = FontStyle.Bold;
            value.rectTransform.At(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-x, 0), new Vector2(84, 64));
            var icon = UIKit.Img(parent, "Icon", Color.white, Art.S(sprite));
            icon.rectTransform.At(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-x - 88, 0), new Vector2(64, 64));
            icon.raycastTarget = false;
            x += 170;
            return value;
        }

        static Sprite MakeVignette()
        {
            const int n = 128;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int xx = 0; xx < n; xx++)
                {
                    float dx = (xx + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx * 0.8f + dy * dy * 1.1f);
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.62f, 1.35f, d));
                    px[y * n + xx] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        }

        public void OnPlayerHurt() => damageFlash = 1f;

        public void ShowHUD() => Show(hud);

        /// Blinks one action button: "shoot", "interact", "trap" (or null to stop).
        public void Highlight(string button)
        {
            shootBtn.highlighted = button == "shoot";
            interactBtn.highlighted = button == "interact";
            decoyBtn.highlighted = button == "trap" || button == "decoy";
        }

        public void UpdateHUD()
        {
            var m = game.Mission;
            var p = game.Player;
            if (m == null || p == null) return;
            float dt = Time.unscaledDeltaTime;

            // objective: only the current goal, big; side contracts small underneath
            missionText.text = game.Run != null
                ? Loc.F("hud.raid_title", game.Run.roomIndex + 1, RunState.RoomCount, game.Run.currentTier)
                : $"{game.MissionIndex:00} · {m.def.Title}";
            if (m.def.tutorial || m.scriptObjective != null) objectiveText.text = m.scriptObjective ?? "";
            else
            {
                int idx = System.Array.IndexOf(m.def.objectives, m.Current) + 1;
                objectiveText.text = $"<color=#ffd060>{idx}/{m.def.objectives.Length}</color>  {LevelDef.ObjectiveLabel(m.Current)}";
            }
            var sb = new StringBuilder();
            foreach (var s in m.def.sides)
            {
                bool ok = m.SideDone(s);
                if (sb.Length > 0) sb.Append('\n');
                sb.Append($"<color={(ok ? "#8fe39a" : "#e36a5a")}>{(ok ? "+" : "×")} {s.Label}</color>");
            }
            sideText.text = sb.ToString();

            // status icons
            for (int i = 0; i < MaxHearts; i++)
            {
                hearts[i].enabled = i < p.maxHp;
                // fill from the left: index 0 is the rightmost slot, so mirror it
                int slot = p.maxHp - 1 - i;
                hearts[i].sprite = Art.S(slot < p.hp ? "ui_heart" : "ui_heart_empty");
            }
            ammoText.text = p.ammo.ToString();
            ammoText.color = p.ammo == 0 ? UIKit.Red : Color.white;
            trapText.text = p.decoys.ToString();
            intelText.text = $"{m.intel}/{m.intelTotal}";
            keycardIcon.SetActive(m.hasKeycard);
            if (game.ActiveCards.Count != hudCardCount)
            {
                hudCardCount = game.ActiveCards.Count;
                UIKit.Clear(hudCards);
                foreach (var id in game.ActiveCards)
                {
                    var def = Cards.Get(id);
                    var bgImg = UIKit.Img(hudCards, "Card", new Color(def.tint.r, def.tint.g, def.tint.b, 0.35f));
                    bgImg.rectTransform.sizeDelta = new Vector2(48, 48);
                    bgImg.raycastTarget = false;
                    var ic = UIKit.Img(bgImg.transform, "Icon", Color.white, Art.S(def.icon));
                    ic.rectTransform.Fill(5f);
                    ic.preserveAspect = true;
                    ic.raycastTarget = false;
                }
            }
            boxHint.SetActive(p.InBox);

            timerText.text = UIKit.Time(m.time);

            if (m.alertActive)
            {
                alertText.color = Color.Lerp(UIKit.Red, Color.white, Mathf.PingPong(Time.time * 3f, 1f) * 0.4f);
                alertText.text = Loc.F("hud.alert", Mathf.CeilToInt(m.alertTimer));
            }
            else if (!game.Level.PowerOn)
            {
                alertText.color = UIKit.Amber;
                alertText.text = Loc.F("hud.power_off", Mathf.CeilToInt(game.Level.PowerTimeLeft));
            }
            else alertText.text = "";

            bannerTime -= dt;
            bannerText.enabled = bannerTime > 0f;
            if (bannerTime < 0.5f) bannerText.color = new Color(bannerText.color.r, bannerText.color.g, bannerText.color.b, Mathf.Clamp01(bannerTime * 2f));
            toastTime -= dt;
            toastText.enabled = toastTime > 0f;
            if (toastTime < 0.5f) toastText.color = new Color(UIKit.Green.r, UIKit.Green.g, UIKit.Green.b, Mathf.Clamp01(toastTime * 2f));

            string prompt = p.ActionPrompt;
            interactBtn.SetActive(prompt != null);
            interactBtn.caption.text = prompt ?? "";
            shootBtn.SetActive(p.ammo > 0 && !p.InBox);
            shootBtn.caption.text = !compactHUD && p.Target != null ? "<color=#ff6a5a>" + Loc.T("hud.target") + "</color>" : "";
            decoyBtn.SetActive(p.decoys > 0 && !p.InBox);

            UpdateDamageOverlay(p, dt);
            UpdatePointer(p);
        }

        void UpdateDamageOverlay(Player p, float dt)
        {
            damageFlash = Mathf.Max(0f, damageFlash - dt * 1.8f);
            float pulse = 0f;
            if (p.hp == 1 && !p.dead)
            {
                // heartbeat: two quick pulses per beat
                heartbeatTimer -= dt;
                if (heartbeatTimer <= 0f)
                {
                    heartbeatTimer = 0.95f;
                    Sfx.Play("heart", 0.9f);
                }
                float t = 0.95f - heartbeatTimer;
                pulse = 0.3f * Mathf.Exp(-t * 9f) + 0.2f * Mathf.Exp(-Mathf.Max(0f, t - 0.18f) * 9f) * (t > 0.18f ? 1f : 0f) + 0.12f;
            }
            else heartbeatTimer = 0f;
            var c = damageOverlay.color;
            c.a = Mathf.Clamp01(damageFlash * 0.6f + pulse);
            damageOverlay.color = c;

            // hearts throb with the heartbeat
            float s = p.hp == 1 ? 1f + pulse * 0.4f : 1f;
            foreach (var h in hearts) h.rectTransform.localScale = new Vector3(s, s, 1f);
        }

        void UpdatePointer(Player p)
        {
            var target = game.ObjectiveTarget();
            var cam = Camera.main;
            bool show = target.HasValue && cam != null;
            if (!show)
            {
                pointerArrow.gameObject.SetActive(false);
                pointerMarker.gameObject.SetActive(false);
                pointerDistance.gameObject.SetActive(false);
                return;
            }

            Vector2 world = target.Value;
            Vector3 sp = cam.WorldToScreenPoint(world + Vector2.up * 1.1f);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(hud, sp, null, out var local);
            var r = hud.rect;
            var inner = new Rect(r.xMin + PointerLeft, r.yMin + PointerBottom, r.width - PointerLeft - PointerRight, r.height - PointerTop - PointerBottom);
            if (compactHUD && game.RoomCameraActive && cam.rect != new Rect(0, 0, 1, 1))
            {
                Rect v = cam.rect;
                inner = new Rect(r.xMin + v.x * r.width + 24, r.yMin + v.y * r.height + 30,
                    v.width * r.width - 48, v.height * r.height - 60);
            }
            float dist = Vector2.Distance(p.Pos, world);
            pointerDistance.gameObject.SetActive(true);
            pointerDistance.text = Loc.F("hud.distance", Mathf.RoundToInt(dist));

            if (sp.z > 0 && inner.Contains(local))
            {
                // target on screen: bobbing marker over it
                pointerArrow.gameObject.SetActive(false);
                pointerMarker.gameObject.SetActive(dist > 1.2f);
                pointerDistance.gameObject.SetActive(dist > 1.2f);
                float bob = Mathf.Sin(Time.unscaledTime * 5f) * 8f;
                pointerMarker.anchoredPosition = local + new Vector2(0, 20 + bob) - r.center;
                pointerDistance.rectTransform.anchoredPosition = local + new Vector2(0, 66 + bob) - r.center;
                return;
            }

            // off-screen: arrow on the screen edge pointing at the target
            pointerMarker.gameObject.SetActive(false);
            pointerArrow.gameObject.SetActive(true);
            Vector2 c = inner.center;
            Vector2 dir = (local - c);
            if (sp.z < 0) dir = -dir;
            if (dir.sqrMagnitude < 1f) dir = Vector2.up;
            float k = Mathf.Min(
                Mathf.Abs(dir.x) > 0.01f ? inner.width * 0.5f / Mathf.Abs(dir.x) : float.MaxValue,
                Mathf.Abs(dir.y) > 0.01f ? inner.height * 0.5f / Mathf.Abs(dir.y) : float.MaxValue);
            Vector2 edge = c + dir * k;
            float pulse = 1f + Mathf.Sin(Time.unscaledTime * 6f) * 0.08f;
            pointerArrow.anchoredPosition = edge - r.center;
            pointerArrow.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            pointerArrow.localScale = new Vector3(pulse, pulse, 1f);
            pointerDistance.rectTransform.anchoredPosition = edge - dir.normalized * 70f - r.center;
        }

        public void SetProgress(float value, string label)
        {
            bool show = value >= 0f;
            progressRoot.gameObject.SetActive(show);
            if (!show) return;
            progressFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(value), 1);
            progressLabel.text = label + $"  {Mathf.RoundToInt(value * 100)}%";
        }

        public void ShowBanner(string text, Color color, float duration)
        {
            bannerText.text = text;
            bannerText.color = color;
            bannerTime = duration;
            bannerText.enabled = true;
        }

        public void Toast(string text)
        {
            toastText.text = text;
            toastText.color = UIKit.Green;
            toastTime = 2.8f;
            toastText.enabled = true;
        }

        // ------------------------------------------------------------------ dialogue (visual novel / codec)

        void BuildDialog()
        {
            // full-screen invisible button: tap anywhere to continue
            var catcher = UIKit.Img(dialog, "TapCatcher", new Color(0, 0, 0, 0.35f));
            dialogTapCatcher = catcher;
            catcher.rectTransform.Fill();
            catcher.gameObject.AddComponent<Button>().onClick.AddListener(AdvanceDialog);

            var box = UIKit.Img(dialog, "Box", new Color(0.02f, 0.06f, 0.04f, 0.96f));
            dialogBox = box.rectTransform.At(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 30), new Vector2(1760, 340));
            box.raycastTarget = false;
            var o = box.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0.4f, 0.9f, 0.5f, 0.9f);
            o.effectDistance = new Vector2(4, -4);

            var frame = UIKit.Img(dialogBox, "PortraitFrame", new Color(0.08f, 0.22f, 0.12f, 1f));
            dialogPortraitFrame = frame.rectTransform;
            frame.raycastTarget = false;
            var fo = frame.gameObject.AddComponent<Outline>();
            fo.effectColor = new Color(0.4f, 0.9f, 0.5f, 0.9f);
            fo.effectDistance = new Vector2(3, -3);
            for (int i = 0; i < 25; i++) // codec scanlines
            {
                var s = UIKit.Img(frame.transform, "Scan", new Color(0.4f, 1f, 0.5f, 0.06f));
                s.rectTransform.At(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -i * 12), new Vector2(300, 4));
                s.raycastTarget = false;
            }
            dialogPortrait = UIKit.Img(frame.transform, "Portrait", Color.white);
            dialogPortrait.rectTransform.Fill(10f);
            dialogPortrait.preserveAspect = true;
            dialogPortrait.raycastTarget = false;

            dialogName = UIKit.Label(dialogBox, "", 40, UIKit.Amber, TextAnchor.UpperLeft);
            dialogName.fontStyle = FontStyle.Bold;
            dialogText = UIKit.Label(dialogBox, "", 36, UIKit.TextCol, TextAnchor.UpperLeft);
            dialogHint = UIKit.Label(dialogBox, Loc.T("dialog.continue"), 24, UIKit.Dim, TextAnchor.LowerRight);
            dialogHint.rectTransform.At(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-30, 18), new Vector2(600, 40));
        }

        public void ShowDialog(string speaker, string text)
        {
            Show(dialog);
            bool radio = game.Def != null && game.Def.tutorial;
            tutorialRadio.SetVisible(radio);
            dialogBox.gameObject.SetActive(!radio);
            dialogTapCatcher.color = radio ? Color.clear : new Color(0, 0, 0, .35f);
            if (radio)
            {
                tutorialRadio.ShowSpeaker(speaker);
                UpdateHUD();
                pointerArrow.gameObject.SetActive(false);
                pointerMarker.gameObject.SetActive(false);
                pointerDistance.gameObject.SetActive(false);
            }
            else
            {
                bool right = speaker == "boss"; // the hero talks from the right, the radio from the left
                dialogPortraitFrame.At(new Vector2(right ? 1 : 0, 0.5f), new Vector2(right ? 1 : 0, 0.5f),
                    new Vector2(right ? -25 : 25, 0), new Vector2(300, 300));
                float textLeft = right ? 40 : 350, textRight = right ? 350 : 40;
                dialogName.alignment = right ? TextAnchor.UpperRight : TextAnchor.UpperLeft;
                dialogName.rectTransform.anchorMin = new Vector2(0, 1);
                dialogName.rectTransform.anchorMax = new Vector2(1, 1);
                dialogName.rectTransform.pivot = new Vector2(0.5f, 1);
                dialogName.rectTransform.offsetMin = new Vector2(textLeft, -80);
                dialogName.rectTransform.offsetMax = new Vector2(-textRight, -24);
                dialogText.rectTransform.anchorMin = Vector2.zero;
                dialogText.rectTransform.anchorMax = Vector2.one;
                dialogText.rectTransform.offsetMin = new Vector2(textLeft, 60);
                dialogText.rectTransform.offsetMax = new Vector2(-textRight, -90);

                var portrait = Art.S("portrait_" + speaker);
                dialogPortrait.sprite = portrait;
                dialogPortrait.enabled = portrait != null;
                dialogName.text = Loc.Has("speaker." + speaker) ? Loc.T("speaker." + speaker) : speaker;
            }
            dialogFull = text;
            dialogChars = 0f;
            dialogShown = 0;
            ActiveDialogText.text = "";
            Sfx.Play("codec", 0.6f);
        }

        public void UpdateDialog()
        {
            if (dialogFull == null) return;
            if (dialogShown < dialogFull.Length)
            {
                dialogChars += Time.unscaledDeltaTime * CharsPerSecond;
                int n = Mathf.Min(dialogFull.Length, Mathf.FloorToInt(dialogChars));
                if (n != dialogShown)
                {
                    if (n / 3 != dialogShown / 3) Sfx.Play("blip");
                    dialogShown = n;
                    ActiveDialogText.text = dialogFull.Substring(0, n);
                }
            }
            ActiveDialogHint.enabled = dialogShown >= dialogFull.Length && Mathf.Repeat(Time.unscaledTime * 2f, 1f) > 0.3f;

            var k = Keyboard.current;
            if (k != null && (k.spaceKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame || k.eKey.wasPressedThisFrame))
                AdvanceDialog();
        }

        void AdvanceDialog()
        {
            if (dialogFull == null) return;
            if (dialogShown < dialogFull.Length)
            {
                // first tap completes the line, second tap continues
                dialogShown = dialogFull.Length;
                ActiveDialogText.text = dialogFull;
                return;
            }
            dialogFull = null;
            Sfx.Play("click");
            game.CloseDialog();
        }

        // ------------------------------------------------------------------ ship (meta upgrades)

        Image shipBoss;

        public void ShowShip()
        {
            Show(ship);
            UIKit.Clear(ship);
            var bg = UIKit.Img(ship, "Deck", Color.white, Art.S("bg_deck"));
            bg.rectTransform.Fill();
            bg.preserveAspect = false;

            // Big Boss standing on the helipad
            shipBoss = UIKit.Img(ship, "BigBoss", Color.white, Art.S("player_down_0"));
            shipBoss.rectTransform.At(new Vector2(0.4f, 0.24f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(300, 300));
            shipBoss.preserveAspect = true;

            var title = UIKit.Label(ship, Loc.T("ship.title"), 72, UIKit.Green, TextAnchor.UpperLeft);
            title.fontStyle = FontStyle.Bold;
            title.rectTransform.At(new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -24), new Vector2(900, 90));
            UIKit.Label(ship, Loc.T("ship.subtitle"), 28, UIKit.TextCol, TextAnchor.UpperLeft)
                .rectTransform.At(new Vector2(0, 1), new Vector2(0, 1), new Vector2(44, -112), new Vector2(760, 80));

            // credits
            var cr = UIKit.Img(ship, "Credits", new Color(0, 0, 0, 0.6f));
            cr.rectTransform.At(new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -200), new Vector2(420, 70));
            var crText = UIKit.Label(cr.transform, Loc.F("ship.credits", Progress.Credits), 40, UIKit.Amber, TextAnchor.MiddleCenter);
            crText.fontStyle = FontStyle.Bold;
            crText.rectTransform.Fill();

            // upgrades panel on the right
            var panel = UIKit.Img(ship, "Upgrades", new Color(0.02f, 0.05f, 0.08f, 0.88f));
            panel.rectTransform.At(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-30, 10), new Vector2(900, 900));
            var col = UIKit.Column(panel.transform, "Rows", 10f);
            col.Fill(22f);
            UIKit.Label(col, Loc.T("ship.upgrades"), 40, UIKit.Amber).Size(54);
            foreach (Stat s in System.Enum.GetValues(typeof(Stat))) StatRow(col, s);

            var back = UIKit.Btn(ship, Loc.T("button.back"), game.ShowMenu, 36);
            back.GetComponent<RectTransform>().At(new Vector2(0, 0), new Vector2(0, 0), new Vector2(40, 40), new Vector2(300, 100));
            var raid = UIKit.Btn(ship, Loc.T("menu.raid"), game.StartRaid, 36);
            raid.GetComponent<RectTransform>().At(new Vector2(0, 0), new Vector2(0, 0), new Vector2(360, 40), new Vector2(420, 100));
            raid.GetComponent<Image>().color = new Color(0.32f, 0.1f, 0.08f, 0.95f);
        }

        void StatRow(RectTransform parent, Stat s)
        {
            int lvl = Meta.Level(s), max = Meta.MaxLevel(s);
            var row = UIKit.Img(parent, "Row", new Color(1, 1, 1, 0.05f));
            row.Size(124);

            var icon = UIKit.Img(row.transform, "Icon", Color.white, Art.S(Meta.Icon(s)));
            icon.rectTransform.At(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(14, 0), new Vector2(84, 84));
            icon.preserveAspect = true;

            var name = UIKit.Label(row.transform, $"{Meta.Name(s)}  <color=#ffd060>{Meta.ValueText(s, lvl)}</color>", 32, Color.white, TextAnchor.UpperLeft);
            name.fontStyle = FontStyle.Bold;
            name.rectTransform.At(new Vector2(0, 1), new Vector2(0, 1), new Vector2(116, -10), new Vector2(480, 44));
            UIKit.Label(row.transform, Meta.Desc(s), 22, UIKit.Dim, TextAnchor.UpperLeft)
                .rectTransform.At(new Vector2(0, 1), new Vector2(0, 1), new Vector2(116, -54), new Vector2(520, 66));

            // level pips
            for (int i = 0; i < max; i++)
            {
                var pip = UIKit.Img(row.transform, "Pip", i < lvl ? UIKit.Green : new Color(1, 1, 1, 0.15f));
                pip.rectTransform.At(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-262 - (max - 1 - i) * 34, -18), new Vector2(26, 12));
            }

            bool maxed = Meta.IsMax(s);
            bool afford = Progress.Credits >= Meta.Cost(s);
            string label = maxed ? Loc.T("ship.max") : Loc.F("ship.upgrade", Meta.Cost(s), Meta.ValueText(s, lvl + 1));
            var buy = UIKit.Btn(row.transform, label, () =>
            {
                if (Meta.Buy(s)) { Sfx.Play("success", 0.7f); ShowShip(); }
            }, 26, !maxed && afford);
            buy.GetComponent<RectTransform>().At(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-12, 0), new Vector2(230, 96));
        }

        void Update()
        {
            // idle "breathing" of Big Boss on the deck
            if (shipBoss != null && ship.gameObject.activeSelf)
            {
                float t = Time.unscaledTime;
                shipBoss.rectTransform.localScale = new Vector3(1f, 1f + Mathf.Sin(t * 2f) * 0.015f, 1f);
                shipBoss.sprite = Art.S(Mathf.Repeat(t, 6f) < 0.4f ? "player_side_0" : "player_down_0");
            }
        }

        // ------------------------------------------------------------------ night raid screens

        public void ShowRaidIntro(RunState run)
        {
            Show(briefing);
            var c = Window(briefing, new Vector2(1400, 860), true);
            UIKit.Label(c, Loc.T("raid.title"), 54, UIKit.Red).Size(74);
            UIKit.Label(c, Loc.F("raid.brief", RunState.RoomCount), 30, UIKit.TextCol).Size(230);
            UIKit.Label(c, Loc.F("raid.loadout", Meta.MaxHp, Meta.Damage, Meta.Ammo, Meta.Traps), 30, UIKit.Amber).Size(90);
            UIKit.Label(c, Loc.T("raid.hint"), 26, UIKit.Dim).Size(150);
            var row = UIKit.Row(c, "Buttons", 30f);
            row.Size(100);
            UIKit.Btn(row, Loc.T("button.back"), game.ShowMenu, 36);
            UIKit.Btn(row, Loc.T("button.start"), game.BeginPlay, 40);
        }

        public void ShowCards(List<CardDef> options)
        {
            Show(cards);
            UIKit.Clear(cards);
            UIKit.Img(cards, "Dim", new Color(0, 0, 0, 0.75f)).rectTransform.Fill();
            var title = UIKit.Label(cards, Loc.T("cards.title"), 60, UIKit.Amber, TextAnchor.MiddleCenter);
            title.fontStyle = FontStyle.Bold;
            title.rectTransform.At(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -70), new Vector2(1600, 80));
            UIKit.Label(cards, Loc.T("cards.subtitle"), 30, UIKit.TextCol, TextAnchor.MiddleCenter)
                .rectTransform.At(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -150), new Vector2(1600, 50));

            float w = 440, gap = 50, total = options.Count * w + (options.Count - 1) * gap;
            for (int i = 0; i < options.Count; i++)
            {
                var def = options[i];
                var card = UIKit.Img(cards, "Card", new Color(0.05f, 0.08f, 0.07f, 0.98f));
                card.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(0, 0.5f), new Vector2(-total / 2 + i * (w + gap), -40), new Vector2(w, 600));
                var frame = card.gameObject.AddComponent<Outline>();
                frame.effectColor = def.tint;
                frame.effectDistance = new Vector2(5, -5);
                var band = UIKit.Img(card.transform, "Band", new Color(def.tint.r, def.tint.g, def.tint.b, 0.25f));
                band.rectTransform.At(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -20), new Vector2(w - 40, 230));
                var icon = UIKit.Img(card.transform, "Icon", Color.white, Art.S(def.icon));
                icon.rectTransform.At(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -45), new Vector2(180, 180));
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                int owned = Cards.Count(game.ActiveCards, def.id);
                var name = UIKit.Label(card.transform, def.Name + (owned > 0 ? $" <color=#8fe39a>+{owned}</color>" : ""), 38, Color.white, TextAnchor.MiddleCenter);
                name.fontStyle = FontStyle.Bold;
                name.rectTransform.At(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -270), new Vector2(w - 30, 60));
                UIKit.Label(card.transform, def.Desc, 30, UIKit.TextCol, TextAnchor.UpperCenter)
                    .rectTransform.At(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -340), new Vector2(w - 50, 170));
                var take = UIKit.Label(card.transform, Loc.T("cards.take"), 30, UIKit.Green, TextAnchor.MiddleCenter);
                take.rectTransform.At(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 24), new Vector2(w - 40, 50));
                var btn = card.gameObject.AddComponent<Button>();
                var picked = def;
                btn.onClick.AddListener(() => game.PickCard(picked));
            }
        }

        public void ShowRoomCleared(RunState run, MissionState m)
        {
            Show(result);
            progressRoot.gameObject.SetActive(false);
            var c = Window(result, new Vector2(1100, 640), true);
            UIKit.Label(c, Loc.F("raid.room_cleared", run.roomIndex + 1, RunState.RoomCount), 52, UIKit.Green, TextAnchor.MiddleCenter).Size(80);
            UIKit.Label(c, Loc.F("raid.room_stats", m.alerts, m.kills, run.credits, run.hp, run.ammo, run.traps), 32, UIKit.TextCol, TextAnchor.MiddleCenter).Size(170);
            CardStrip(c, run.cards, 64);
            UIKit.Label(c, Loc.T(run.roomIndex + 2 >= RunState.RoomCount ? "raid.next_final" : "raid.next"), 28, UIKit.Amber, TextAnchor.MiddleCenter).Size(50);
            var row = UIKit.Row(c, "Buttons", 24f);
            row.Size(100);
            UIKit.Btn(row, Loc.T("raid.retreat"), game.RetreatRaid, 30);
            UIKit.Btn(row, Loc.T("raid.continue"), game.ContinueRaid, 38);
        }

        public void ShowRaidResult(RunState run, int cleared)
        {
            Show(result);
            progressRoot.gameObject.SetActive(false);
            Highlight(null);
            var c = Window(result, new Vector2(1200, 820), true);
            UIKit.Label(c, Loc.T(run.won ? "raid.won" : run.retreated ? "raid.retreated" : "raid.lost"), 58, run.won ? UIKit.Green : run.retreated ? UIKit.Amber : UIKit.Red, TextAnchor.MiddleCenter).Size(84);
            UIKit.Label(c, Loc.F("raid.result_stats", cleared, RunState.RoomCount, Progress.BestRaid, run.kills, run.alerts, UIKit.Time(run.time)), 32, UIKit.TextCol, TextAnchor.MiddleCenter).Size(150);
            var cr = UIKit.Label(c, Loc.F("raid.result_credits", run.credits, Progress.Credits), 40, UIKit.Amber, TextAnchor.MiddleCenter);
            cr.fontStyle = FontStyle.Bold;
            cr.Size(70);
            CardStrip(c, run.cards, 72);
            UIKit.Label(c, Loc.T("raid.result_hint"), 26, UIKit.Dim, TextAnchor.MiddleCenter).Size(70);
            var row = UIKit.Row(c, "Buttons", 24f);
            row.Size(100);
            UIKit.Btn(row, Loc.T("button.menu"), game.ShowMenu, 32);
            UIKit.Btn(row, Loc.T("menu.ship"), game.ShowShip, 32);
            UIKit.Btn(row, Loc.T("raid.again"), game.StartRaid, 32);
        }

        /// Row of small card icons.
        static void CardStrip(RectTransform parent, List<CardId> list, float size)
        {
            var row = UIKit.Rect(parent, "Cards");
            row.Size(size + 10);
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.childAlignment = TextAnchor.MiddleCenter;
            h.spacing = 10;
            h.childControlWidth = h.childControlHeight = false;
            h.childForceExpandWidth = h.childForceExpandHeight = false;
            if (list.Count == 0)
            {
                var none = UIKit.Label(row, Loc.T("raid.no_cards"), 26, UIKit.Dim, TextAnchor.MiddleCenter);
                none.rectTransform.sizeDelta = new Vector2(600, size);
                return;
            }
            foreach (var id in list)
            {
                var def = Cards.Get(id);
                var bgImg = UIKit.Img(row, "Card", new Color(def.tint.r, def.tint.g, def.tint.b, 0.3f));
                bgImg.rectTransform.sizeDelta = new Vector2(size, size);
                var ic = UIKit.Img(bgImg.transform, "Icon", Color.white, Art.S(def.icon));
                ic.rectTransform.Fill(6f);
                ic.preserveAspect = true;
            }
        }

        // ------------------------------------------------------------------ pause / results

        public void ShowPause()
        {
            Show(pause);
            var c = Window(pause, new Vector2(900, 640), true);
            UIKit.Label(c, Loc.T("pause.title"), 60, UIKit.Green, TextAnchor.MiddleCenter).Size(90);
            UIKit.Btn(c, Loc.T("button.resume"), game.Resume, 38).Size(100);
            UIKit.Btn(c, Loc.T("button.restart"), game.Restart, 38).Size(100);
            UIKit.Btn(c, Loc.T("button.quit"), game.ShowMenu, 38).Size(100);
            UIKit.Label(c, Loc.T("hint.controls"), 22, UIKit.Dim, TextAnchor.MiddleCenter).Size(80);
        }

        public void ShowResult(MissionState m, int credits)
        {
            Show(result);
            progressRoot.gameObject.SetActive(false);
            Highlight(null);

            if (m.def.tutorial)
            {
                var t = Window(result, new Vector2(1100, 520), true);
                UIKit.Label(t, Loc.T("result.tutorial_done"), 56, UIKit.Green, TextAnchor.MiddleCenter).Size(90);
                UIKit.Label(t, Loc.T("result.tutorial_text"), 30, UIKit.TextCol, TextAnchor.MiddleCenter).Size(170);
                var tr = UIKit.Row(t, "Buttons", 24f);
                tr.Size(100);
                UIKit.Btn(tr, Loc.T("button.menu"), game.ShowMenu, 34);
                if (game.HasNextMission) UIKit.Btn(tr, Loc.T("button.next"), game.NextMission, 34);
                return;
            }

            var c = Window(result, new Vector2(1300, 940), true);
            UIKit.Label(c, Loc.T("result.success"), 54, UIKit.Green, TextAnchor.MiddleCenter).Size(70);

            var sb = new StringBuilder();
            sb.Append(Loc.F("result.stats", UIKit.Time(m.time), m.alerts, m.kills, m.intel, m.intelTotal, credits)).Append('\n');
            if (m.alerts > 0 && m.lastAlertReason != null)
                sb.Append("<color=#9aa89e>").Append(Loc.F("result.why", m.lastAlertReason.ToLowerInvariant())).Append("</color>\n");
            sb.Append($"\n<color=#ffd060>{Loc.T("brief.sides")}</color>\n");
            foreach (var s in m.def.sides)
            {
                bool ok = m.SideDone(s);
                sb.Append($"<color={(ok ? "#8fe39a" : "#e36a5a")}>{(ok ? "■ " + Loc.T("result.done") : "□ " + Loc.T("result.not_done"))}</color> — {s.Label}\n");
            }
            UIKit.Label(c, sb.ToString(), 30, UIKit.TextCol, TextAnchor.UpperCenter).Size(470);

            var row = UIKit.Row(c, "Buttons", 24f);
            row.Size(100);
            UIKit.Btn(row, Loc.T("button.menu"), game.ShowMenu, 34);
            UIKit.Btn(row, Loc.T("button.retry"), game.Restart, 34);
            if (game.HasNextMission) UIKit.Btn(row, Loc.T("button.next"), game.NextMission, 34);
        }

        public void ShowFail(MissionState m)
        {
            Show(result);
            progressRoot.gameObject.SetActive(false);
            var c = Window(result, new Vector2(1000, 560), true);
            UIKit.Label(c, Loc.T("result.fail"), 60, UIKit.Red, TextAnchor.MiddleCenter).Size(90);
            string why = m.lastAlertReason != null ? Loc.F("result.why", m.lastAlertReason.ToLowerInvariant()) + "\n" : "";
            UIKit.Label(c, Loc.T("result.fail_text") + "\n" + why + Loc.T("result.fail_hint"), 30, UIKit.TextCol, TextAnchor.MiddleCenter).Size(200);
            var row = UIKit.Row(c, "Buttons", 24f);
            row.Size(100);
            UIKit.Btn(row, Loc.T("button.menu"), game.ShowMenu, 36);
            UIKit.Btn(row, Loc.T("button.retry"), game.Restart, 36);
        }
    }
}
