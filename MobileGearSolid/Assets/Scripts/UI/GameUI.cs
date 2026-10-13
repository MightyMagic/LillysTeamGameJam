using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace NightShift
{
    /// All screens: main menu, briefing, HUD with touch controls, pause, results.
    public class GameUI : MonoBehaviour
    {
        Game game;
        RectTransform root, menu, briefing, hud, pause, result;

        // HUD widgets
        Text objectiveText, statusText, timerText, alertText, bannerText, toastText, progressLabel;
        RectTransform progressRoot;
        Image progressFill;
        TouchButton shootBtn, interactBtn, decoyBtn;
        VirtualJoystick joystick;
        float bannerTime, toastTime;

        const string ControlsHint =
            "Клавиатура: WASD/стрелки — движение · Пробел — выстрел · E — действие · Q — приманка · Esc — пауза";

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
            ui.pause = UIKit.Rect(ui.root, "Pause").Fill();
            ui.result = UIKit.Rect(ui.root, "Result").Fill();
            ui.BuildHUD();
            return ui;
        }

        void Show(RectTransform screen)
        {
            menu.gameObject.SetActive(screen == menu);
            briefing.gameObject.SetActive(screen == briefing);
            hud.gameObject.SetActive(screen == hud || screen == pause || screen == result);
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

            var title = UIKit.Label(menu, "PROJECT: NIGHT SHIFT", 104, UIKit.Green, TextAnchor.MiddleCenter);
            title.fontStyle = FontStyle.Bold;
            title.rectTransform.At(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -60), new Vector2(1800, 130));
            UIKit.Label(menu, "Зайди, обмани охрану, забери секрет и выйди. Чем чище — тем лучше награда.", 32, UIKit.Dim, TextAnchor.MiddleCenter)
                .rectTransform.At(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -190), new Vector2(1800, 50));

            // missions
            var left = UIKit.Column(menu, "Missions", 18f);
            left.At(new Vector2(0.5f, 1f), new Vector2(1f, 1f), new Vector2(-40, -280), new Vector2(820, 620));
            UIKit.Label(left, "МИССИИ", 40, UIKit.Amber).Size(54);
            for (int i = 0; i < MissionDef.All.Length; i++)
            {
                int idx = i;
                var def = MissionDef.All[i];
                bool unlocked = Progress.IsUnlocked(i);
                int best = Progress.BestRank(i);
                string sub = !unlocked ? "ЗАКРЫТО — пройди предыдущую миссию"
                           : best < 0 ? "Не пройдена"
                           : "Лучший ранг: " + MissionState.RankName((Rank)best);
                UIKit.Btn(left, $"{def.code}  {def.title}\n<size=24>{sub}</size>", () => game.StartMission(idx), 36, unlocked).Size(130);
            }

            // upgrades
            var right = UIKit.Column(menu, "Upgrades", 18f);
            right.At(new Vector2(0.5f, 1f), new Vector2(0f, 1f), new Vector2(40, -280), new Vector2(820, 620));
            UIKit.Label(right, $"СНАРЯЖЕНИЕ    <color=#ffd060>Кредиты: {Progress.Credits}</color>", 40, UIKit.Amber).Size(54);
            foreach (Upgrade u in System.Enum.GetValues(typeof(Upgrade)))
            {
                var up = u;
                bool has = Progress.Has(u);
                bool afford = Progress.Credits >= Progress.Cost(u);
                string label = Progress.Name(u) + "\n<size=24>" + (has ? "КУПЛЕНО" : $"Цена: {Progress.Cost(u)}") + "</size>";
                UIKit.Btn(right, label, () => { if (Progress.Buy(up)) { Sfx.Play("pickup"); ShowMenu(); } }, 30, !has && afford).Size(110);
            }
            UIKit.Label(right, "Улучшения расширяют тактику, но не обязательны для прохождения.", 24, UIKit.Dim).Size(60);

            UIKit.Label(menu, ControlsHint + "\nТелефон: левый стик — движение, кнопки справа — выстрел, действие, приманка.", 24, UIKit.Dim, TextAnchor.MiddleCenter)
                .rectTransform.At(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 30), new Vector2(1800, 80));
            var reset = UIKit.Btn(menu, "Сбросить прогресс", () => { Progress.ResetAll(); ShowMenu(); }, 22);
            reset.GetComponent<RectTransform>().At(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24, -24), new Vector2(260, 56));
        }

        // ------------------------------------------------------------------ briefing

        public void ShowBriefing(MissionDef def)
        {
            Show(briefing);
            var c = Window(briefing, new Vector2(1400, 900), true);
            UIKit.Label(c, $"МИССИЯ {def.code}: {def.title.ToUpperInvariant()}", 50, UIKit.Green).Size(70);
            UIKit.Label(c, def.brief, 30, UIKit.TextCol).Size(120);

            var sb = new StringBuilder("<color=#ffd060>ЦЕЛИ</color>\n");
            int n = 1;
            foreach (var o in def.objectives) sb.Append($"  {n++}. {MissionDef.ObjectiveLabel(o)}\n");
            sb.Append("<color=#ffd060>ДОП. КОНТРАКТЫ</color>\n");
            foreach (var s in def.sides) sb.Append($"  • {s.Label}\n");
            sb.Append("  • Ранг: GHOST — без тревог и устранений; SILENT OPERATOR — без тревог\n");
            sb.Append("<color=#ffd060>ИЗВЕСТНЫЕ УГРОЗЫ</color>\n  " + def.threats);
            UIKit.Label(c, sb.ToString(), 28, UIKit.TextCol).Size(390);
            UIKit.Label(c, "Конусы: жёлтый — патруль, оранжевый — подозрение/поиск, красный — тревога/камера. Голубые ромбы — с этим можно взаимодействовать.", 24, UIKit.Dim).Size(70);

            var row = UIKit.Row(c, "Buttons", 30f);
            row.Size(100);
            UIKit.Btn(row, "НАЗАД", game.ShowMenu, 36);
            UIKit.Btn(row, "НАЧАТЬ", game.BeginPlay, 40);
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
            shootBtn = TouchButton.Create(root, "ui_gun", "Пробел", new Vector2(-170, 190), 210, GameInput.PressShoot);
            interactBtn = TouchButton.Create(root, "ui_hand", "E", new Vector2(-420, 130), 190, GameInput.PressInteract);
            decoyBtn = TouchButton.Create(root, "ui_decoy", "Q", new Vector2(-150, 420), 140, GameInput.PressDecoy);
        }

        public void ShowHUD() => Show(hud);

        public void UpdateHUD()
        {
            var m = game.Mission;
            var p = game.Player;
            if (m == null || p == null) return;

            var sb = new StringBuilder();
            sb.Append($"<color=#ffd060>{m.def.code} · {m.def.title}</color>\n");
            var current = m.Current;
            foreach (var o in m.def.objectives)
            {
                bool done = m.IsDone(o);
                string mark = done ? "■" : "□";
                string col = done ? "#7a8a7e" : o == current ? "#ffffff" : "#9aa89e";
                sb.Append($"<color={col}>{mark} {MissionDef.ObjectiveLabel(o)}</color>\n");
            }
            foreach (var s in m.def.sides)
            {
                bool ok = m.SideDone(s);
                sb.Append($"<size=22><color={(ok ? "#8fe39a" : "#e36a5a")}>{(ok ? "+" : "×")} {s.Label}</color></size>\n");
            }
            objectiveText.text = sb.ToString();

            var hp = new StringBuilder();
            for (int i = 0; i < p.maxHp; i++) hp.Append(i < p.hp ? "■" : "□");
            statusText.text =
                $"<color=#e36a5a>ЗДОРОВЬЕ {hp}</color>\n" +
                $"ПАТРОНЫ {p.ammo}   ПРИМАНКИ {p.decoys}\n" +
                $"РАЗВЕДДАННЫЕ {m.intel}/{m.intelTotal}" + (m.hasKeycard ? "   <color=#ff9a8a>ПРОПУСК</color>" : "");

            timerText.text = UIKit.Time(m.time);

            if (m.alertActive)
            {
                alertText.color = Color.Lerp(UIKit.Red, Color.white, Mathf.PingPong(Time.time * 3f, 1f) * 0.4f);
                alertText.text = $"! ТРЕВОГА  {Mathf.CeilToInt(m.alertTimer)}";
            }
            else if (!game.Level.PowerOn)
            {
                alertText.color = UIKit.Amber;
                alertText.text = $"ПИТАНИЕ ОТКЛ.  {Mathf.CeilToInt(game.Level.PowerTimeLeft)}";
            }
            else alertText.text = "";

            float dt = Time.unscaledDeltaTime;
            bannerTime -= dt;
            bannerText.enabled = bannerTime > 0f;
            if (bannerTime < 0.5f) bannerText.color = new Color(bannerText.color.r, bannerText.color.g, bannerText.color.b, Mathf.Clamp01(bannerTime * 2f));
            toastTime -= dt;
            toastText.enabled = toastTime > 0f;
            if (toastTime < 0.5f) toastText.color = new Color(UIKit.Green.r, UIKit.Green.g, UIKit.Green.b, Mathf.Clamp01(toastTime * 2f));

            var it = p.CurrentInteractable;
            interactBtn.SetActive(it != null);
            interactBtn.caption.text = it != null ? it.Prompt : "";
            shootBtn.SetActive(p.ammo > 0);
            shootBtn.caption.text = p.Target != null ? "<color=#ff6a5a>ЦЕЛЬ</color>" : "";
            decoyBtn.SetActive(p.decoys > 0);
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
            toastTime = 2.5f;
            toastText.enabled = true;
        }

        // ------------------------------------------------------------------ pause / results

        public void ShowPause()
        {
            Show(pause);
            var c = Window(pause, new Vector2(900, 640), true);
            UIKit.Label(c, "ПАУЗА", 60, UIKit.Green, TextAnchor.MiddleCenter).Size(90);
            UIKit.Btn(c, "Продолжить", game.Resume, 38).Size(100);
            UIKit.Btn(c, "Начать заново", game.Restart, 38).Size(100);
            UIKit.Btn(c, "Выйти в меню", game.ShowMenu, 38).Size(100);
            UIKit.Label(c, ControlsHint, 22, UIKit.Dim, TextAnchor.MiddleCenter).Size(80);
        }

        public void ShowResult(MissionState m, int missionIndex, int credits)
        {
            Show(result);
            progressRoot.gameObject.SetActive(false);
            var c = Window(result, new Vector2(1300, 940), true);
            UIKit.Label(c, "МИССИЯ ВЫПОЛНЕНА", 54, UIKit.Green, TextAnchor.MiddleCenter).Size(70);

            var rank = m.Rank;
            Color rc = rank == Rank.Ghost ? UIKit.Green : rank == Rank.SilentOperator ? UIKit.Amber : UIKit.Red;
            var rl = UIKit.Label(c, "РАНГ: " + MissionState.RankName(rank), 72, rc, TextAnchor.MiddleCenter);
            rl.fontStyle = FontStyle.Bold;
            rl.Size(100);

            var sb = new StringBuilder();
            sb.Append($"Время: {UIKit.Time(m.time)}      Тревоги: {m.alerts}      Устранено: {m.kills}\n");
            sb.Append($"Разведданные: {m.intel}/{m.intelTotal}      Кредиты: <color=#ffd060>+{credits}</color>\n");
            if (m.alerts > 0 && m.lastAlertReason != null) sb.Append($"<color=#9aa89e>Что тебя выдало: {m.lastAlertReason.ToLowerInvariant()}</color>\n");
            sb.Append("\n<color=#ffd060>ДОП. КОНТРАКТЫ</color>\n");
            foreach (var s in m.def.sides)
            {
                bool ok = m.SideDone(s);
                sb.Append($"<color={(ok ? "#8fe39a" : "#e36a5a")}>{(ok ? "■ выполнено" : "□ не выполнено")}</color> — {s.Label}\n");
            }
            sb.Append("\n<color=#ffd060>ДО СЛЕДУЮЩЕЙ НАГРАДЫ</color>\n");
            if (rank == Rank.Compromised) sb.Append("Пройди без тревог, чтобы получить SILENT OPERATOR.");
            else if (rank == Rank.SilentOperator) sb.Append("Пройди без устранений, чтобы получить GHOST.");
            else sb.Append("Высший ранг получен. Собери всё и уложись быстрее!");
            UIKit.Label(c, sb.ToString(), 30, UIKit.TextCol, TextAnchor.UpperCenter).Size(470);

            var row = UIKit.Row(c, "Buttons", 24f);
            row.Size(100);
            UIKit.Btn(row, "В МЕНЮ", game.ShowMenu, 34);
            UIKit.Btn(row, "ПОВТОРИТЬ", game.Restart, 34);
            if (missionIndex + 1 < MissionDef.All.Length)
                UIKit.Btn(row, "СЛЕДУЮЩАЯ", () => game.StartMission(missionIndex + 1), 34);
        }

        public void ShowFail(MissionState m)
        {
            Show(result);
            progressRoot.gameObject.SetActive(false);
            var c = Window(result, new Vector2(1000, 560), true);
            UIKit.Label(c, "МИССИЯ ПРОВАЛЕНА", 60, UIKit.Red, TextAnchor.MiddleCenter).Size(90);
            string why = m.lastAlertReason != null ? $"Что тебя выдало: {m.lastAlertReason.ToLowerInvariant()}." : "";
            UIKit.Label(c, "Оперативник нейтрализован.\n" + why + "\nПопробуй обойти патруль, отвлечь охрану приманкой или отключить питание.",
                30, UIKit.TextCol, TextAnchor.MiddleCenter).Size(200);
            var row = UIKit.Row(c, "Buttons", 24f);
            row.Size(100);
            UIKit.Btn(row, "В МЕНЮ", game.ShowMenu, 36);
            UIKit.Btn(row, "ПОВТОРИТЬ", game.Restart, 36);
        }
    }
}
