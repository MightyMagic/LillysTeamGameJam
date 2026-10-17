using UnityEngine;

namespace MobileGearSolid
{
    public enum Stat { Health, Damage, Ammo, Speed, Stealth, Traps }

    /// Permanent character upgrades bought on the ship (Archero-style levels). Stored in PlayerPrefs.
    /// Texts: stat.&lt;name&gt; / stat.&lt;name&gt;.desc in texts.txt.
    public static class Meta
    {
        // base values of a fresh character
        public const int BaseHp = 2, BaseDamage = 1, BaseAmmo = 4, BaseTraps = 1;
        /// Guards take this many pistol hits at base damage.
        public const int GuardHp = 2;

        static readonly int[] MaxLevels = { 3, 2, 3, 3, 3, 2 };
        static readonly int[][] Costs =
        {
            new[] { 80, 180, 320 },   // Health
            new[] { 150, 350 },       // Damage
            new[] { 60, 140, 260 },   // Ammo
            new[] { 70, 160, 280 },   // Speed
            new[] { 90, 200, 340 },   // Stealth
            new[] { 100, 240 },       // Traps
        };

        static string Key(Stat s) => "ns_stat_" + s;
        public static int Level(Stat s) => PlayerPrefs.GetInt(Key(s), 0);
        public static int MaxLevel(Stat s) => MaxLevels[(int)s];
        public static bool IsMax(Stat s) => Level(s) >= MaxLevel(s);
        public static int Cost(Stat s) => IsMax(s) ? 0 : Costs[(int)s][Level(s)];

        public static bool Buy(Stat s)
        {
            if (IsMax(s) || Progress.Credits < Cost(s)) return false;
            Progress.Credits -= Cost(s);
            PlayerPrefs.SetInt(Key(s), Level(s) + 1);
            PlayerPrefs.Save();
            return true;
        }

        // ------------------------------------------------------------------ resulting values
        public static int MaxHp => BaseHp + Level(Stat.Health);
        public static int Damage => BaseDamage + Level(Stat.Damage);
        public static int Ammo => BaseAmmo + 2 * Level(Stat.Ammo);
        public static float SpeedMul => 1f + 0.06f * Level(Stat.Speed);
        /// Multiplier on how fast guards fill their suspicion meter.
        public static float StealthMul => 1f - 0.1f * Level(Stat.Stealth);
        public static int Traps => BaseTraps + Level(Stat.Traps);

        /// Current value as shown on the ship screen.
        public static string ValueText(Stat s, int level)
        {
            switch (s)
            {
                case Stat.Health: return (BaseHp + level).ToString();
                case Stat.Damage: return (BaseDamage + level).ToString();
                case Stat.Ammo: return (BaseAmmo + 2 * level).ToString();
                case Stat.Speed: return level == 0 ? Loc.T("ship.base") : $"+{6 * level}%";
                case Stat.Stealth: return level == 0 ? Loc.T("ship.base") : $"-{10 * level}%";
                default: return (BaseTraps + level).ToString();
            }
        }

        public static string Icon(Stat s) =>
            s == Stat.Health ? "ui_heart" : s == Stat.Damage ? "icon_ap" : s == Stat.Ammo ? "ui_ammo"
            : s == Stat.Speed ? "icon_bolt" : s == Stat.Stealth ? "icon_boot" : "item_trap";

        public static string Name(Stat s) => Loc.T("stat." + s.ToString().ToLowerInvariant());
        public static string Desc(Stat s) => Loc.T("stat." + s.ToString().ToLowerInvariant() + ".desc");

        public static void ResetAll()
        {
            foreach (Stat s in System.Enum.GetValues(typeof(Stat))) PlayerPrefs.DeleteKey(Key(s));
        }
    }
}
