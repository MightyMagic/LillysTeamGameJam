using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MobileGearSolid.EditorTools
{
    /// Batch-mode play test. An autopilot plays the whole tutorial (walks, shoots, hides in the box,
    /// freezes when watched, places a trap and sneaks around), then opens every mission.
    /// Saves screenshots to Logs/SmokeTest and logs every error. Player progress is restored afterwards.
    ///   Unity -batchmode -projectPath . -executeMethod MobileGearSolid.EditorTools.SmokeTest.Run
    public static class SmokeTest
    {
        static int step;
        static double stepStart;
        static readonly List<string> errors = new List<string>();
        static string outDir;
        static int dialogs, shots;
        static double dialogSince = -1;

        const string RunningKey = "ns_smoke_running";

        [MenuItem("Mobile Gear Solid/Run Smoke Test")]
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Game.unity");
            SessionState.SetBool(RunningKey, true);
            SnapshotPrefs();
            Hook();
        }

        // entering Play Mode may reload the domain, so re-attach afterwards
        [InitializeOnLoadMethod]
        static void Resume()
        {
            if (SessionState.GetBool(RunningKey, false)) Hook();
        }

        static void Hook()
        {
            outDir = Path.GetFullPath("Logs/SmokeTest");
            Directory.CreateDirectory(outDir);
            Application.logMessageReceived -= OnLog;
            Application.logMessageReceived += OnLog;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            step = 0;
            stepStart = EditorApplication.timeSinceStartup;
        }

        static void OnLog(string msg, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                errors.Add(msg + "\n" + stack);
        }

        static double Elapsed => EditorApplication.timeSinceStartup - stepStart;

        static void Next()
        {
            step++;
            stepStart = EditorApplication.timeSinceStartup;
            GameInput.Joystick = Vector2.zero;
        }

        static void Fail(string why)
        {
            var g = Game.I;
            why += $" | phase={g.phase} timeScale={Time.timeScale} dt={Time.deltaTime} player={g.Player?.Pos} joy={GameInput.Joystick} kills={g.Mission?.kills}";
            errors.Add($"[SmokeTest] step {step}: {why}");
            Debug.Log($"[SmokeTest] FAIL step {step}: {why}");
            Shot($"fail_step{step}");
            Finish();
        }

        // ------------------------------------------------------------------ autopilot helpers

        static Vector2 WP(char c) => Game.I.Level.waypoints[c];

        /// Walks along an A* path; true when within tolerance.
        static bool MoveTo(Vector2 target, float tol = 0.35f)
        {
            var p = Game.I.Player;
            if (Vector2.Distance(p.Pos, target) <= tol) { GameInput.Joystick = Vector2.zero; return true; }
            var path = Game.I.Level.FindPath(p.Pos, target);
            Vector2 next = target;
            foreach (var n in path)
                if (Vector2.Distance(p.Pos, n) > 0.3f) { next = n; break; }
            GameInput.Joystick = (next - p.Pos).normalized;
            return false;
        }

        /// While in the box: freeze whenever a guard is looking our way.
        static bool Watched()
        {
            var p = Game.I.Player;
            foreach (var g in Game.I.Guards)
                if (!g.Dead && g.CanSee(p.Center, Guard.ViewDist + 1f)) return true;
            return false;
        }

        // ------------------------------------------------------------------ script

        static void Tick()
        {
            if (!EditorApplication.isPlaying)
            {
                // let startup imports/recompiles settle before entering Play Mode
                if (Elapsed > 8 && !EditorApplication.isCompiling && !EditorApplication.isUpdating)
                    EditorApplication.isPlaying = true;
                if (Elapsed > 120) Finish();
                return;
            }
            var g = Game.I;
            if (g == null) return;
            Application.runInBackground = true; // keep simulating while the Editor window is unfocused

            // dialogue: screenshot the first few, then tap through
            if (g.phase == Game.Phase.Dialog)
            {
                GameInput.Joystick = Vector2.zero;
                if (dialogSince < 0) dialogSince = EditorApplication.timeSinceStartup;
                if (EditorApplication.timeSinceStartup - dialogSince > 0.8)
                {
                    if (dialogs < 3 || dialogs % 4 == 0) Shot($"dialog_{dialogs:00}");
                    dialogs++;
                    dialogSince = -1;
                    g.CloseDialog();
                }
                return;
            }
            if (Elapsed > 60 && step < 100) { Fail("timeout"); return; }

            var p = g.Player;
            switch (step)
            {
                case 0: if (Elapsed > 1) { g.StartMission(0); Next(); } break;          // tutorial
                case 1: if (Elapsed > 0.5) Next(); break;
                // --- 1. close in on the guard and shoot
                case 2:
                    if (MoveTo(WP('0') + Vector2.left * 3f, 0.3f)) { GameInput.Joystick = Vector2.right * 0.16f; Next(); }
                    break;
                case 3: GameInput.Joystick = Vector2.zero; if (Elapsed > 0.2) { Shot("t1_aim"); GameInput.PressShoot(); Next(); } break;
                case 4: if (g.Mission.kills >= 1) { Shot("t2_killed"); Next(); } else if (Elapsed > 2) Fail("guard not killed"); break;
                // --- 2. box
                case 5: if (MoveTo(WP('1'))) Next(); break;
                case 6: if (MoveTo(WP('8') + Vector2.down * 0.6f, 0.3f)) Next(); break;
                case 7: if (Elapsed > 0.3) { Shot("t3_box_prompt"); GameInput.PressInteract(); Next(); } break;
                case 8: if (p.InBox) { Shot("t4_in_box"); Next(); } else if (Elapsed > 2) Fail("could not enter box"); break;
                case 9:
                    if (Watched()) GameInput.Joystick = Vector2.zero;
                    else if (MoveTo(new Vector2(40.5f, WP('8').y - 1f), 0.4f)) Next();
                    if (Elapsed > 8 && Elapsed < 8.05) Shot("t5_sneaking");
                    break;
                case 10: if (MoveTo(WP('4'), 0.5f)) Next(); break;
                // --- 3. trap
                case 11: GameInput.PressInteract(); Next(); break;          // leave the box
                case 12: if (Elapsed > 0.3) { GameInput.PressDecoy(); Next(); } break;
                case 13:
                    if (Elapsed < 0.3) break;
                    if (g.Mission.trapsUsed < 1) { Fail("trap not placed"); break; }
                    if (MoveTo(new Vector2(WP('4').x, 1.5f), 0.4f)) Next();
                    break;
                case 14: if (MoveTo(new Vector2(58.5f, 1.5f), 0.4f)) { Shot("t6_loop"); Next(); } break;
                case 15: if (g.phase == Game.Phase.Result || MoveTo(WP('7'), 0.6f)) Next(); break;
                case 16:
                    if (g.phase == Game.Phase.Result) { Shot("t7_tutorial_done"); Debug.Log("[SmokeTest] tutorial completed"); Next(); }
                    else if (Elapsed > 5) Fail("tutorial did not finish");
                    break;
                // --- missions: open each, look around, check box + alert in mission 2
                case 17: if (Elapsed > 1) { g.StartMission(1); Next(); } break;
                case 18: if (Elapsed > 0.8) { Shot("m1_briefing"); g.BeginPlay(); Next(); } break;
                case 19: if (Elapsed > 1.5) { Shot("m1_start_pointer"); p.hp = 2; p.Hurt(1); Next(); } break;
                case 20: if (Elapsed > 0.12) { Shot("m1_hurt_flash"); Next(); } break;
                case 21: if (Elapsed > 2.0) { Shot("m1_low_hp_pulse"); g.StartMission(2); g.BeginPlay(); Next(); } break;
                case 22: if (Elapsed > 1.5) { Shot("m2_start"); g.Level.CutPower(); Next(); } break;
                case 23: if (Elapsed > 1.5) { Shot("m2_power_off"); g.StartMission(3); g.BeginPlay(); Next(); } break;
                case 24: if (Elapsed > 1.5) { Shot("m3_start"); g.RaiseAlert("test", p.Pos); Next(); } break;
                case 25: if (Elapsed > 8) { Shot("m3_alert_search"); LogGuards(); Next(); } break;
                // --- night raid: chest -> card -> next room -> death -> results; ship + menu screens
                case 26: g.ShowMenu(); Next(); break;
                case 27: if (Elapsed > 0.8) { Shot("menu"); g.ShowShip(); Next(); } break;
                case 28: if (Elapsed > 0.8) { Shot("ship"); g.StartRaid(); Next(); } break;
                case 29: if (Elapsed > 0.8) { Shot("raid_intro"); g.BeginPlay(); Next(); } break;
                case 30:
                    if (Elapsed > 1.0)
                    {
                        Shot("raid_room1");
                        Chest chest = null;
                        foreach (var it in g.Interactables) if (it is Chest c) chest = c;
                        if (chest == null) { Fail("no chest in raid room " + g.Run.currentRoom); break; }
                        p.Teleport(chest.Pos + Vector2.down * 1.1f);
                        Next();
                    }
                    break;
                case 31: if (Elapsed > 0.4) { GameInput.PressInteract(); Next(); } break;
                case 32:
                    if (g.phase == Game.Phase.CardPick) { if (Elapsed > 0.6) { Shot("raid_cards"); Next(); } }
                    else if (Elapsed > 2) Fail("chest did not offer cards");
                    break;
                case 33:
                    g.PickCard(Cards.Get(CardId.ApRounds));
                    Next();
                    break;
                case 34: if (Elapsed > 0.8) { Shot("raid_card_taken"); Debug.Log($"[SmokeTest] damage after card {p.damage}"); g.CompleteMission(); Next(); } break;
                case 35: if (Elapsed > 0.8) { Shot("raid_room_cleared"); g.ContinueRaid(); Next(); } break;
                case 36:
                    if (Elapsed > 1.0)
                    {
                        Shot("raid_room2");
                        Debug.Log($"[SmokeTest] raid room2 {g.Run.currentRoom} tier {g.Run.currentTier} hp {p.hp} cards {g.Run.cards.Count}");
                        p.secondChance = 0;
                        p.hp = 1;
                        p.Hurt(5);
                        Next();
                    }
                    break;
                case 37: if (Elapsed > 2.5) { Shot("raid_result"); Next(); } break;
                case 38: Finish(); break;
            }
        }

        static void LogGuards()
        {
            foreach (var gd in Game.I.Guards) Debug.Log($"[SmokeTest] guard at {gd.Pos} state {gd.state}");
        }

        static void Shot(string name)
        {
            var cam = Camera.main;
            var canvas = Object.FindAnyObjectByType<Canvas>();
            var prevMode = canvas.renderMode;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 1f;
            Canvas.ForceUpdateCanvases();

            var rt = new RenderTexture(1600, 900, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(outDir, $"{shots++:00}_{name}.png"), tex.EncodeToPNG());
            RenderTexture.active = null;
            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
            canvas.renderMode = prevMode;
            Debug.Log("[SmokeTest] screenshot " + name);
        }

        static void Finish()
        {
            SessionState.SetBool(RunningKey, false);
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= OnLog;
            RestorePrefs(); // don't leave test progress in the user's PlayerPrefs
            File.WriteAllText(Path.Combine(outDir, "errors.txt"), errors.Count + " errors\n\n" + string.Join("\n----\n", errors));
            Debug.Log($"[SmokeTest] finished at step {step}, {errors.Count} errors");
            EditorApplication.isPlaying = false;
            if (Application.isBatchMode) EditorApplication.Exit(errors.Count == 0 ? 0 : 1);
        }

        // ------------------------------------------------------------------ keep the user's save intact

        static string[] PrefKeys()
        {
            var keys = new List<string> { "ns_credits" };
            foreach (var id in LevelDef.Campaign()) { keys.Add("ns_done_" + id); keys.Add("ns_rank_" + id); }
            foreach (Stat st in System.Enum.GetValues(typeof(Stat))) keys.Add("ns_stat_" + st);
            keys.Add("ns_raid_best");
            return keys.ToArray();
        }

        static void SnapshotPrefs()
        {
            foreach (var k in PrefKeys())
                SessionState.SetInt("ns_smoke_pref_" + k, PlayerPrefs.HasKey(k) ? PlayerPrefs.GetInt(k) : int.MinValue);
        }

        static void RestorePrefs()
        {
            foreach (var k in PrefKeys())
            {
                int v = SessionState.GetInt("ns_smoke_pref_" + k, int.MinValue);
                if (v == int.MinValue) PlayerPrefs.DeleteKey(k); else PlayerPrefs.SetInt(k, v);
            }
            PlayerPrefs.Save();
        }
    }
}
