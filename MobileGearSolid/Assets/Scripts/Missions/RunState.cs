using System.Collections.Generic;
using UnityEngine;

namespace MobileGearSolid
{
    /// One "Night Raid" run (roguelite mode): a chain of hand-made rooms from Resources/Levels/raid,
    /// picked by difficulty tier. Health, ammo, traps and cards carry over between rooms.
    public class RunState
    {
        public const int RoomCount = 5;
        // starts at tier 2: the easiest rooms were not fun as an opening (tier-1 rooms stay for future modes)
        static readonly int[] TierPlan = { 2, 2, 3, 3, 3 };
        /// Strong builds get tougher rooms: from this many cards on, the next room is one tier higher.
        const int StrongBuildCards = 4;

        public int roomIndex = -1;             // index of the current room (0-based)
        public string currentRoom;
        public int currentTier;
        public readonly List<string> visited = new List<string>();
        public readonly List<CardId> cards = new List<CardId>();

        // carried between rooms
        public int hp = -1, ammo, traps, secondChance;

        // totals for the result screen
        public int kills, alerts, intel, credits, chests;
        public float time;
        public bool finished, won, retreated;

        public bool IsLastRoom => roomIndex >= RoomCount - 1;

        public struct RoomInfo { public string id; public int tier; }

        public static List<RoomInfo> Pool()
        {
            var list = new List<RoomInfo>();
            foreach (var t in Resources.LoadAll<TextAsset>("Levels/raid"))
            {
                var def = LevelDef.Parse("raid/" + t.name, t.text);
                list.Add(new RoomInfo { id = def.id, tier = Mathf.Clamp(def.tier, 1, 3) });
            }
            return list;
        }

        /// Picks the next room: planned tier (+1 for strong builds), never repeating a room in one run.
        public string NextRoom()
        {
            roomIndex++;
            int tier = TierPlan[Mathf.Min(roomIndex, TierPlan.Length - 1)];
            if (roomIndex > 0 && cards.Count >= StrongBuildCards) tier++;
            tier = Mathf.Clamp(tier, 1, 3);

            var pool = Pool();
            var candidates = pool.FindAll(r => r.tier == tier && !visited.Contains(r.id));
            if (candidates.Count == 0)
            {
                // out of rooms of that tier: take the closest tier, preferring the harder one
                var rest = pool.FindAll(r => !visited.Contains(r.id));
                rest.Sort((x, y) =>
                {
                    int dx = Mathf.Abs(x.tier - tier), dy = Mathf.Abs(y.tier - tier);
                    return dx != dy ? dx.CompareTo(dy) : y.tier.CompareTo(x.tier);
                });
                if (rest.Count > 0) candidates = rest.FindAll(r => r.tier == rest[0].tier);
            }
            if (candidates.Count == 0) candidates = pool;
            if (candidates.Count == 0) return null;

            var pick = candidates[Random.Range(0, candidates.Count)];
            currentRoom = pick.id;
            currentTier = pick.tier;
            visited.Add(pick.id);
            return pick.id;
        }

        /// Credits for clearing a room: more for harder rooms and clean play.
        public static int RoomReward(int tier, bool noAlerts) => 30 + 20 * tier + (noAlerts ? 25 : 0);
    }
}
