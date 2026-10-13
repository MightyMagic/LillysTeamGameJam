using UnityEngine;

namespace NightShift
{
    public enum Upgrade { Magazine, Decoy, Armor }

    /// Meta progression stored in PlayerPrefs: unlocked missions, best ranks, credits, upgrades.
    public static class Progress
    {
        public static int Credits
        {
            get => PlayerPrefs.GetInt("ns_credits", 0);
            set { PlayerPrefs.SetInt("ns_credits", value); PlayerPrefs.Save(); }
        }

        public static bool IsUnlocked(int mission) => mission == 0 || PlayerPrefs.GetInt("ns_done_" + (mission - 1), 0) == 1;

        /// -1 = never completed.
        public static int BestRank(int mission) => PlayerPrefs.GetInt("ns_rank_" + mission, -1);

        public static void Complete(int mission, Rank rank, int credits)
        {
            PlayerPrefs.SetInt("ns_done_" + mission, 1);
            if ((int)rank > BestRank(mission)) PlayerPrefs.SetInt("ns_rank_" + mission, (int)rank);
            Credits += credits;
        }

        public static bool Has(Upgrade u) => PlayerPrefs.GetInt("ns_up_" + u, 0) == 1;

        public static int Cost(Upgrade u) => u == Upgrade.Armor ? 250 : 150;

        public static string Name(Upgrade u) =>
            u == Upgrade.Magazine ? "Увеличенный магазин (+3 патрона)" :
            u == Upgrade.Decoy ? "Доп. шумовая приманка (+1)" : "Бронежилет (+1 здоровье)";

        public static bool Buy(Upgrade u)
        {
            if (Has(u) || Credits < Cost(u)) return false;
            Credits -= Cost(u);
            PlayerPrefs.SetInt("ns_up_" + u, 1);
            PlayerPrefs.Save();
            return true;
        }

        public static void ResetAll()
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
        }
    }
}
