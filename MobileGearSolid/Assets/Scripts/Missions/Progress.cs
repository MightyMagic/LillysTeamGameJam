using System.Collections.Generic;
using UnityEngine;

namespace MobileGearSolid
{
    public enum Upgrade { Magazine, Decoy, Armor }

    /// Meta progression stored in PlayerPrefs: completed levels, best ranks, credits, upgrades.
    /// Levels are identified by their id (file name), so reordering campaign.txt keeps saves valid.
    public static class Progress
    {
        public static int Credits
        {
            get => PlayerPrefs.GetInt("ns_credits", 0);
            set { PlayerPrefs.SetInt("ns_credits", value); PlayerPrefs.Save(); }
        }

        public static bool IsDone(string id) => PlayerPrefs.GetInt("ns_done_" + id, 0) == 1;

        /// A level is open when every level before it in the campaign is done.
        public static bool IsUnlocked(List<string> campaign, int index) => index == 0 || IsDone(campaign[index - 1]);

        /// -1 = never completed.
        public static int BestRank(string id) => PlayerPrefs.GetInt("ns_rank_" + id, -1);

        public static void Complete(string id, Rank rank, int credits)
        {
            PlayerPrefs.SetInt("ns_done_" + id, 1);
            if ((int)rank > BestRank(id)) PlayerPrefs.SetInt("ns_rank_" + id, (int)rank);
            Credits += credits;
        }

        public static void MarkDone(string id)
        {
            PlayerPrefs.SetInt("ns_done_" + id, 1);
            PlayerPrefs.Save();
        }

        public static bool Has(Upgrade u) => PlayerPrefs.GetInt("ns_up_" + u, 0) == 1;

        public static int Cost(Upgrade u) => u == Upgrade.Armor ? 250 : 150;

        public static string Name(Upgrade u) =>
            Loc.T(u == Upgrade.Magazine ? "upgrade.magazine" : u == Upgrade.Decoy ? "upgrade.decoy" : "upgrade.armor");

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
