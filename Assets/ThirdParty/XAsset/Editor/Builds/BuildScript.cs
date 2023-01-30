using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VEngine.Editor.Builds
{
    public static class BuildScript
    {
        public static Action<BuildTask> postprocessBuildBundles;
        public static Action<BuildTask> preprocessBuildBundles;

        public static void BuildBundles(BuildTask task)
        {
            preprocessBuildBundles?.Invoke(task);
            task.BuildBundles();
            postprocessBuildBundles?.Invoke(task);
        }

        public static void BuildBundles()
        {
            var tasks = new List<BuildTask>();
            var builds = Build.GetAllBuilds();
            foreach (var build in builds)
            {
                tasks.Add(new BuildTask(build));
            }
            foreach (var task in tasks)
            {
                BuildBundles(task);
            }
        }

        private static string GetTimeForNow()
        {
            return DateTime.Now.ToString("yyyyMMdd-HHmmss");
        }

        private static string GetBuildTargetName(BuildTarget target)
        {
            var productName = "xc" + "-v" + UnityEditor.PlayerSettings.bundleVersion + ".";
            var targetName = $"/{productName}-{GetTimeForNow()}";
            switch (target)
            {
                case BuildTarget.Android:
                    return targetName + ".apk";
                case BuildTarget.StandaloneWindows:
                case BuildTarget.StandaloneWindows64:
                    return targetName + ".exe";
                case BuildTarget.StandaloneOSX:
                    return targetName + ".app";
                default:
                    return targetName;
            }
        }

        public static void BuildPlayer()
        {
            var path = Path.Combine(Environment.CurrentDirectory, "Build");
            if (path.Length == 0)
            {
                return;
            }
            BuildPlayer(path);
        }

        public static void BuildPlayer(string path)
        {
            var levels = new List<string>();
            foreach (var scene in EditorBuildSettings.scenes)
            {
                if (scene.enabled)
                {
                    levels.Add(scene.path);
                }
            }

            if (levels.Count == 0)
            {
                Debug.Log("Nothing to build.");
                return;
            }

            var buildTarget = EditorUserBuildSettings.activeBuildTarget;
            var buildTargetName = GetBuildTargetName(buildTarget);
            if (buildTargetName == null)
            {
                return;
            }

            var buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = levels.ToArray(),
                locationPathName = path + buildTargetName,
                target = buildTarget,
                options = EditorUserBuildSettings.development
                    ? BuildOptions.Development
                    : BuildOptions.None
            };
            BuildPipeline.BuildPlayer(buildPlayerOptions);
        }

        public static void CopyToStreamingAssets()
        {
            var settings = Settings.GetDefaultSettings();
            var destinationDir = Settings.BuildPlayerDataPath;
            if (Directory.Exists(destinationDir))
            {
                Directory.Delete(destinationDir, true);
            }

            Directory.CreateDirectory(destinationDir);
            var bundles = settings.GetBundlesInBuild();
            var builds = Build.GetAllBuilds();
            if (!settings.binaryMode)
            {
                foreach (var bundle in bundles)
                {
                    var destFile = Path.Combine(Settings.BuildPlayerDataPath, bundle.nameWithAppendHash);
                    var srcFile = Settings.GetBuildPath(bundle.nameWithAppendHash);
                    if (!File.Exists(srcFile))
                    {
                        Debug.LogWarningFormat("Bundle not found: {0}", bundle.name);
                        continue;
                    }

                    var dir = Path.GetDirectoryName(destFile);
                    if (!Directory.Exists(dir) && !string.IsNullOrEmpty(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    File.Copy(srcFile, destFile, true);
                }
            }
            else
            {
                var filename = Settings.GetBuildPath(Versions.BinaryData);
                if (File.Exists(filename))
                {
                    File.Delete(filename);
                }
                using (var stream = File.Open(filename, FileMode.CreateNew, FileAccess.Write))
                {
                    using (var writer = new BinaryWriter(stream))
                    {
                        foreach (var bundle in bundles)
                        {
                            var assetPath = Settings.GetBuildPath(bundle.nameWithAppendHash);
                            if (!File.Exists(assetPath))
                            {
                                Debug.LogWarningFormat("Bundle not found: {0}", bundle.name);
                                continue;
                            }

                            var bytes = File.ReadAllBytes(assetPath);
                            writer.Write(bundle.nameWithAppendHash);
                            writer.Write(bundle.crc);
                            writer.Write(bundle.size);
                            bundle.offset = (ulong)writer.BaseStream.Position;
                            writer.Write(bytes);
                        }
                    }
                }

                File.Copy(filename, Path.Combine(Settings.BuildPlayerDataPath, Versions.BinaryData), true);
                File.Delete(filename);
            }

            foreach (var build in builds)
            {
                var manifest = Build.GetManifest(build.name);
                Copy(manifest.name, destinationDir);
                Copy(Manifest.GetVersionFile(manifest.name), destinationDir);
            }

            var config = Settings.GetPlayerSettings();
            config.assets = bundles.ConvertAll(o => new AssetLocation
            {
                name = o.nameWithAppendHash,
                offset = o.offset
            });
            config.manifests = Array.ConvertAll(builds, build => build.name);
            config.offlineMode = settings.offlineMode;
            config.binaryMode = settings.binaryMode;
            config.version = GetTimeForNow();
            Settings.SaveAsset(config);
        }

        private static void Copy(string filename, string destinationDir)
        {
            var from = Settings.GetBuildPath(filename);
            if (File.Exists(from))
            {
                var dest = $"{destinationDir}/{filename}";
                File.Copy(from, dest, true);
            }
            else
            {
                Debug.LogErrorFormat("File not found: {0}", from);
            }
        }

        public static void ClearBuildFromSelection()
        {
            var filtered = Selection.GetFiltered<Object>(SelectionMode.DeepAssets);
            var assetPaths = new List<string>();
            foreach (var o in filtered)
            {
                var assetPath = AssetDatabase.GetAssetPath(o);
                if (string.IsNullOrEmpty(assetPath))
                {
                    continue;
                }
                assetPaths.Add(assetPath);
            }

            var builds = Build.GetAllBuilds();
            var bundles = new List<string>();
            foreach (var build in builds)
            {
                var manifest = Build.GetManifest(build.name);
                foreach (var assetPath in assetPaths)
                {
                    var bundle = manifest.GetBundle(assetPath);
                    if (bundle != null)
                    {
                        bundles.Add(bundle.nameWithAppendHash);
                    }
                }
            }

            foreach (var bundle in bundles)
            {
                var file = Settings.GetBuildPath(bundle);
                if (File.Exists(file))
                {
                    File.Delete(file);
                    Debug.LogFormat("Delete:{0}", file);
                }
            }
        }

        public static void ClearHistory()
        {
            var usedFiles = new List<string>
            {
                Settings.GetPlatformName(),
                Settings.GetPlatformName() + ".manifest"
            };
            foreach (var build in Build.GetAllBuilds())
            {
                var manifest = Build.GetManifest(build.name);
                usedFiles.Add($"{build.name}");
                usedFiles.Add($"{build.name}.version");
                var version = ManifestVersion.Load(Settings.GetBuildPath($"{build.name}.version"));
                usedFiles.Add($"{build.name}_v{version.version}_{version.crc}");
                usedFiles.Add($"{build.name}_v{version.version}_{version.crc}.version");
                if (build.packBinary)
                {
                    usedFiles.Add($"{build.name}.bin");
                }

                foreach (var bundle in manifest.bundles)
                {
                    usedFiles.Add(bundle.nameWithAppendHash);
                    usedFiles.Add($"{bundle.name}.manifest");
                    if (Settings.AppendCRCToBundleName)
                    {
                        usedFiles.Add(bundle.name);
                        usedFiles.Add($"{bundle.name}.manifest");
                    }
                }
            }

            var files = Directory.GetFiles(Settings.PlatformBuildPath);
            foreach (var file in files)
            {
                var name = Path.GetFileName(file);
                if (usedFiles.Contains(name))
                {
                    continue;
                }

                File.Delete(file);
                Debug.LogFormat("Delete {0}", file);
            }
        }
    }
}