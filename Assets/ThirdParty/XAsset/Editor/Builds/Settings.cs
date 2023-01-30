using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace VEngine.Editor.Builds
{
    public enum ScriptPlayMode
    {
        Simulation,
        Preload,
        Incremental
    }

    [CreateAssetMenu(menuName = "Versions/Settings", fileName = "Settings", order = 0)]
    public sealed class Settings : ScriptableObject
    {
        public static bool BundleNameWithHash;
        public static string BundleExtension;
        public static bool AppendCRCToBundleName;
        public static List<string> ExcludeFiles;

        /// <summary>
        ///     采集资源或依赖需要过滤掉的文件
        /// </summary>
        [Header("Bundle")] public List<string> excludeFiles =
            new List<string>
            {
                ".spriteatlas",
                ".giparams",
                "LightingData.asset"
            };

        /// <summary>
        ///     使用 crc 重名输出给打包后的 bundle 为 bundle_crc.ext，开启后会多输出一份文件到打包目录。
        /// </summary>
        public bool appendCRCToBundleName;

        /// <summary>
        ///     是否将 bundle 名字进行 hash 处理，开启后，可以规避中文或一些特殊字符的平台兼容性问题。
        /// </summary>
        public bool bundleNameWithHash;

        /// <summary>
        ///     bundle 的扩展名，例如：.bundle, .unity3d, .ab, 团队版不用给 bundle 加 扩展名。
        /// </summary>
        public string bundleExtension;

        [Header("Player")] public List<PlayerConfig> playerConfigs = new List<PlayerConfig>();
        public int buildPlayerConfigIndex;
        public bool offlineMode;
        public bool binaryMode;
        public ScriptPlayMode scriptPlayMode = ScriptPlayMode.Simulation;

        public static string PlatformBuildPath
        {
            get
            {
                var dir = $"{Utility.buildPath}/{GetPlatformName()}";
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                return dir;
            }
        }

        public static string BuildPlayerDataPath => $"{Application.streamingAssetsPath}/{Utility.buildPath}";

        public void Initialize()
        {
            BundleExtension = bundleExtension;
            BundleNameWithHash = bundleNameWithHash;
            AppendCRCToBundleName = appendCRCToBundleName;
            ExcludeFiles = excludeFiles;
        }


        public static PlayerSettings GetPlayerSettings()
        {
            return LoadAsset<PlayerSettings>("Assets/Resources/PlayerSettings.asset");
        }

        public static Settings GetDefaultSettings()
        {
            var guilds = AssetDatabase.FindAssets($"t:{typeof(Settings).FullName}");
            foreach (var guild in guilds)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guild);
                if (string.IsNullOrEmpty(assetPath))
                {
                    continue;
                }
                var settings = LoadAsset<Settings>(assetPath);
                if (settings == null)
                {
                    continue;
                }
                return settings;
            }

            return LoadAsset<Settings>("Assets/Settings.asset");
        }

        public List<ManifestBundle> GetBundlesInBuild()
        {
            var bundles = new List<ManifestBundle>();
            var builds = Build.GetAllBuilds();
            if (buildPlayerConfigIndex >= 0 && buildPlayerConfigIndex < playerConfigs.Count)
            {
                var playerConfig = playerConfigs[buildPlayerConfigIndex];
                foreach (var build in builds)
                {
                    var manifest = Build.GetManifest(build.name);
                    if (!playerConfig.splitBuildWithGroup)
                    {
                        bundles.AddRange(manifest.bundles);
                    }
                    else
                    {
                        var set = new HashSet<string>();
                        if (!playerConfig.blacklistMode)
                        {
                            foreach (var item in playerConfig.assetGroup.assets)
                            {
                                if (manifest.IsDirectory(item))
                                {
                                    set.UnionWith(manifest.GetAssetsWithDirectory(item, true));
                                }
                                else
                                {
                                    set.Add(item);
                                }
                            }
                        }
                        else
                        {
                            foreach (var bundle in manifest.bundles)
                            {
                                set.UnionWith(bundle.assets);
                            }
                            foreach (var item in playerConfig.assetGroup.assets)
                            {
                                if (manifest.IsDirectory(item))
                                {
                                    set.ExceptWith(manifest.GetAssetsWithDirectory(item, true));
                                }
                                else
                                {
                                    set.Remove(item);
                                }
                            }
                        }

                        foreach (var asset in set)
                        {
                            var bundle = manifest.GetBundle(asset);
                            if (bundle != null)
                            {
                                if (!bundles.Contains(bundle))
                                {
                                    bundles.Add(bundle);
                                }
                                foreach (var dependency in manifest.GetDependencies(bundle))
                                {
                                    if (!bundles.Contains(dependency))
                                    {
                                        bundles.Add(dependency);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                foreach (var build in builds)
                {
                    var manifest = Build.GetManifest(build.name);
                    bundles.AddRange(manifest.bundles);
                }
            }

            return bundles;
        }

        public static string GetBuildPath(string file)
        {
            return $"{PlatformBuildPath}/{file}";
        }

        public static string GetPlatformName()
        {
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android)
            {
                return "Android";
            }
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.StandaloneOSX)
            {
                return "OSX";
            }
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.StandaloneWindows ||
                EditorUserBuildSettings.activeBuildTarget == BuildTarget.StandaloneWindows64)
            {
                return "Windows";
            }
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.iOS)
            {
                return "iOS";
            }
            return EditorUserBuildSettings.activeBuildTarget == BuildTarget.WebGL
                ? "WebGL"
                : Utility.unsupported;
        }

        public static void PingWithSelected(Object target)
        {
            Selection.activeObject = target;
            EditorGUIUtility.PingObject(target);
        }

        public static T LoadAsset<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                return asset;
            }

            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            asset = CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        public static void SaveAsset(Object asset)
        {
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
        }
    }
}