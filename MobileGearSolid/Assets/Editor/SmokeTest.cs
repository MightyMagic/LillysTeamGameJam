using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NightShift.EditorTools
{
    /// Batch-mode play test: enters Play Mode, runs scripted steps through the missions,
    /// saves screenshots to Logs/SmokeTest and logs every error. Usage:
    ///   Unity -batchmode -projectPath . -executeMethod NightShift.EditorTools.SmokeTest.Run   (screenshots in Logs/SmokeTest)
    public static class SmokeTest
    {
        static int step;
        static double stepStart;
        static readonly List<string> errors = new List<string>();
        static string outDir;

        const string RunningKey = "ns_smoke_running";

        [MenuItem("Night Shift/Run Smoke Test")]
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Game.unity");
            SessionState.SetBool(RunningKey, true);
            Hook();
        }

        // entering Play Mode reloads the domain, so re-attach afterwards
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
        }

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
            if (Game.I == null) return;
            var g = Game.I;
            switch (step)
            {
                case 0: if (Elapsed > 1.0) { Shot("00_menu"); Next(); } break;
                case 1: g.StartMission(0); Next(); break;
                case 2: if (Elapsed > 0.8) { Shot("01_briefing"); g.BeginPlay(); Next(); } break;
                case 3: if (Elapsed > 1.0) { Shot("02_m1_start"); GameInput.Joystick = new Vector2(1, -0.3f); Next(); } break;
                case 4: if (Elapsed > 2.5) { GameInput.Joystick = Vector2.zero; Shot("03_m1_moved"); Next(); } break;
                case 5:
                    GameInput.PressDecoy();
                    Next();
                    break;
                case 6: if (Elapsed > 1.2) { Shot("04_decoy"); Next(); } break;
                case 7:
                    g.RaiseAlert("Тест: охранник заметил тебя", g.Player.Pos);
                    Next();
                    break;
                case 8: if (Elapsed > 1.5) { Shot("05_alert"); Next(); } break;
                case 9: g.CompleteMission(); Next(); break;
                case 10: if (Elapsed > 0.8) { Shot("06_result"); g.StartMission(1); g.BeginPlay(); Next(); } break;
                case 11: if (Elapsed > 1.0) { g.Level.CutPower(); Next(); } break;
                case 12: if (Elapsed > 1.5) { Shot("07_m2_poweroff"); Next(); } break;
                case 13: if (Elapsed > 12) { LogGuards(); Shot("08_m2_guard_to_panel"); g.StartMission(2); g.BeginPlay(); Next(); } break;
                case 14:
                    if (Elapsed > 1.0)
                    {
                        // teleport near the keycard camera to look at the warehouse
                        g.Player.transform.position = new Vector3(30.5f, 9.5f);
                        g.Player.GetComponent<Rigidbody2D>().position = new Vector2(30.5f, 9.5f);
                        Next();
                    }
                    break;
                case 15:
                    if (Elapsed > 2.5)
                    {
                        foreach (var c in Game.I.Cameras) Debug.Log($"[SmokeTest] camera {c.transform.position} sees={c.seesPlayer} susp={c.suspicion:0.00}");
                        var pl = Game.I.Player;
                        var hit = Physics2D.Linecast(new Vector2(30.5f, 12.9f), pl.Center, Level.ObstacleMask);
                        Debug.Log($"[SmokeTest] player {pl.Pos} alert={Game.I.Mission.alertActive} linecastHit={(hit.collider ? hit.collider.name + " " + hit.point : "none")}");
                        Shot("09_m3_warehouse"); Next();
                    }
                    break;
                case 16:
                    g.Player.GetComponent<Rigidbody2D>().position = new Vector2(10.5f, 7.5f);
                    Next();
                    break;
                case 17: if (Elapsed > 6) { Shot("10_m3_armory"); Next(); } break;
                case 18: Finish(); break;
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
            File.WriteAllBytes(Path.Combine(outDir, name + ".png"), tex.EncodeToPNG());
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
            Progress.ResetAll(); // don't leave test progress in the user's PlayerPrefs
            File.WriteAllText(Path.Combine(outDir, "errors.txt"), errors.Count + " errors\n\n" + string.Join("\n----\n", errors));
            Debug.Log($"[SmokeTest] finished, {errors.Count} errors");
            EditorApplication.isPlaying = false;
            EditorApplication.Exit(errors.Count == 0 ? 0 : 1);
        }
    }
}
