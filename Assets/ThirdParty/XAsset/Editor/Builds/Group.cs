using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using Object = UnityEngine.Object;

namespace VEngine.Editor.Builds
{
    [Serializable]
    public class Group
    {
        public string name;
        public Object target;
        public string filter;
        public bool active = true;
        public bool handleDependencies;
        public BundleMode bundleMode = BundleMode.PackByFile;

        public string TargetPath => target == null ? string.Empty : AssetDatabase.GetAssetPath(target);

        public static Func<string, string, string, string> customPacker { get; set; }
        public static Func<string, bool> customFilter { get; set; }

        public static string GetDirectoryName(string path)
        {
            var dir = Path.GetDirectoryName(path);
            return !string.IsNullOrEmpty(dir) ? dir.Replace("\\", "/") : string.Empty;
        }

        public static string GetBundle(string assetPath, string rootPath, BundleMode bundleMode, string group)
        {
            var bundle = string.Empty;
            if (assetPath.EndsWith(".unity"))
            {
                bundleMode = BundleMode.PackByFile;
            }

            switch (bundleMode)
            {
                case BundleMode.PackTogether:
                    bundle = group;
                    break;
                case BundleMode.PackByDirectory:
                    bundle = GetDirectoryName(assetPath);
                    break;
                case BundleMode.PackByFile:
                    bundle = assetPath;
                    break;
                case BundleMode.PackByTopDirectory:
                    if (!string.IsNullOrEmpty(rootPath))
                    {
                        var pos = assetPath.IndexOf("/", rootPath.Length + 1, StringComparison.Ordinal);
                        bundle = pos != -1 ? assetPath.Substring(0, pos) : rootPath;
                    }

                    break;
                case BundleMode.PackByRaw:
                    var crc = Utility.ComputeCRC32(assetPath);
                    bundle = $"{Path.GetFileNameWithoutExtension(assetPath)}_{crc}";
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            bundle = FixedBundle(bundle);
            if (customPacker != null)
            {
                bundle = customPacker(assetPath, bundle, group);
            }

            var extension = Settings.BundleExtension;
            if (Settings.BundleNameWithHash)
            {
                return Utility.GetMD5(bundle) + extension;
            }

            return bundle + extension;
        }

        public static string FixedBundle(string bundle)
        {
            return bundle.Replace(" ", "").Replace("/", "_").Replace("-", "_").Replace(".", "_").ToLower();
        }


        public string GetBundle(string assetPath)
        {
            return GetBundle(assetPath, TargetPath, bundleMode, name);
        }

        public string[] GetFiles()
        {
            if (target == null)
            {
                return Array.Empty<string>();
            }

            var assetPath = AssetDatabase.GetAssetPath(target);
            if (string.IsNullOrEmpty(assetPath))
            {
                return Array.Empty<string>();
            }

            if (Directory.Exists(assetPath))
            {
                var guilds = AssetDatabase.FindAssets(filter, new[]
                {
                    assetPath
                });
                var set = new HashSet<string>();
                foreach (var guild in guilds)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guild);
                    if (string.IsNullOrEmpty(path)
                        || Directory.Exists(path)
                        || path.EndsWith(".cs")
                        || Settings.ExcludeFiles.Exists(s => path.EndsWith(s))
                        || customFilter != null && customFilter(path))
                    {
                        continue;
                    }
                    set.Add(path);
                }
                return set.ToArray();
            }

            return new[]
            {
                assetPath
            };
        }

        public Asset[] Collect()
        {
            var getFiles = GetFiles();
            if (bundleMode == BundleMode.PackByRaw)
            {
                return Array.ConvertAll(getFiles, input =>
                {
                    var assetPath = input.Replace("\\", "/");
                    var bundle = GetBundle(assetPath);
                    var asset = new Asset
                    {
                        path = assetPath,
                        bundle = bundle
                    };
                    return asset;
                });
            }

            return Array.ConvertAll(getFiles, input =>
            {
                var asset = new Asset
                {
                    path = input,
                    bundle = GetBundle(input)
                };
                return asset;
            });
        }
    }
}