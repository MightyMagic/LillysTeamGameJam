using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace MobileGearSolid
{
    /// Supply chest: open it to pick 1 of 3 run cards (roguelite layer). Map symbol '$'.
    public class Chest : Interactable
    {
        SpriteRenderer sr;
        Light2D glow;
        bool opened;

        public void Init(Vector2 pos)
        {
            Pos = pos;
            range = 1.4f;
            transform.position = pos;
            sr = Art.MakeSprite(transform, "Chest", "prop_chest", new Vector3(0, -0.48f), Art.SortY(pos.y - 0.5f));
            sr.transform.localPosition = new Vector3(0, -0.48f);
            var col = sr.gameObject.AddComponent<BoxCollider2D>();
            col.size = new Vector2(0.9f, 0.6f);
            col.offset = new Vector2(0, 0.35f);
            glow = Level.MakeLight(transform, pos, new Color(0.4f, 0.95f, 1f), 0.9f, 2.2f);
            SetupMarker(1.0f, new Color(0.5f, 1f, 1f));
        }

        public override bool Available => !opened;
        public override string Prompt => Loc.T("prompt.chest");

        public override void Interact(Player p)
        {
            if (opened) return;
            opened = true;
            sr.sprite = Art.S("prop_chest_open");
            glow.color = new Color(1f, 0.9f, 0.5f);
            glow.intensity = 0.5f;
            Sfx.Play("pickup");
            Game.I.OfferCards();
        }

        protected override void Update()
        {
            base.Update();
            if (!opened) glow.intensity = 0.7f + Mathf.Sin(Time.time * 3f) * 0.25f;
        }
    }
}
