using UnityEngine;

namespace NightShift
{
    public enum PickupKind { Keycard, Intel, Ammo, Medkit, Decoy, Prototype }

    /// Mission items and intel need the interact button; consumables are picked up by walking over them.
    public class Pickup : Interactable
    {
        public PickupKind kind;
        SpriteRenderer sr;
        bool taken;

        public void Init(PickupKind k, Vector2 pos)
        {
            kind = k;
            Pos = pos;
            autoPickup = k == PickupKind.Ammo || k == PickupKind.Medkit || k == PickupKind.Decoy;

            if (k == PickupKind.Prototype)
            {
                sr = Art.MakeSprite(transform, "Case", "prop_prototype", new Vector3(pos.x, pos.y - 0.48f), Art.SortY(pos.y - 0.5f));
                var col = sr.gameObject.AddComponent<BoxCollider2D>();
                col.size = new Vector2(0.92f, 0.84f);
                col.offset = new Vector2(0, 0.46f);
                SetupMarker(1.25f, new Color(0.5f, 1f, 1f));
                return;
            }

            string spr = k == PickupKind.Keycard ? "item_keycard" : k == PickupKind.Intel ? "item_intel"
                       : k == PickupKind.Ammo ? "item_ammo" : k == PickupKind.Medkit ? "item_medkit" : "item_decoy";
            sr = Art.MakeSprite(transform, "Item", spr, pos, Art.OrderFloorItems);
            if (!autoPickup) SetupMarker(0.55f, k == PickupKind.Keycard ? new Color(1f, 0.6f, 0.5f) : new Color(0.5f, 1f, 1f));
        }

        public override bool Available => !taken;

        public override string Prompt
        {
            get
            {
                switch (kind)
                {
                    case PickupKind.Keycard: return "Взять пропуск";
                    case PickupKind.Intel: return "Забрать документы";
                    case PickupKind.Prototype: return "Забрать прототип";
                    default: return "Подобрать";
                }
            }
        }

        public override void Interact(Player p)
        {
            if (taken) return;
            var m = Game.I.Mission;
            switch (kind)
            {
                case PickupKind.Keycard:
                    m.hasKeycard = true;
                    Game.I.Toast("Пропуск получен");
                    break;
                case PickupKind.Intel:
                    m.intel++;
                    Game.I.Toast($"Разведданные {m.intel}/{m.intelTotal}");
                    break;
                case PickupKind.Prototype:
                    m.hasPrototype = true;
                    Game.I.Toast("Прототип у тебя. Уходи!");
                    break;
                case PickupKind.Ammo:
                    p.ammo += 4;
                    Game.I.Toast("+4 патрона");
                    break;
                case PickupKind.Medkit:
                    if (p.hp >= p.maxHp) return; // leave it for later
                    p.hp = Mathf.Min(p.maxHp, p.hp + 2);
                    Game.I.Toast("Аптечка: +2 здоровья");
                    break;
                case PickupKind.Decoy:
                    p.decoys++;
                    Game.I.Toast("+1 шумовая приманка");
                    break;
            }
            taken = true;
            Sfx.Play("pickup");
            if (kind == PickupKind.Prototype) sr.color = new Color(0.35f, 0.35f, 0.35f);
            else gameObject.SetActive(false);
        }
    }
}
