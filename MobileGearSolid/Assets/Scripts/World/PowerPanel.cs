using UnityEngine;

namespace MobileGearSolid
{
    /// Sabotage target (GDD §4D): cutting power kills lamps and cameras for a while,
    /// but a guard is sent to check the panel and switch it back on.
    public class PowerPanel : Interactable
    {
        Level level;
        SpriteRenderer sr;
        public Vector2 ServicePoint; // where a guard stands to repair it

        public void Init(Level lvl, int x, int y)
        {
            level = lvl;
            Pos = Level.Center(x, y);
            ServicePoint = Pos + Vector2.down;
            sr = Art.MakeSprite(transform, "Panel", "prop_panel_on", new Vector3(x + 0.5f, y + 0.02f), Art.SortY(y));
            var col = sr.gameObject.AddComponent<BoxCollider2D>();
            col.size = new Vector2(0.92f, 0.84f);
            col.offset = new Vector2(0, 0.46f);
            SetupMarker(1.9f, new Color(1f, 0.85f, 0.3f));
        }

        public override bool Available => level.PowerOn;
        public override string Prompt => Loc.T("prompt.panel");

        public override void Interact(Player p)
        {
            if (!level.PowerOn) return;
            level.CutPower();
        }

        protected override void Update()
        {
            base.Update();
            sr.sprite = Art.S(level.PowerOn ? "prop_panel_on" : "prop_panel_off");
        }
    }
}
