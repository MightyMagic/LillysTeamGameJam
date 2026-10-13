using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;

namespace NightShift
{
    /// Entry point. Lives in the Game scene and drives menu -> briefing -> mission -> result.
    /// The level, actors and UI are all built from code.
    public class Game : MonoBehaviour
    {
        public enum Phase { Menu, Briefing, Playing, Paused, Result }

        public static Game I;
        public Phase phase;

        public Level Level { get; private set; }
        public Player Player { get; private set; }
        public MissionState Mission { get; private set; }
        public MissionDef Def { get; private set; }
        public int MissionIndex { get; private set; }
        public GameUI UI { get; private set; }

        public readonly List<Guard> Guards = new List<Guard>();
        public readonly List<SecurityCamera> Cameras = new List<SecurityCamera>();
        public readonly List<Interactable> Interactables = new List<Interactable>();

        public const float AlertDuration = 18f;
        const float PanelDispatchDelay = 10f;

        Camera cam;
        Light2D global;
        Transform levelRoot;
        int reinforcements;
        float shake, panelDispatchTimer;

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

            Sfx.Init(cam.gameObject);

            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem));
                es.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }

            UI = GameUI.Create(this);
            ShowMenu();
        }

        // ------------------------------------------------------------------ flow

        public void ShowMenu()
        {
            ClearLevel();
            phase = Phase.Menu;
            Time.timeScale = 1f;
            cam.transform.position = new Vector3(0, 0, -10);
            UI.ShowMenu();
        }

        void ClearLevel()
        {
            if (levelRoot != null) Destroy(levelRoot.gameObject);
            levelRoot = null;
            Level = null;
            Player = null;
            Guards.Clear();
            Cameras.Clear();
            Interactables.Clear();
            GameInput.Clear();
        }

        public void StartMission(int index)
        {
            ClearLevel();
            MissionIndex = index;
            Def = MissionDef.All[index];
            Mission = new MissionState(Def);
            reinforcements = 0;
            panelDispatchTimer = -1f;

            levelRoot = new GameObject("Level").transform;
            Level = levelRoot.gameObject.AddComponent<Level>();
            Level.Build(Resources.Load<TextAsset>("Levels/facility").text, Def);

            foreach (var route in Def.routes)
            {
                var pts = new List<Vector2>();
                foreach (char c in route)
                    if (Level.waypoints.TryGetValue(c, out var p)) pts.Add(p);
                if (pts.Count > 0) Guards.Add(Guard.Spawn(Level, pts));
            }

            int hp = 4 + (Progress.Has(Upgrade.Armor) ? 1 : 0);
            int ammo = 6 + (Progress.Has(Upgrade.Magazine) ? 3 : 0);
            int decoys = 1 + (Progress.Has(Upgrade.Decoy) ? 1 : 0);
            Player = Player.Spawn(Level, Level.playerStart, hp, ammo, decoys);
            SnapCamera();

            phase = Phase.Briefing;
            Time.timeScale = 1f;
            UpdateLighting();
            UI.ShowBriefing(Def);
        }

        public void BeginPlay()
        {
            phase = Phase.Playing;
            Time.timeScale = 1f;
            GameInput.Clear();
            UI.ShowHUD();
            Toast("Цель: " + MissionDef.ObjectiveLabel(Mission.Current));
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

        public void Restart() => StartMission(MissionIndex);

        public void CompleteMission()
        {
            if (phase != Phase.Playing) return;
            Mission.extracted = true;
            phase = Phase.Result;
            int credits = Mission.Credits();
            Progress.Complete(MissionIndex, Mission.Rank, credits);
            Sfx.Play("success");
            UI.ShowResult(Mission, MissionIndex, credits);
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
            UI.ShowFail(Mission);
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

            UpdateLighting();
        }

        void LateUpdate()
        {
            if (Player == null || Level == null) return;
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

        void SnapCamera() => cam.transform.position = TargetCamPos();

        public void Shake(float amount) => shake = Mathf.Max(shake, amount);

        void UpdateLighting()
        {
            if (global == null) return;
            Color baseColor = new Color(0.55f, 0.62f, 0.7f);
            float intensity = 0.5f;
            if (Level != null)
            {
                intensity = Level.PowerOn ? 0.17f : 0.09f;
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
                if (Level.waypoints.TryGetValue('A', out var a)) route.Add(a);
                var g = Guard.Spawn(Level, route);
                Guards.Add(g);
                g.EnterAlert();
                Toast("Тревога! Прибыло подкрепление");
            }
        }

        public void ReportSighting(Vector2 pos)
        {
            Mission.lastKnownPos = pos;
            Mission.alertTimer = AlertDuration;
        }

        void EndAlert()
        {
            Mission.alertActive = false;
            UI.ShowBanner("Тревога снята — охрана ищет тебя", UIKit.Amber, 2.5f);
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
                Toast("Питание восстановлено");
                panelDispatchTimer = -1f;
                return;
            }
            Mission.powerWasCut = true;
            panelDispatchTimer = PanelDispatchDelay;
            UI.ShowBanner("Питание отключено: камеры и свет не работают", UIKit.Amber, 3f);
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
                Toast("Охрану отправили проверить щит");
            }
        }

        public void Toast(string text) => UI.Toast(text);
    }
}
