using UnityEngine;

namespace NightShift
{
    /// Runtime state of one mission attempt: objectives, alarms, loot, stats.
    public class MissionState
    {
        public readonly LevelDef def;
        public float time;
        public int alerts, kills, intel, intelTotal;
        public bool hasKeycard, hasPrototype, dataDownloaded, powerWasCut, extracted;
        public int trapsUsed, hides;
        public string scriptObjective; // objective text set by a level script (tutorial)

        public bool alertActive;
        public float alertTimer;
        public Vector2 lastKnownPos;
        public string lastAlertReason;

        public MissionState(LevelDef def) { this.def = def; }

        public bool IsDone(ObjectiveKind k)
        {
            switch (k)
            {
                case ObjectiveKind.GetKeycard: return hasKeycard;
                case ObjectiveKind.CutPower: return powerWasCut;
                case ObjectiveKind.DownloadData: return dataDownloaded;
                case ObjectiveKind.GetPrototype: return hasPrototype;
                case ObjectiveKind.Extract: return extracted;
            }
            return false;
        }

        /// First unfinished objective (objectives are sequential).
        public ObjectiveKind Current
        {
            get
            {
                foreach (var o in def.objectives)
                    if (!IsDone(o)) return o;
                return ObjectiveKind.Extract;
            }
        }

        public bool ReadyToExtract
        {
            get
            {
                foreach (var o in def.objectives)
                    if (o != ObjectiveKind.Extract && !IsDone(o)) return false;
                return true;
            }
        }

        public bool SideDone(SideTask s)
        {
            switch (s.kind)
            {
                case SideKind.NoKills: return kills == 0;
                case SideKind.NoAlerts: return alerts == 0;
                case SideKind.AllIntel: return intel >= intelTotal;
                case SideKind.IntelAtLeast: return intel >= s.param;
                case SideKind.TimeLimit: return time <= s.param;
            }
            return false;
        }

        public Rank Rank => alerts > 0 ? Rank.Compromised : kills > 0 ? Rank.SilentOperator : Rank.Ghost;

        public static string RankName(Rank r) =>
            Loc.T(r == Rank.Ghost ? "rank.ghost" : r == Rank.SilentOperator ? "rank.silent" : "rank.compromised");

        public int Credits()
        {
            int c = 100 + intel * 40;
            if (Rank == Rank.Ghost) c += 150;
            else if (Rank == Rank.SilentOperator) c += 75;
            foreach (var s in def.sides) if (SideDone(s)) c += 50;
            return c;
        }
    }
}
