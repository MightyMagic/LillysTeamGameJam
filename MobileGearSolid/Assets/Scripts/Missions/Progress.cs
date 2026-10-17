using System.Collections.Generic;
using UnityEngine;

namespace MobileGearSolid
{
    /// Progress stored in PlayerPrefs: completed levels, best ranks, credits, best raid (stats live in Meta).
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

        /// Most rooms cleared in a single Night Raid.
        public static int BestRaid
        {
            get => PlayerPrefs.GetInt("ns_raid_best", 0);
            set { PlayerPrefs.SetInt("ns_raid_best", value); PlayerPrefs.Save(); }
        }

        public static void ResetAll()
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
        }
    }
}
