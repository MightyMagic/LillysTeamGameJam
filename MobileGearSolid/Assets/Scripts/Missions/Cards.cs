using System.Collections.Generic;
using UnityEngine;

namespace MobileGearSolid
{
    public enum CardId { ApRounds, ExtraMag, Vitality, SoftSoles, Sprint, Suppressor, BoxMaster, DoubleTrap, Jammer, SecondChance }

    public class CardDef
    {
        public CardId id;
        public string icon;
        public Color tint;
        public bool unique;      // can be taken only once per run
        public string Key => "card." + id.ToString().ToLowerInvariant();
        public string Name => Loc.T(Key);
        public string Desc => Loc.T(Key + ".desc");
    }

    /// The 10 run cards found in chests (roguelite layer). Effects are read by Game.ApplyStats.
    public static class Cards
    {
        public static readonly CardDef[] All =
        {
            new CardDef { id = CardId.ApRounds, icon = "icon_ap", tint = new Color(1f, 0.55f, 0.45f) },
            new CardDef { id = CardId.ExtraMag, icon = "ui_ammo", tint = new Color(1f, 0.85f, 0.45f) },
            new CardDef { id = CardId.Vitality, icon = "ui_heart", tint = new Color(1f, 0.45f, 0.45f) },
            new CardDef { id = CardId.SoftSoles, icon = "icon_boot", tint = new Color(0.6f, 0.9f, 0.6f) },
            new CardDef { id = CardId.Sprint, icon = "icon_bolt", tint = new Color(1f, 0.9f, 0.4f) },
            new CardDef { id = CardId.Suppressor, icon = "icon_silencer", tint = new Color(0.7f, 0.8f, 1f), unique = true },
            new CardDef { id = CardId.BoxMaster, icon = "prop_hidebox", tint = new Color(1f, 0.8f, 0.5f), unique = true },
            new CardDef { id = CardId.DoubleTrap, icon = "item_trap", tint = new Color(0.8f, 0.7f, 1f) },
            new CardDef { id = CardId.Jammer, icon = "icon_jammer", tint = new Color(1f, 0.5f, 0.5f), unique = true },
            new CardDef { id = CardId.SecondChance, icon = "icon_shield", tint = new Color(0.5f, 0.75f, 1f) },
        };

        public static CardDef Get(CardId id) => All[(int)id];

        /// Three different random cards, skipping unique ones the run already has.
        public static List<CardDef> Roll(List<CardId> owned, int count = 3)
        {
            var pool = new List<CardDef>();
            foreach (var c in All)
                if (!(c.unique && owned.Contains(c.id))) pool.Add(c);
            var result = new List<CardDef>();
            while (result.Count < count && pool.Count > 0)
            {
                int i = Random.Range(0, pool.Count);
                result.Add(pool[i]);
                pool.RemoveAt(i);
            }
            return result;
        }

        public static int Count(List<CardId> cards, CardId id)
        {
            int n = 0;
            foreach (var c in cards) if (c == id) n++;
            return n;
        }
    }
}
