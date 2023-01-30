using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace VEngine
{
    [Serializable]
    public class ManifestBundle
    {
        public static ManifestBundle Empty = new ManifestBundle();
        public int id;
        public string name;
        public List<string> assets;
        public long size;
        public uint crc;
        public string nameWithAppendHash;
        public int[] dependencies;
        public ulong offset { get; set; }
    }

    public class Manifest : ScriptableObject
    {
        public static bool SaveAssetsWithDirectory = false;
        public int version;
        public string appVersion;
        public List<ManifestBundle> bundles = new List<ManifestBundle>();
        private Dictionary<string, List<string>> directoryWithPaths = new Dictionary<string, List<string>>();
        private Dictionary<string, ManifestBundle> nameWithBundles = new Dictionary<string, ManifestBundle>();
        public Action<string> onReadAsset;

        public bool Contains(string assetPath)
        {
            return nameWithBundles.ContainsKey(assetPath);
        }

        public ManifestBundle GetBundle(string assetPath)
        {
            return nameWithBundles.TryGetValue(assetPath, out var manifestBundle) ? manifestBundle : null;
        }

        public ManifestBundle[] GetDependencies(ManifestBundle bundle)
        {
            return bundle == null
                ? Array.Empty<ManifestBundle>()
                : Array.ConvertAll(bundle.dependencies, input => bundles[input]);
        }

        public void Override(Manifest manifest)
        {
            version = manifest.version;
            bundles = manifest.bundles;

            nameWithBundles = manifest.nameWithBundles;
            appVersion = manifest.appVersion;
            version = manifest.version;
            directoryWithPaths = manifest.directoryWithPaths;
        }

        public static string GetVersionFile(string file)
        {
            return $"{file}.version";
        }

        public void Load(string path)
        {
            var json = File.ReadAllText(path);
            JsonUtility.FromJsonOverwrite(json, this);
            nameWithBundles.Clear();
            if (onReadAsset != null)
            {
                foreach (var bundle in bundles)
                {
                    nameWithBundles[bundle.name] = bundle;
                    foreach (var asset in bundle.assets)
                    {
                        nameWithBundles[asset] = bundle;
                        onReadAsset.Invoke(asset);
                        AddPathToDirectory(asset);
                    }
                }
            }
            else
            {
                foreach (var bundle in bundles)
                {
                    nameWithBundles[bundle.name] = bundle;
                    //Logger.F($"[nameWithBundles]:[{bundle.name}]=>[{bundle.nameWithAppendHash}]");
                    foreach (var asset in bundle.assets)
                    {
                        nameWithBundles[asset] = bundle;
                        //Logger.F($"[nameWithBundles]:[{asset}]=>[{bundle.nameWithAppendHash}]");
                        AddPathToDirectory(asset);
                    }
                }
            }
        }

        public bool IsDirectory(string path)
        {
            return directoryWithPaths.ContainsKey(path);
        }

        public string[] GetAssetsWithDirectory(string dir, bool recursion)
        {
            if (recursion)
            {
                var dirs = new List<string>();
                foreach (var item in directoryWithPaths.Keys)
                {
                    if (item.StartsWith(dir) && (item.Length == dir.Length ||
                                                 item.Length > dir.Length && item[dir.Length] == '/'))
                    {
                        dirs.Add(item);
                    }
                }

                if (dirs.Count > 0)
                {
                    var assets = new List<string>();
                    foreach (var item in dirs)
                    {
                        assets.AddRange(GetAssetsWithDirectory(item, false));
                    }

                    return assets.ToArray();
                }
            }
            else
            {
                if (directoryWithPaths.TryGetValue(dir, out var value))
                {
                    return value.ToArray();
                }
            }

            return Array.Empty<string>();
        }

        private void AddPathToDirectory(string asset)
        {
            if (!SaveAssetsWithDirectory)
            {
                return;
            }
            var dir = Path.GetDirectoryName(asset)?.Replace('\\', '/');
            if (dir == null || directoryWithPaths.TryGetValue(dir, out var value))
            {
                return;
            }
            value = new List<string>();
            directoryWithPaths.Add(dir, value);
            int pos;
            while ((pos = dir.LastIndexOf('/')) != -1)
            {
                dir = dir.Substring(0, pos);
                if (!directoryWithPaths.TryGetValue(dir, out _))
                {
                    directoryWithPaths.Add(dir, new List<string>());
                }
            }

            value.Add(asset);
        }

        public void AddAsset(string assetPath)
        {
            nameWithBundles[assetPath] = ManifestBundle.Empty;
            AddPathToDirectory(assetPath);
        }
    }
}