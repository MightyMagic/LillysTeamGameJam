using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace NightShift
{
    /// All screens: main menu, briefing, HUD with touch controls, dialogue, pause, results.
    /// Every visible string comes from Resources/Texts/texts.txt via Loc.
    public class GameUI : MonoBehaviour
    {
        Game game;
        RectTransform root, menu, briefing, hud, dialog, pause, result;

        // HUD widgets
        Text objectiveText, statusText, timerText, alertText, bannerText, toastText, progressLabel;
        RectTransform progressRoot;
        Image progressFill;
        TouchButton shootBtn, interactBtn, decoyBtn;
        VirtualJoystick joystick;
        float bannerTime, toastTime;

        // dialogue widgets
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
            ui.menu = UIKit.Rect(ui.root, "Menu").Fill();
            ui.briefing = UIKit.Rect(ui.root, "Briefing").Fill();
            ui.hud = UIKit.Rect(ui.root, "HUD").Fill();
            ui.BuildHUD();
            ui.dialog = UIKit.Rect(ui.root, "Dialog").Fill();
            ui.BuildDialog();
            ui.pause = UIKit.Rect(ui.root, "Pause").Fill();
            ui.result = UIKit.Rect(ui.root, "Result").Fill();
            return ui;
        }

        void Show(RectTransform screen)
        {
            menu.gameObject.SetActive(screen == menu);
            briefing.gameObject.SetActive(screen == briefing);
            hud.gameObject.SetActive(screen == hud || screen == pause || screen == result || screen == dialog);
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
                int best = Progress.BestRank(id);
                string sub = !unlocked ? Loc.T("menu.locked")
                           : !Progress.IsDone(id) ? Loc.T("menu.not_done")
                           : Loc.F("menu.best_rank", MissionState.RankName((Rank)best));
                UIKit.Btn(left, $"{i:00}  {Loc.T(id + ".title")}\n<size=24>{sub}</size>", () => game.StartMission(idx), 34, unlocked).Size(112);
            }

            // upgrades
            var right = UIKit.Column(menu, "Upgrades", 14f);
            right.At(new Vector2(0.5f, 1f), new Vector2(0f, 1f), new Vector2(40, -250), new Vector2(820, 600));
            UIKit.Label(right, Loc.F("menu.gear", Progress.Credits), 40, UIKit.Amber).Size(54);
            foreach (Upgrade u in System.Enum.GetValues(typeof(Upgrade)))
            {
                var up = u;
                bool has = Progress.Has(u);
                bool afford = Progress.Credits >= Progress.Cost(u);
                string label = Progress.Name(u) + "\n<size=24>" + (has ? Loc.T("menu.bought") : Loc.F("menu.price", Progress.Cost(u))) + "</size>";
                UIKit.Btn(right, label, () => { if (Progress.Buy(up)) { Sfx.Play("pickup"); ShowMenu(); } }, 30, !has && afford).Size(110);
            }
            UIKit.Label(right, Loc.T("menu.gear_hint"), 24, UIKit.Dim).Size(60);

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
            sb.Append("  • " + Loc.T("brief.ranks") + "\n");
            sb.Append($"<color=#ffd060>{Loc.T("brief.threats")}</color>\n  " + def.Threats);
            UIKit.Label(c, sb.ToString(), 28, UIKit.TextCol).Size(390);
            UIKit.Label(c, Loc.T("brief.legend"), 24, UIKit.Dim).Size(70);

            var row = UIKit.Row(c, "Buttons", 30f);
            row.Size(100);
            UIKit.Btn(row, Loc.T("button.back"), game.ShowMenu, 36);
            UIKit.Btn(row, Loc.T("button.start"), game.BeginPlay, 40);
        }

        // ------------------------------------------------------------------ HUD

        void BuildHUD()
        {
            var objBg = UIKit.Img(hud, "ObjectivesBg", new Color(0, 0, 0, 0.45f));
            objBg.rectTransform.At(new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -20), new Vector2(720, 230));
            objectiveText = UIKit.Label(objBg.transform, "", 28, UIKit.TextCol, TextAnchor.UpperLeft);
            objectiveText.rectTransform.Fill(16f);

            var stBg = UIKit.Img(hud, "StatusBg", new Color(0, 0, 0, 0.45f));
            stBg.rectTransform.At(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-130, -20), new Vector2(560, 150));
            statusText = UIKit.Label(stBg.transform, "", 28, UIKit.TextCol, TextAnchor.UpperRight);
            statusText.rectTransform.Fill(14f);

            var pauseBtn = UIKit.Btn(hud, "II", game.Pause, 40);
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

            joystick = VirtualJoystick.Create(root);
            shootBtn = TouchButton.Create(root, "ui_gun", Loc.T("key.shoot"), new Vector2(-170, 190), 210, GameInput.PressShoot);
            interactBtn = TouchButton.Create(root, "ui_hand", Loc.T("key.interact"), new Vector2(-420, 130), 190, GameInput.PressInteract);
            decoyBtn = TouchButton.Create(root, "ui_decoy", Loc.T("key.trap"), new Vector2(-150, 420), 140, GameInput.PressDecoy);
        }

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

            var sb = new StringBuilder();
            sb.Append($"<color=#ffd060>{game.MissionIndex:00} · {m.def.Title}</color>\n");
            if (m.def.tutorial || m.scriptObjective != null)
            {
                if (m.scriptObjective != null) sb.Append($"<color=#ffffff>□ {m.scriptObjective}</color>\n");
            }
            else
            {
                var current = m.Current;
                foreach (var o in m.def.objectives)
                {
                    bool done = m.IsDone(o);
                    string col = done ? "#7a8a7e" : o == current ? "#ffffff" : "#9aa89e";
                    sb.Append($"<color={col}>{(done ? "■" : "□")} {LevelDef.ObjectiveLabel(o)}</color>\n");
                }
            }
            foreach (var s in m.def.sides)
            {
                bool ok = m.SideDone(s);
                sb.Append($"<size=22><color={(ok ? "#8fe39a" : "#e36a5a")}>{(ok ? "+" : "×")} {s.Label}</color></size>\n");
            }
            objectiveText.text = sb.ToString();

            var hp = new StringBuilder();
            for (int i = 0; i < p.maxHp; i++) hp.Append(i < p.hp ? "■" : "□");
            statusText.text = Loc.F("hud.status", hp, p.ammo, p.decoys, m.intel, m.intelTotal)
                + (m.hasKeycard ? "   <color=#ff9a8a>" + Loc.T("hud.keycard") + "</color>" : "")
                + (p.InBox ? "\n<color=#ffd060>" + Loc.T("hud.in_box") + "</color>" : "");

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

            float dt = Time.unscaledDeltaTime;
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
            shootBtn.caption.text = p.Target != null ? "<color=#ff6a5a>" + Loc.T("hud.target") + "</color>" : "";
            decoyBtn.SetActive(p.decoys > 0 && !p.InBox);
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
            dialogFull = text;
            dialogChars = 0f;
            dialogShown = 0;
            dialogText.text = "";
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
                    dialogText.text = dialogFull.Substring(0, n);
                }
            }
            dialogHint.enabled = dialogShown >= dialogFull.Length && Mathf.Repeat(Time.unscaledTime * 2f, 1f) > 0.3f;

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
                dialogText.text = dialogFull;
                return;
            }
            dialogFull = null;
            Sfx.Play("click");
            game.CloseDialog();
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

            var rank = m.Rank;
            Color rc = rank == Rank.Ghost ? UIKit.Green : rank == Rank.SilentOperator ? UIKit.Amber : UIKit.Red;
            var rl = UIKit.Label(c, Loc.F("result.rank", MissionState.RankName(rank)), 72, rc, TextAnchor.MiddleCenter);
            rl.fontStyle = FontStyle.Bold;
            rl.Size(100);

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
            sb.Append($"\n<color=#ffd060>{Loc.T("result.next_reward")}</color>\n");
            sb.Append(Loc.T(rank == Rank.Compromised ? "result.hint_silent" : rank == Rank.SilentOperator ? "result.hint_ghost" : "result.hint_max"));
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
