using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MobileGearSolid.EditorTools
{
    /// Builds an Android APK into Builds/AndroidBuilds/MobileGearSolid_<date>.apk (Builds/ is git-ignored).
    /// Menu: Mobile Gear Solid > Build Android APK. Batch: -executeMethod MobileGearSolid.EditorTools.AndroidBuilder.Build
    public static class AndroidBuilder
    {
        public const string OutputDir = "Builds/AndroidBuilds";

        [MenuItem("Mobile Gear Solid/Build Android APK")]
        public static void Build()
        {
            Directory.CreateDirectory(OutputDir);
            string path = Path.Combine(OutputDir, $"MobileGearSolid_{DateTime.Now:yyyy-MM-dd_HH-mm}.apk");

            EditorUserBuildSettings.buildAppBundle = false; // plain APK that installs directly on a phone
            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = path,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None,
            };

            var report = BuildPipeline.BuildPlayer(options);
            var s = report.summary;
            if (s.result == BuildResult.Succeeded)
                Debug.Log($"[MGS] Android build OK: {Path.GetFullPath(path)} ({new FileInfo(path).Length / (1024f * 1024f):0.0} MB, {s.totalTime:mm\\:ss})");
            else
                Debug.LogError($"[MGS] Android build {s.result}: {s.totalErrors} errors");

            if (Application.isBatchMode) EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
