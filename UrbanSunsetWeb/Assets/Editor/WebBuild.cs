using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace UrbanSunset.EditorTools
{
    /// <summary>
    /// One-click / command-line WebGL build. The game builds itself at runtime, so the only scene is empty.
    ///   Editor:  menu "Urban Sunset > Build WebGL"
    ///   CI:      -executeMethod UrbanSunset.EditorTools.WebBuild.Build [-customBuildPath out/dir]
    /// </summary>
    public static class WebBuild
    {
        const string ScenePath = "Assets/Scenes/Main.unity";

        [MenuItem("Urban Sunset/Build WebGL")]
        public static void Build()
        {
            string output = GetArg("-customBuildPath") ?? "Build/WebGL";
            Debug.Log("[UrbanSunset] Building WebGL to " + output);

            AssetDatabase.ImportAsset("Assets/Resources", ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
            EnsureScene();
            ConfigurePlayer();

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = output,
                target = BuildTarget.WebGL,
                targetGroup = BuildTargetGroup.WebGL,
                options = BuildOptions.None
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            Debug.Log("[UrbanSunset] Build result: " + report.summary.result + ", size " + report.summary.totalSize + " bytes");
            if (report.summary.result != BuildResult.Succeeded)
                throw new Exception("WebGL build failed: " + report.summary.result);
        }

        [MenuItem("Urban Sunset/Create Main Scene")]
        public static void EnsureScene()
        {
            if (!File.Exists(ScenePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
                AssetDatabase.Refresh();
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "GD2A";
            PlayerSettings.productName = "Urban Sunset";
            PlayerSettings.runInBackground = false;
            PlayerSettings.colorSpace = ColorSpace.Gamma;
            PlayerSettings.defaultWebScreenWidth = 1280;
            PlayerSettings.defaultWebScreenHeight = 720;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.WebGL, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.WebGL, new[] { GraphicsDeviceType.OpenGLES3 });
            try { PlayerSettings.SplashScreen.show = false; }
            catch (Exception e) { Debug.LogWarning("Could not disable splash screen: " + e.Message); }

            PlayerSettings.WebGL.template = "PROJECT:UrbanSunset";
            // Gzip + decompression fallback works on static hosts (GitHub Pages) that don't send
            // Content-Encoding headers for .gz files.
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.None;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.nameFilesAsHashes = false;
            PlayerSettings.stripEngineCode = true;
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.Medium);

            // The game reads UnityEngine.Input directly: make sure the legacy Input Manager is active.
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (assets != null && assets.Length > 0)
            {
                var so = new SerializedObject(assets[0]);
                var prop = so.FindProperty("activeInputHandler");
                if (prop != null && prop.intValue != 0)
                {
                    prop.intValue = 0;
                    so.ApplyModifiedProperties();
                }
            }
            AssetDatabase.SaveAssets();
        }

        static string GetArg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return null;
        }
    }
}
