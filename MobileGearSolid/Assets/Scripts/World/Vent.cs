using UnityEngine;

namespace MobileGearSolid
{
    /// One end of a ventilation shaft. Crawling to the linked vent is silent and invisible:
    /// a way to slip between neighbouring rooms unseen. Pairs come from the level's [vents] section.
    public class Vent : Interactable
    {
        public Vent partner;
        SpriteRenderer sr;

        public static void Link(Transform parent, Vector2 a, Vector2 b)
        {
            var va = Create(parent, a);
            var vb = Create(parent, b);
            va.partner = vb;
            vb.partner = va;
        }

        static Vent Create(Transform parent, Vector2 pos)
        {
            var v = new GameObject("Vent").AddComponent<Vent>();
            v.transform.SetParent(parent, false);
            v.transform.position = pos;
            v.Pos = pos;
            v.range = 1.3f;
            v.sr = Art.MakeSprite(v.transform, "Grate", "prop_vent", Vector3.zero, Art.OrderFloorItems + 5);
            v.sr.transform.localPosition = new Vector3(0, -0.5f);
            v.SetupMarker(0.7f, new Color(0.6f, 0.9f, 1f));
            return v;
        }

        public override bool Available => partner != null;
        public override string Prompt => Loc.T("prompt.vent");

        public override void Interact(Player p)
        {
            if (partner == null) return;
            p.EnterVent(this, partner);
        }
    }
}
