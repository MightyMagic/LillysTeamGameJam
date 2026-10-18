using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;

namespace MobileGearSolid
{
    /// Entry point. Lives in the Game scene and drives menu -> briefing -> mission -> result.
    /// Missions come from Resources/Levels (campaign.txt lists the order), texts from Resources/Texts.
    public class Game : MonoBehaviour
    {
        public enum Phase { Menu, Briefing, Playing, Dialog, CardPick, Paused, Result }

        public static Game I;
        public Phase phase;

        public Level Level { get; private set; }
        public Player Player { get; private set; }
        public MissionState Mission { get; private set; }
        public LevelDef Def { get; private set; }
        public int MissionIndex { get; private set; }
        public List<string> Campaign { get; private set; }
        public GameUI UI { get; private set; }
        public ScriptRunner Script { get; private set; }
        /// Current Night Raid, or null in story missions.
        public RunState Run { get; private set; }

        public readonly List<Guard> Guards = new List<Guard>();
        public readonly List<SecurityCamera> Cameras = new List<SecurityCamera>();
        public readonly List<Interactable> Interactables = new List<Interactable>();

        public const float AlertDuration = 18f;
        const float PanelDispatchDelay = 10f;

        public string CameraAreaId => roomCamera != null ? roomCamera.AreaId : null;
        public bool RoomCameraActive => roomCamera != null && roomCamera.Active;
        const float CameraFollow = 5f; // smoothing of the room camera (higher = snappier)
        RoomCamera roomCamera;
        Camera cam;
        Light2D global;
        Transform levelRoot;
        int reinforcements;
        float shake, panelDispatchTimer;
        bool resettingCheckpoint;
        System.Action dialogDone;
        string debugLevel; // set by the Level Editor's "Play this level" button

        void Awake()
        {
            I = this;
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }
            cam.orthographic = true;
            cam.orthographicSize = 6.5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;

            foreach (var l in FindObjectsByType<Light2D>())
                if (l.lightType == Light2D.LightType.Global) global = l;
            if (global == null)
            {
                global = new GameObject("Global Light 2D").AddComponent<Light2D>();
                global.lightType = Light2D.LightType.Global;
            }

            AudioConfig.Reload();
            Sfx.Init(cam.gameObject);
            Music.Init(cam.gameObject);

            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem));
                es.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }

            Loc.Reload();
            Campaign = LevelDef.Campaign();
            UI = GameUI.Create(this);

#if UNITY_EDITOR
            debugLevel = UnityEditor.SessionState.GetString("ns_test_level", "");
            UnityEditor.SessionState.EraseString("ns_test_level");
#endif
            if (!string.IsNullOrEmpty(debugLevel))
            {
                int i = Campaign.IndexOf(debugLevel);
                if (i < 0) { Campaign.Add(debugLevel); i = Campaign.Count - 1; }
                StartMission(i);
            }
            else ShowMenu();
        }

        // ------------------------------------------------------------------ flow

        public void ShowMenu()
        {
            ClearLevel();
            Run = null;
            phase = Phase.Menu;
            Time.timeScale = 1f;
            cam.transform.position = new Vector3(0, 0, -10);
            Music.Play("menu");
            UI.ShowMenu();
        }

        void ClearLevel()
        {
            roomCamera = null;
            cam.rect = new Rect(0, 0, 1, 1);
            cam.orthographicSize = 6.5f;
            UI.SetCameraFrame(cam.rect, null);
            UI.ConfigureCompactHUD(false);
            UI.ClearMinimap();
            if (levelRoot != null) Destroy(levelRoot.gameObject);
            levelRoot = null;
            Level = null;
            Player = null;
            Script = null;
            Guards.Clear();
            Cameras.Clear();
            Interactables.Clear();
            GameInput.Clear();
        }

        /// Story mission from campaign.txt.
        public void StartMission(int index)
        {
            var def = LevelDef.Load(Campaign[index]);
            if (def == null) { ShowMenu(); return; }
            Run = null;
            MissionIndex = index;
            missionCards.Clear();
            StartLevel(def);
            if (Def.tutorial) BeginPlay(); // the tutorial briefs through dialogue instead
            else
            {
                phase = Phase.Briefing;
                UI.ShowBriefing(Def);
            }
        }

        // ------------------------------------------------------------------ night raid (roguelite)

        /// New Night Raid: a chain of random hand-made rooms, cards carry over.
        public void StartRaid()
        {
            Run = new RunState();
            if (!NextRaidRoom()) { ShowMenu(); return; }
            phase = Phase.Briefing;
            UI.ShowRaidIntro(Run);
        }

        bool NextRaidRoom()
        {
            string id = Run.NextRoom();
            var def = id != null ? LevelDef.Load(id) : null;
            if (def == null)
            {
                Debug.LogError("[MGS] Night Raid: no rooms in Resources/Levels/raid");
                return false;
            }
            StartLevel(def);
            return true;
        }

        public void ShowShip()
        {
            ClearLevel();
            Run = null;
            phase = Phase.Menu;
            Time.timeScale = 1f;
            cam.transform.position = new Vector3(0, 0, -10);
            Music.Play("base");
            UI.ShowShip();
        }

        /// Cards active right now: the run's cards in a raid, or cards found during a story mission.
        public List<CardId> ActiveCards => Run != null ? Run.cards : missionCards;
        readonly List<CardId> missionCards = new List<CardId>();

        // ------------------------------------------------------------------ level setup

        void StartLevel(LevelDef def)
        {
            ClearLevel();
            Def = def;
            Mission = new MissionState(Def);
            reinforcements = 0;
            panelDispatchTimer = -1f;
            resettingCheckpoint = false;

            levelRoot = new GameObject("Level").transform;
            Level = levelRoot.gameObject.AddComponent<Level>();
            Level.Build(Def);

            foreach (var gd in Def.guards)
            {
                var pts = new List<Vector2>();
                foreach (char c in gd.waypoints)
                {
                    if (Level.waypoints.TryGetValue(c, out var p)) pts.Add(p);
                    else Debug.LogWarning($"[MGS] {Def.id}: waypoint '{c}' not found on the map");
                }
                if (pts.Count > 0) Guards.Add(Guard.Spawn(Level, pts, gd));
            }

            foreach (var (a, b) in Def.vents)
            {
                if (Level.waypoints.TryGetValue(a, out var pa) && Level.waypoints.TryGetValue(b, out var pb))
                    Vent.Link(levelRoot, pa, pb);
                else Debug.LogWarning($"[MGS] {Def.id}: vent markers '{a}'/'{b}' not found on the map");
            }

            Player = Player.Spawn(Level, Level.playerStart, 1, 0, 0);
            ApplyStats(true);
            // Campaign, tutorial, raid rooms and editor maps share the same framing and HUD.
            roomCamera = RoomCamera.ForLevel(Def);
            roomCamera.Select(Player.Pos);
            UI.ConfigureCompactHUD(true);
            UI.BuildMinimap(Level);
            SnapCamera();

            if (Def.script.Count > 0)
            {
                Script = levelRoot.gameObject.AddComponent<ScriptRunner>();
                Script.Init(Def.script, Level.playerStart);
            }

            Time.timeScale = 1f;
            UpdateLighting();
            Music.Play("level");
        }

        /// Ship upgrades + active cards -> player stats. On level start also sets hp/ammo/traps
        /// (fresh loadout, or what the raid carried over from the previous room).
        void ApplyStats(bool levelStart)
        {
            var p = Player;
            var cards = ActiveCards;
            int Count(CardId id) => Cards.Count(cards, id);

            p.maxHp = (Def.hp > 0 ? Def.hp : Meta.MaxHp) + Count(CardId.Vitality);
            p.damage = Meta.Damage + Count(CardId.ApRounds);
            p.speedMul = Meta.SpeedMul * (1f + 0.15f * Count(CardId.Sprint));
            p.stealthMul = Meta.StealthMul * Mathf.Pow(0.7f, Count(CardId.SoftSoles));
            p.cameraMul = Count(CardId.Jammer) > 0 ? 0.5f : 1f;
            p.shotNoiseMul = Count(CardId.Suppressor) > 0 ? 0.5f : 1f;
            p.boxMaster = Count(CardId.BoxMaster) > 0;

            if (!levelStart) return;
            if (Run != null && Run.hp > 0)
            {
                p.hp = Mathf.Min(Run.hp, p.maxHp);
                p.ammo = Run.ammo;
                p.decoys = Run.traps;
                p.secondChance = Run.secondChance;
            }
            else
            {
                p.hp = p.maxHp;
                p.ammo = Def.ammo >= 0 ? Def.ammo : Meta.Ammo;
                p.decoys = Def.decoys >= 0 ? Def.decoys : Meta.Traps;
                p.secondChance = 0;
            }
        }

        // ------------------------------------------------------------------ cards

        /// Opened a chest: pause and offer 3 cards.
        public void OfferCards()
        {
            if (Player == null) return;
            var options = Cards.Roll(ActiveCards);
            if (options.Count == 0) return;
            if (Run != null) Run.chests++;
            phase = Phase.CardPick;
            Time.timeScale = 0f;
            GameInput.Clear();
            UI.ShowCards(options);
        }

        public void PickCard(CardDef card)
        {
            if (phase != Phase.CardPick) return;
            ActiveCards.Add(card.id);
            var p = Player;
            switch (card.id)
            {
                case CardId.ExtraMag: p.ammo += 3; break;
                case CardId.DoubleTrap: p.decoys += 2; break;
                case CardId.SecondChance: p.secondChance++; break;
                case CardId.Vitality: p.hp = p.maxHp + 1; break; // maxHp is raised by ApplyStats below
            }
            ApplyStats(false);
            p.hp = Mathf.Min(p.hp, p.maxHp);
            Sfx.Play("success", 0.7f);
            phase = Phase.Playing;
            Time.timeScale = 1f;
            GameInput.Clear();
            UI.ShowHUD();
            Toast(Loc.F("toast.card", card.Name));
        }

        // ------------------------------------------------------------------ flow

        public void BeginPlay()
        {
            phase = Phase.Playing;
            Time.timeScale = 1f;
            GameInput.Clear();
            UI.ShowHUD();
            if (Run != null) Toast(Loc.F("toast.raid_room", Run.roomIndex + 1, RunState.RoomCount));
            else if (!Def.tutorial) Toast(Loc.F("toast.objective", LevelDef.ObjectiveLabel(Mission.Current)));
        }

        public void Pause()
        {
            if (phase != Phase.Playing) return;
            phase = Phase.Paused;
            Time.timeScale = 0f;
            UI.ShowPause();
        }

        public void Resume()
        {
            if (phase != Phase.Paused) return;
            phase = Phase.Playing;
            Time.timeScale = 1f;
            GameInput.Clear();
            UI.ShowHUD();
        }

        public void Restart()
        {
            if (Run != null) StartRaid();
            else StartMission(MissionIndex);
        }

        public bool HasNextMission => Run == null && MissionIndex + 1 < Campaign.Count;
        public void NextMission() => StartMission(MissionIndex + 1);

        public void CompleteMission()
        {
            if (phase != Phase.Playing) return;
            if (Player != null) Player.ExitBox();
            Mission.extracted = true;
            phase = Phase.Result;
            Sfx.Play("success");

            if (Run != null)
            {
                // room cleared: bank the reward, carry the loadout into the next room
                Run.kills += Mission.kills;
                Run.alerts += Mission.alerts;
                Run.intel += Mission.intel;
                Run.time += Mission.time;
                Run.credits += RunState.RoomReward(Run.currentTier, Mission.alerts == 0) + Mission.intel * 30;
                Run.hp = Player.hp;
                Run.ammo = Player.ammo;
                Run.traps = Player.decoys;
                Run.secondChance = Player.secondChance;
                if (Run.IsLastRoom) FinishRaid(true);
                else UI.ShowRoomCleared(Run, Mission);
                return;
            }

            int credits = Def.tutorial ? 0 : Mission.Credits();
            Progress.Complete(Def.id, Mission.Rank, credits);
            UI.ShowResult(Mission, credits);
        }

        /// "Next room" button on the room-cleared screen.
        public void ContinueRaid()
        {
            if (Run == null) return;
            if (!NextRaidRoom()) { FinishRaid(true); return; }
            BeginPlay();
        }

        /// "Take the reward and leave" on the room-cleared screen.
        public void RetreatRaid()
        {
            if (Run == null) return;
            Run.retreated = true;
            FinishRaid(false, Run.roomIndex + 1);
        }

        void FinishRaid(bool won, int cleared = -1)
        {
            Run.finished = true;
            Run.won = won;
            if (cleared < 0) cleared = won ? RunState.RoomCount : Run.roomIndex;
            if (cleared > Progress.BestRaid) Progress.BestRaid = cleared;
            if (won) Run.credits += 150; // completion bonus
            Progress.Credits += Run.credits;
            phase = Phase.Result;
            UI.ShowRaidResult(Run, cleared);
        }

        public void PlayerDied()
        {
            phase = Phase.Result;
            Sfx.Play("fail");
            StartCoroutine(ShowFailLater());
        }

        IEnumerator ShowFailLater()
        {
            yield return new WaitForSecondsRealtime(1.2f);
            if (Run != null)
            {
                Run.kills += Mission.kills;
                Run.alerts += Mission.alerts;
                Run.time += Mission.time;
                FinishRaid(false);
            }
            else UI.ShowFail(Mission);
        }

        // ------------------------------------------------------------------ dialogue

        /// Visual-novel style line; the game is paused until the player taps through it.
        public void ShowDialog(string speaker, string text, System.Action done)
        {
            phase = Phase.Dialog;
            Time.timeScale = 0f;
            dialogDone = done;
            GameInput.Clear();
            UI.ShowDialog(speaker, text);
        }

        public void CloseDialog()
        {
            if (phase != Phase.Dialog) return;
            phase = Phase.Playing;
            Time.timeScale = 1f;
            GameInput.Clear();
            UI.ShowHUD();
            var done = dialogDone;
            dialogDone = null;
            done?.Invoke();
        }

        // ------------------------------------------------------------------ loop

        void Update()
        {
            if (phase == Phase.Playing)
            {
                if (GameInput.PausePressed()) { Pause(); return; }
                float dt = Time.deltaTime;
                Mission.time += dt;

                if (Mission.alertActive)
                {
                    Mission.alertTimer -= dt;
                    if (Mission.alertTimer <= 0f) EndAlert();
                }

                if (panelDispatchTimer > 0f && !Level.PowerOn)
                {
                    panelDispatchTimer -= dt;
                    if (panelDispatchTimer <= 0f) DispatchToPanel();
                }
                UI.UpdateHUD();
            }
            else if (phase == Phase.Paused && GameInput.PausePressed()) Resume();
            else if (phase == Phase.Dialog) UI.UpdateDialog();

            UpdateLighting();
        }

        void LateUpdate()
        {
            if (Player == null || Level == null) return;
            if (roomCamera != null)
            {
                if (phase == Phase.Playing) roomCamera.Select(Player.Pos); // never pauses or clears input
                if (roomCamera.Active)
                {
                    // smooth follow: covers room changes and leaning toward neighbouring rooms
                    Rect view = roomCamera.ViewFrame(Player.Pos, Screen.width, Screen.height);
                    float k = 1f - Mathf.Exp(-CameraFollow * Time.unscaledDeltaTime);
                    Vector3 pos = Vector3.Lerp(cam.transform.position, new Vector3(view.center.x, view.center.y, -10f), k);
                    cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, view.height * 0.5f, k);
                    if (shake > 0f)
                    {
                        shake = Mathf.Max(0f, shake - Time.unscaledDeltaTime);
                        pos += (Vector3)(Random.insideUnitCircle * shake * 0.5f);
                    }
                    cam.transform.position = new Vector3(pos.x, pos.y, -10f);
                    cam.rect = RoomCamera.Strip;
                    UI.SetCameraFrame(cam.rect, roomCamera.AreaId);
                }
                return;
            }
            Vector3 target = TargetCamPos();
            Vector3 p = Vector3.Lerp(cam.transform.position, target, 1f - Mathf.Exp(-8f * Time.unscaledDeltaTime));
            if (shake > 0f)
            {
                shake = Mathf.Max(0f, shake - Time.unscaledDeltaTime);
                p += (Vector3)(Random.insideUnitCircle * shake * 0.5f);
            }
            cam.transform.position = new Vector3(p.x, p.y, -10f);
        }

        Vector3 TargetCamPos()
        {
            Vector2 t = Player.Pos + Vector2.up * 0.5f;
            float halfH = cam.orthographicSize, halfW = halfH * cam.aspect;
            float x = Level.W <= halfW * 2 ? Level.W * 0.5f : Mathf.Clamp(t.x, halfW - 1f, Level.W - halfW + 1f);
            float y = Level.H <= halfH * 2 ? Level.H * 0.5f : Mathf.Clamp(t.y, halfH - 1f, Level.H - halfH + 1f);
            return new Vector3(x, y, -10f);
        }

        void SnapCamera()
        {
            // A tutorial checkpoint can teleport the player into another authored area.
            roomCamera?.Select(Player.Pos);
            if (RoomCameraActive)
            {
                Rect view = roomCamera.ViewFrame(Player.Pos, Screen.width, Screen.height);
                cam.transform.position = new Vector3(view.center.x, view.center.y, -10f);
                cam.orthographicSize = view.height * 0.5f;
                cam.rect = RoomCamera.Strip;
                UI.SetCameraFrame(cam.rect, roomCamera.AreaId);
            }
            else cam.transform.position = TargetCamPos();
        }

        public void Shake(float amount) => shake = Mathf.Max(shake, amount);

        void UpdateLighting()
        {
            if (global == null) return;
            Color baseColor = new Color(0.55f, 0.62f, 0.7f);
            float intensity = 0.5f;
            if (Level != null)
            {
                intensity = Level.PowerOn ? 0.17f : 0.045f;
                if (!Level.PowerOn) baseColor = new Color(0.55f, 0.45f, 0.5f);
            }
            if (Mission != null && Mission.alertActive && phase == Phase.Playing)
                baseColor = Color.Lerp(baseColor, new Color(1f, 0.2f, 0.15f), 0.35f + 0.25f * Mathf.Sin(Time.time * 6f));
            global.color = baseColor;
            global.intensity = intensity;
        }

        // ------------------------------------------------------------------ stealth events

        public void RaiseAlert(string reason, Vector2 pos)
        {
            if (Def.tutorial)
            {
                CaughtInTutorial(reason);
                return;
            }

            var m = Mission;
            m.lastKnownPos = pos;
            m.alertTimer = AlertDuration;
            if (m.alertActive) return;

            m.alertActive = true;
            m.alerts++;
            m.lastAlertReason = reason;
            Sfx.Play("alert");
            Shake(0.2f);
            UI.ShowBanner("! " + reason.ToUpperInvariant(), UIKit.Red, 3.5f);

            foreach (var g in Guards) g.EnterAlert();

            if (reinforcements < 2 && Level.exitSpawn != Vector2.zero)
            {
                reinforcements++;
                var route = new List<Vector2> { Level.exitSpawn };
                var g = Guard.Spawn(Level, route);
                Guards.Add(g);
                g.EnterAlert();
                Toast(Loc.T("toast.reinforcements"));
            }
        }

        /// In the tutorial being spotted is a lesson, not a fail: explain and rewind to the checkpoint.
        void CaughtInTutorial(string reason)
        {
            if (resettingCheckpoint) return;
            resettingCheckpoint = true;
            Sfx.Play("alert");
            Shake(0.2f);
            ShowDialog("major", Loc.F("tut.caught", reason.ToLowerInvariant()), () =>
            {
                Player.Teleport(Script != null ? Script.Checkpoint : Level.playerStart);
                foreach (var g in Guards) g.ResetToStart();
                foreach (var it in Interactables.ToArray())
                    if (it is HideBox box) box.ResetHome();
                foreach (var c in Cameras) c.suspicion = 0f;
                SnapCamera();
                resettingCheckpoint = false;
            });
        }

        public void ReportSighting(Vector2 pos)
        {
            if (Def.tutorial) { CaughtInTutorial(Loc.T("reason.guard")); return; }
            Mission.lastKnownPos = pos;
            Mission.alertTimer = AlertDuration;
        }

        void EndAlert()
        {
            Mission.alertActive = false;
            UI.ShowBanner(Loc.T("banner.alert_over"), UIKit.Amber, 2.5f);
        }

        public void EmitNoise(Vector2 pos, float radius)
        {
            foreach (var g in Guards)
                if (!g.Dead && Vector2.Distance(g.Pos, pos) <= radius) g.OnNoise(pos);
            NoiseRipple.Spawn(levelRoot, pos, radius);
        }

        public void OnGuardKilled(Guard g) => Mission.kills++;

        public void OnPowerChanged(bool on)
        {
            if (on)
            {
                Toast(Loc.T("toast.power_on"));
                panelDispatchTimer = -1f;
                return;
            }
            Mission.powerWasCut = true;
            panelDispatchTimer = PanelDispatchDelay;
            UI.ShowBanner(Loc.T("banner.power_off"), UIKit.Amber, 3f);
        }

        void DispatchToPanel()
        {
            if (Level.panel == null) return;
            Guard best = null;
            float bestD = float.MaxValue;
            foreach (var g in Guards)
            {
                if (g.Dead || g.state == GuardState.Alert) continue;
                float d = Vector2.Distance(g.Pos, Level.panel.ServicePoint);
                if (d < bestD) { bestD = d; best = g; }
            }
            if (best != null)
            {
                best.SendToPanel();
                Toast(Loc.T("toast.panel_dispatch"));
            }
        }

        public void Toast(string text) => UI.Toast(text);

        /// World position the HUD pointer should lead to: the current main objective,
        /// or (in scripted levels) the marker the script is waiting for. Null = nothing to show.
        public Vector2? ObjectiveTarget()
        {
            if (Mission == null || Level == null) return null;
            if (Def.tutorial || Mission.scriptObjective != null) return Script != null ? Script.Target : null;
            var cur = Mission.Current;
            foreach (var it in Interactables)
            {
                switch (cur)
                {
                    case ObjectiveKind.GetKeycard:
                        if (it is Pickup k && k.kind == PickupKind.Keycard && k.Available) return k.Pos;
                        break;
                    case ObjectiveKind.GetPrototype:
                        if (it is Pickup pr && pr.kind == PickupKind.Prototype && pr.Available) return pr.Pos;
                        break;
                    case ObjectiveKind.CutPower:
                        if (it is PowerPanel pp) return pp.Pos;
                        break;
                    case ObjectiveKind.DownloadData:
                        if (it is DataTerminal t && t.IsObjective) return t.Pos;
                        break;
                    case ObjectiveKind.Extract:
                        if (it is ExitDoor e) return e.Pos;
                        break;
                }
            }
            return null;
        }
    }
}
