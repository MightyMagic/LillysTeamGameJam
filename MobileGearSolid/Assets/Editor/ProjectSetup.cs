using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace MobileGearSolid.EditorTools
{
    /// One-click (or batch-mode) setup: reimports sprites, creates the unlit material,
    /// builds the Game scene and registers it in Build Settings.
    public static class ProjectSetup
    {
        const string ScenePath = "Assets/Scenes/Game.unity";

        [MenuItem("Mobile Gear Solid/Setup Project (rebuild Game scene)")]
        public static void Run()
        {
            AssetDatabase.ImportAsset("Assets/Resources/Sprites", ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);

            if (!AssetDatabase.IsValidFolder("Assets/Resources/Materials"))
                AssetDatabase.CreateFolder("Assets/Resources", "Materials");
            const string matPath = "Assets/Resources/Materials/Unlit.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(matPath) == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                AssetDatabase.CreateAsset(new Material(shader), matPath);
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 7f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            camGo.transform.position = new Vector3(0, 0, -10);
            camGo.AddComponent<AudioListener>();

            var lightGo = new GameObject("Global Light 2D");
            var light = lightGo.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.intensity = 0.42f;
            light.color = new Color(0.55f, 0.62f, 0.7f);

            new GameObject("Game").AddComponent<Game>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            PlayerSettings.productName = "Mobile Gear Solid";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;

            AssetDatabase.SaveAssets();
            Debug.Log("[MGS] Project setup complete: " + ScenePath);
        }
    }
}
