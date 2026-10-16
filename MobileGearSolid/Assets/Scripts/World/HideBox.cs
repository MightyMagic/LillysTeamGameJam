using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace NightShift
{
    /// The legendary cardboard box. Climb in with the action button: cameras ignore the box,
    /// guards don't notice it while it stands still, and only grow suspicious when it moves.
    public class HideBox : Interactable
    {
        SpriteRenderer sr;
        Light2D glow;
        public bool Worn { get; private set; }
        Vector2 home;

        public void Init(Vector2 pos)
        {
            Pos = home = pos;
            range = 1.45f;
            transform.position = pos;
            sr = Art.MakeSprite(transform, "Box", "prop_hidebox", new Vector3(0, -0.45f), Art.SortY(pos.y - 0.5f));
            sr.transform.localPosition = new Vector3(0, -0.45f);
            // a faint warm glow so the box reads as interactive, not as decor
            glow = Level.MakeLight(transform, pos, new Color(1f, 0.8f, 0.45f), 0.6f, 1.6f);
            SetupMarker(0.85f, new Color(1f, 0.85f, 0.45f));
        }

        /// Put the box back where the level placed it (tutorial checkpoints).
        public void ResetHome() => SetWorn(false, home);

        public override bool Available => !Worn;
        public override string Prompt => Loc.T("prompt.box");

        public override void Interact(Player p)
        {
            if (Worn) return;
            p.EnterBox(this);
        }

        public void SetWorn(bool worn, Vector2 pos)
        {
            Worn = worn;
            sr.enabled = !worn;
            glow.enabled = !worn;
            if (worn) return;
            Pos = pos;
            transform.position = pos;
            sr.sortingOrder = Art.SortY(pos.y - 0.5f);
        }
    }
}
