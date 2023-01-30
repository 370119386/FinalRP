using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using VEngine.Editor.Builds;
using VEngine.Editor.Simulation;

namespace VEngine.Editor
{
    public static class Initializer
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            Versions.PlatformName = Settings.GetPlatformName();
            var config = Settings.GetPlayerSettings();
            var settings = Settings.GetDefaultSettings();
            var builds = Build.GetAllBuilds();
            config.simulationMode = settings.scriptPlayMode == ScriptPlayMode.Simulation;
            config.manifests = Array.ConvertAll(builds, build => build.name);
            switch (settings.scriptPlayMode)
            {
                case ScriptPlayMode.Simulation:
                    settings.Initialize();
                    Asset.Creator = EditorAsset.Create;
                    Scene.Creator = EditorScene.Create;
                    ManifestAsset.Creator = EditorManifestAsset.Create;
                    config.offlineMode = true;
                    config.binaryMode = false;
                    break;
                case ScriptPlayMode.Preload:
                    Versions.PlayerDataPath = Path.Combine(Environment.CurrentDirectory, Settings.PlatformBuildPath);
                    config.offlineMode = true;
                    config.binaryMode = false;
                    break;
                case ScriptPlayMode.Incremental:
                    if (!Directory.Exists(Path.Combine(Application.streamingAssetsPath, Utility.buildPath)))
                    {
                        config.assets.Clear();
                    }
                    config.offlineMode = false;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            VEngine.Logger.F($"[EditorOnly]:[Initializer]:[ScriptPlayMode]:[{settings.scriptPlayMode}]");
            // PlayerSettings 需要 Unity 编辑器设置为 Auto Refresh，这里要改成不依赖 Auto Refresh 的版本
            AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(config));
        }
    }
}