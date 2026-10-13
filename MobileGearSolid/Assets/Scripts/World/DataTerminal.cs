using UnityEngine;

namespace NightShift
{
    /// Server terminal: data can only be pulled while the facility is on emergency power.
    /// The download continues while the player stays close.
    public class DataTerminal : Interactable
    {
        public const float DownloadTime = 5f;
        bool required, downloading;
        public float progress;
        SpriteRenderer sr;

        public void Init(int x, int y, bool isObjective)
        {
            required = isObjective;
            Pos = Level.Center(x, y);
            sr = Art.MakeSprite(transform, "Terminal", isObjective ? "prop_terminal_off" : "prop_terminal_on",
                new Vector3(x + 0.5f, y + 0.02f), Art.SortY(y));
            var col = sr.gameObject.AddComponent<BoxCollider2D>();
            col.size = new Vector2(0.92f, 0.84f);
            col.offset = new Vector2(0, 0.46f);
            if (required) SetupMarker(1.75f, new Color(0.5f, 1f, 0.6f));
        }

        public override bool Available => required && !Game.I.Mission.dataDownloaded && !downloading;

        public override string Prompt => Game.I.Level.PowerOn ? "Терминал (защита)" : "Скачать данные";

        public override void Interact(Player p)
        {
            if (Game.I.Level.PowerOn)
            {
                Game.I.Toast("Защита активна: сначала отключи питание");
                Sfx.Play("empty");
                return;
            }
            downloading = true;
            Sfx.Play("beep");
        }

        protected override void Update()
        {
            base.Update();
            if (!required) return;
            bool powerOff = !Game.I.Level.PowerOn;
            sr.sprite = Art.S(powerOff || Game.I.Mission.dataDownloaded ? "prop_terminal_on" : "prop_terminal_off");
            if (!downloading || Game.I.phase != Game.Phase.Playing) return;

            var p = Game.I.Player;
            bool near = p != null && Vector2.Distance(p.Pos, Pos) < range + 0.6f;
            if (near && powerOff)
            {
                progress += Time.deltaTime / DownloadTime;
                if (progress >= 1f)
                {
                    progress = 1f;
                    downloading = false;
                    Game.I.Mission.dataDownloaded = true;
                    Game.I.Toast("Данные скачаны!");
                    Sfx.Play("success", 0.7f);
                }
            }
            if (!powerOff && downloading)
            {
                downloading = false;
                Game.I.Toast("Питание вернули — загрузка прервана");
            }
            Game.I.UI.SetProgress(downloading ? progress : -1f, near ? "Загрузка данных..." : "Вернись к терминалу!");
        }
    }
}
