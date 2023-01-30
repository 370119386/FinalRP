using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace VEngine.Editor.Builds
{
    public class BuildTask
    {
        private readonly List<Asset> assetsToHandleDependencies = new List<Asset>();
        private readonly bool autoGroup;
        private readonly BundleMode autoGroupBundleMode;
        private readonly BuildAssetBundleOptions buildAssetBundleOptions;
        private readonly List<Asset> bundledAssets = new List<Asset>();
        private readonly List<Group> groups;
        public readonly string name;
        private readonly bool packBinary;
        private readonly Dictionary<string, Asset> pathWithAssets = new Dictionary<string, Asset>();
        private readonly List<Asset> rawAssets = new List<Asset>();
        private readonly int version;
        private readonly Stopwatch watch = new Stopwatch();

        public BuildTask(Build build, int buildVersion = -1) : this(build.name)
        {
            Settings.GetDefaultSettings().Initialize();
            version = buildVersion;
            groups = build.groups;
            autoGroup = build.autoGroup;
            autoGroupBundleMode = build.autoGroupBundleMode;
            buildAssetBundleOptions = Settings.AppendCRCToBundleName ? build.buildAssetBundleOptions : build.buildAssetBundleOptions | BuildAssetBundleOptions.AppendHashToAssetBundleName;
            packBinary = build.packBinary;
        }

        public BuildTask(string buildName)
        {
            name = buildName;
            watch.Start();
        }

        public Record record { get; private set; }

        private static string GetRecordsPath(string buildName)
        {
            return Settings.GetBuildPath($"build_records_for_{buildName}.json");
        }

        public List<Asset> CollectAssets()
        {
            var assets = new List<Asset>();
            foreach (var item in groups)
            {
                assets.AddRange(item.Collect());
            }
            watch.Stop();
            Debug.LogFormat("CollectAssets for {0} with {1} seconds", name, watch.ElapsedMilliseconds / 1000);
            return assets;
        }


        private static void WriteRecord(Record record)
        {
            var records = GetRecords(record.build);
            records.data.Insert(0, record);
            File.WriteAllText(GetRecordsPath(record.build), JsonUtility.ToJson(records));
        }

        private static Records GetRecords(string build)
        {
            var records = ScriptableObject.CreateInstance<Records>();
            var path = GetRecordsPath(build);
            if (File.Exists(path))
            {
                JsonUtility.FromJsonOverwrite(File.ReadAllText(path), records);
            }

            return records;
        }

        private static void DisplayProgressBar(string title, string content, int index, int max)
        {
            EditorUtility.DisplayProgressBar($"{title}({index}/{max}) ", content,
                index * 1f / max);
        }

        public void BuildBundles()
        {
            for (var i = 0; i < groups.Count; i++)
            {
                var group = groups[i];
                if (!group.active)
                {
                    continue;
                }

                DisplayProgressBar("采集资源", group.name, i, groups.Count);
                var assets = group.Collect();
                if (group.bundleMode == BundleMode.PackByRaw)
                {
                    rawAssets.AddRange(assets);
                }
                else
                {
                    if (group.handleDependencies)
                    {
                        assetsToHandleDependencies.AddRange(assets);
                    }

                    bundledAssets.AddRange(assets);
                }
            }

            CheckAssets();

            if (autoGroup)
            {
                AutoGrouping();
            }

            EditorUtility.ClearProgressBar();
            FinishBuild();
            watch.Stop();
            Debug.LogFormat("BuildBundles for {0} with {1} seconds", name, watch.ElapsedMilliseconds / 1000);
        }

        private void CheckAssets()
        {
            for (var i = 0; i < bundledAssets.Count; i++)
            {
                var asset = bundledAssets[i];
                if (!pathWithAssets.TryGetValue(asset.path, out var ba))
                {
                    pathWithAssets[asset.path] = asset;
                }
                else
                {
                    bundledAssets.RemoveAt(i);
                    i--;
                    Debug.LogWarningFormat("{0} can't pack with {1}, because already pack to {2}", asset.path,
                        asset.bundle, ba.bundle);
                }
            }
        }

        private void FinishBuild()
        {
            var bundles = new List<ManifestBundle>();
            var dictionary = new Dictionary<string, List<string>>();
            foreach (var asset in bundledAssets)
            {
                if (!dictionary.TryGetValue(asset.bundle, out var assets))
                {
                    assets = new List<string>();
                    dictionary.Add(asset.bundle, assets);
                    bundles.Add(new ManifestBundle
                    {
                        name = asset.bundle,
                        assets = assets
                    });
                }

                assets.Add(asset.path);
            }

            var outputPath = Settings.PlatformBuildPath;
            AssetBundleManifest manifest = null;
            if (bundles.Count > 0)
            {
                manifest = BuildPipeline.BuildAssetBundles(outputPath, bundles.ConvertAll(bundle =>
                        new AssetBundleBuild
                        {
                            assetNames = bundle.assets.ToArray(),
                            assetBundleName = bundle.name
                        }).ToArray(),
                    buildAssetBundleOptions,
                    EditorUserBuildSettings.activeBuildTarget);

                if (manifest == null)
                {
                    Debug.LogErrorFormat("Failed to build {0}.", name);
                    return;
                }
            }

            AfterBuildBundles(bundles, manifest);
        }


        private static string GetOriginBundle(string assetBundle)
        {
            var pos = assetBundle.LastIndexOf("_", StringComparison.Ordinal) + 1;
            var hash = assetBundle.Substring(pos);
            if (!string.IsNullOrEmpty(Settings.BundleExtension))
            {
                hash = hash.Replace(Settings.BundleExtension, "");
            }
            var originBundle = $"{assetBundle.Replace("_" + hash, "")}";
            return originBundle;
        }

        private void AfterBuildBundles(List<ManifestBundle> bundles, AssetBundleManifest manifest)
        {
            if (rawAssets.Count > 0)
            {
                bundles.AddRange(rawAssets.ConvertAll(asset =>
                {
                    var crc = Utility.ComputeCRC32(asset.path);
                    var bundle = new ManifestBundle
                    {
                        crc = crc,
                        name = asset.bundle,
                        nameWithAppendHash = asset.bundle,
                        assets = new List<string>
                        {
                            asset.path
                        }
                    };
                    var file = new FileInfo(asset.path);
                    if (file.Exists)
                    {
                        bundle.size = file.Length;
                        var path = Settings.GetBuildPath(asset.bundle);
                        if (!File.Exists(path))
                        {
                            file.CopyTo(path);
                        }
                    }
                    return bundle;
                }));
            }

            var nameWithBundles = new Dictionary<string, ManifestBundle>();
            for (var i = 0; i < bundles.Count; i++)
            {
                var bundle = bundles[i];
                bundle.id = i;
                nameWithBundles[bundle.name] = bundle;
            }

            if (manifest != null)
            {
                var assetBundles = manifest.GetAllAssetBundles();
                if (Settings.AppendCRCToBundleName)
                {
                    foreach (var assetBundle in assetBundles)
                    {
                        if (nameWithBundles.TryGetValue(assetBundle, out var manifestBundle))
                        {
                            manifestBundle.nameWithAppendHash = assetBundle;
                            manifestBundle.dependencies = Array.ConvertAll(manifest.GetAllDependencies(assetBundle), input => nameWithBundles[input].id);
                            var file = Settings.GetBuildPath(assetBundle);
                            if (File.Exists(file))
                            {
                                using (var stream = File.OpenRead(file))
                                {
                                    manifestBundle.size = stream.Length;
                                    manifestBundle.crc = Utility.ComputeCRC32(stream);
                                }
                                var dir = Path.GetDirectoryName(file);
                                var newName = $"{Path.GetFileNameWithoutExtension(file)}_{manifestBundle.crc}{Settings.BundleExtension}";
                                manifestBundle.nameWithAppendHash = newName;
                                File.Copy(file, $"{dir}/{newName}", true);
                            }
                            else
                            {
                                Debug.LogErrorFormat("File not found: {0}", file);
                            }
                        }
                        else
                        {
                            Debug.LogErrorFormat("Bundle not exist: {0}", assetBundle);
                        }
                    }
                }
                else
                {
                    foreach (var assetBundle in assetBundles)
                    {
                        var originBundle = GetOriginBundle(assetBundle);
                        var dependencies = Array.ConvertAll(manifest.GetAllDependencies(assetBundle), GetOriginBundle);
                        if (nameWithBundles.TryGetValue(originBundle, out var manifestBundle))
                        {
                            manifestBundle.nameWithAppendHash = assetBundle;
                            manifestBundle.dependencies = Array.ConvertAll(dependencies, input => nameWithBundles[input].id);
                            var file = Settings.GetBuildPath(assetBundle);
                            if (File.Exists(file))
                            {
                                using (var stream = File.OpenRead(file))
                                {
                                    manifestBundle.size = stream.Length;
                                    manifestBundle.crc = Utility.ComputeCRC32(stream);
                                }
                            }
                            else
                            {
                                Debug.LogErrorFormat("File not found: {0}", file);
                            }
                        }
                        else
                        {
                            Debug.LogErrorFormat("Bundle not exist: {0}", originBundle);
                        }
                    }
                }
            }
            CreateManifest(bundles);
        }

        public void CreateManifest(List<ManifestBundle> bundles)
        {
            var manifest = Build.GetManifest(name);
            if (version > 0)
            {
                manifest.version = version;
            }
            else
            {
                manifest.version++;
            }

            manifest.appVersion = UnityEditor.PlayerSettings.bundleVersion;
            var getBundles = new Dictionary<string, ManifestBundle>();
            foreach (var bundle in manifest.bundles)
            {
                getBundles[bundle.name] = bundle;
            }
            var newFiles = new List<string>();
            var newSize = 0L;
            foreach (var bundle in bundles)
            {
                if (getBundles.TryGetValue(bundle.name, out var value) &&
                    value.nameWithAppendHash == bundle.nameWithAppendHash)
                {
                    continue;
                }
                newFiles.Add(bundle.nameWithAppendHash);
                newSize += bundle.size;
            }

            manifest.bundles = bundles;
            var newFilesSize = Utility.FormatBytes(newSize);
            newFiles.AddRange(WriteManifest(manifest));
            // write upload files
            var filename = Settings.GetBuildPath($"upload_files_for_{manifest.name}.txt");
            File.WriteAllText(filename, string.Join("\n", newFiles.ToArray()));
            record = new Record
            {
                build = name,
                version = manifest.version,
                files = newFiles,
                size = newSize,
                time = DateTime.Now.ToFileTime()
            };
            WriteRecord(record);
            Debug.LogFormat("Build bundles with {0}({1}) files with version {2} for {3}.", newFiles.Count, newFilesSize,
                manifest.version, manifest.name);
            if (packBinary)
            {
                PackBinary(Settings.PlatformBuildPath, manifest);
            }
        }

        private static void PackBinary(string dir, Manifest manifest)
        {
            var filename = $"{dir}/{manifest.name}.bin";
            if (File.Exists(filename))
            {
                File.Delete(filename);
            }

            using (var stream = File.Open(filename, FileMode.CreateNew, FileAccess.Write))
            {
                using (var writer = new BinaryWriter(stream))
                {
                    foreach (var bundle in manifest.bundles)
                    {
                        var assetPath = $"{dir}/{bundle.nameWithAppendHash}";
                        if (string.IsNullOrEmpty(bundle.nameWithAppendHash))
                        {
                            continue;
                        }

                        var bytes = File.ReadAllBytes(assetPath);
                        writer.Write(bundle.name);
                        writer.Write(bundle.nameWithAppendHash);
                        writer.Write(bundle.crc);
                        writer.Write(bundle.size);
                        writer.Write(bytes);
                    }
                }
            }
        }


        private static IEnumerable<string> WriteManifest(Manifest manifest)
        {
            var newFiles = new List<string>();
            var filename = $"{manifest.name}";
            var version = manifest.version;
            WriteJson(manifest, filename, newFiles);
            var path = Settings.GetBuildPath(filename);
            var crc = Utility.ComputeCRC32(path);
            var info = new FileInfo(path);
            WriteJson(manifest, $"{filename}_v{version}_{crc}", newFiles);
            // for version file
            var manifestVersion = ScriptableObject.CreateInstance<ManifestVersion>();
            manifestVersion.crc = crc;
            manifestVersion.size = info.Length;
            manifestVersion.version = version;
            manifestVersion.appVersion = manifest.appVersion;
            WriteJson(manifestVersion, Manifest.GetVersionFile(filename), newFiles);
            WriteJson(manifestVersion, $"{filename}_v{version}_{crc}.version", newFiles);
            return newFiles;
        }

        private static void WriteJson(ScriptableObject so, string file, List<string> newFiles)
        {
            newFiles.Add(file);
            var json = JsonUtility.ToJson(so);
            File.WriteAllText(Settings.GetBuildPath(file), json);
        }

        public static IEnumerable<string> GetDependencies(string path)
        {
            var set = new HashSet<string>(AssetDatabase.GetDependencies(path, true));
            set.Remove(path);
            set.RemoveWhere(s => Settings.ExcludeFiles.Exists(s.EndsWith));
            return set;
        }

        private void AutoGrouping()
        {
            var dependencyWithAssets = new Dictionary<string, List<Asset>>();
            var settings = Settings.GetDefaultSettings();
            for (var i = 0; i < assetsToHandleDependencies.Count; i++)
            {
                var asset = assetsToHandleDependencies[i];
                var dependencies = GetDependencies(asset.path);
                DisplayProgressBar("分析依赖", asset.path, i, assetsToHandleDependencies.Count);
                foreach (var dependency in dependencies)
                {
                    var extension = Path.GetExtension(dependency);
                    if (extension.Equals(".cs") || settings.excludeFiles.Exists(dependency.EndsWith))
                    {
                        continue;
                    }

                    if (pathWithAssets.ContainsKey(dependency))
                    {
                        continue;
                    }
                    if (!dependencyWithAssets.TryGetValue(dependency, out var assets))
                    {
                        assets = new List<Asset>();
                        dependencyWithAssets[dependency] = assets;
                    }
                    assets.Add(asset);
                }
            }

            if (dependencyWithAssets.Count > 0)
            {
                var builder = new StringBuilder();
                var dependencies = new List<string>(dependencyWithAssets.Keys);
                dependencies.Sort();
                for (var i = 0; i < dependencies.Count; i++)
                {
                    var key = dependencies[i];
                    DisplayProgressBar("自动分组", key, i, dependencies.Count);
                    var value = dependencyWithAssets[key];
                    var set = new List<string>();
                    foreach (var item in value)
                    {
                        if (!set.Contains(item.bundle))
                        {
                            set.Add(item.bundle);
                        }
                    }

                    set.Sort();
                    builder.AppendLine(key);
                    foreach (var bundle in set)
                    {
                        builder.AppendLine($" - {bundle}");
                    }

                    if (pathWithAssets.ContainsKey(key))
                    {
                        continue;
                    }

                    var asset = new Asset
                    {
                        path = key,
                        bundle = Group.GetBundle(key, key, autoGroupBundleMode, "Auto")
                    };
                    pathWithAssets.Add(key, asset);
                    bundledAssets.Add(asset);
                }

                var file = $"AutoCollectedDependenciesFor{name}.txt";
                File.WriteAllText(file, builder.ToString());
            }
        }
    }
}