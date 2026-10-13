namespace NightShift
{
    public enum ObjectiveKind { GetKeycard, CutPower, DownloadData, GetPrototype, Extract }
    public enum SideKind { NoKills, NoAlerts, AllIntel, IntelAtLeast, TimeLimit }
    public enum Rank { Compromised, SilentOperator, Ghost }

    public class SideTask
    {
        public SideKind kind;
        public float param;

        public SideTask(SideKind kind, float param = 0) { this.kind = kind; this.param = param; }

        public string Label
        {
            get
            {
                switch (kind)
                {
                    case SideKind.NoKills: return "Никого не устранять";
                    case SideKind.NoAlerts: return "Ни одной тревоги";
                    case SideKind.AllIntel: return "Собрать все разведданные";
                    case SideKind.IntelAtLeast: return $"Собрать разведданные: {param:0} шт.";
                    case SideKind.TimeLimit: return $"Уложиться в {(int)param / 60}:{(int)param % 60:00}";
                }
                return kind.ToString();
            }
        }
    }

    /// Static mission data. All three missions run on the same facility map (GDD §7).
    public class MissionDef
    {
        public string code, title, brief, threats;
        public ObjectiveKind[] objectives;
        public SideTask[] sides;
        public string[] routes; // each string = sequence of waypoint markers from facility.txt
        public bool keycard, terminal, prototype;

        public static string ObjectiveLabel(ObjectiveKind k)
        {
            switch (k)
            {
                case ObjectiveKind.GetKeycard: return "Найти пропуск";
                case ObjectiveKind.CutPower: return "Отключить питание (электрощитовая)";
                case ObjectiveKind.DownloadData: return "Скачать данные в серверной";
                case ObjectiveKind.GetPrototype: return "Забрать прототип из хранилища";
                case ObjectiveKind.Extract: return "Эвакуироваться через выход";
            }
            return k.ToString();
        }

        public static readonly MissionDef[] All =
        {
            new MissionDef
            {
                code = "01", title = "Войти незамеченным",
                brief = "Ты внутри — через вентиляцию склада. Найди пропуск персонала и доберись до выхода " +
                        "в северо-восточном крыле. Камера над стеллажами следит за пропуском: изучи её сектор, прежде чем лезть.",
                threats = "Патруль склада, патруль коридора, охранник у выхода, 4 камеры.",
                objectives = new[] { ObjectiveKind.GetKeycard, ObjectiveKind.Extract },
                sides = new[] { new SideTask(SideKind.IntelAtLeast, 2), new SideTask(SideKind.NoKills) },
                routes = new[] { "6789", "01", "AB" },
                keycard = true,
            },
            new MissionDef
            {
                code = "02", title = "Чёрный ящик",
                brief = "Сервер защищён, пока на объекте есть питание. Отключи электрощит, проберись в серверную " +
                        "и скачай данные, пока свет не вернули. Охрана отправит человека проверить щит.",
                threats = "Пять патрулей, камеры (отключаются вместе с питанием).",
                objectives = new[] { ObjectiveKind.CutPower, ObjectiveKind.DownloadData, ObjectiveKind.Extract },
                sides = new[] { new SideTask(SideKind.TimeLimit, 240), new SideTask(SideKind.NoAlerts) },
                routes = new[] { "6789", "01", "23", "45", "AB" },
                terminal = true,
            },
            new MissionDef
            {
                code = "03", title = "Ночной визит",
                brief = "В хранилище арсенала заперт экспериментальный прототип. Добудь пропуск, вскрой хранилище " +
                        "и эвакуируйся. Охрана усилена — используй всё, чему научился.",
                threats = "Семь патрулей, камеры, запертое хранилище.",
                objectives = new[] { ObjectiveKind.GetKeycard, ObjectiveKind.GetPrototype, ObjectiveKind.Extract },
                sides = new[] { new SideTask(SideKind.AllIntel), new SideTask(SideKind.NoKills) },
                routes = new[] { "6789", "01", "23", "45", "AB", "FH", "IJ" },
                keycard = true, prototype = true,
            },
        };
    }
}
