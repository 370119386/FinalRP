using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace VEngine
{
    /// <summary>
    ///     Versions 类，持有运行时的所有资源的版本信息和依赖关系。
    /// </summary>
    public static partial class Versions
    {
        public const string APIVersion = "7.3";
        public const string BinaryData = "data.bin";
        public const string AppVersion = "AppVersion";

        public static readonly List<Manifest> Manifests = new List<Manifest>();
        public static readonly Dictionary<string, AssetLocation> builtinAssets = new Dictionary<string, AssetLocation>();

        /// <summary>
        ///     是否是仿真模式
        /// </summary>
        public static bool SimulationMode { get; internal set; }

        /// <summary>
        ///     是否是加密模式
        /// </summary>
        public static bool BinaryMode { get; internal set; }

        /// <summary>
        ///     是否是离线模式
        /// </summary>
        public static bool OfflineMode { get; internal set; }

        /// <summary>
        ///     获取清单的版本号
        /// </summary>
        public static string ManifestsVersion
        {
            get
            {
                var sb = new StringBuilder();
                for (var index = 0; index < Manifests.Count; index++)
                {
                    var manifest = Manifests[index];
                    sb.Append(manifest.version);
                    if (index < Manifests.Count - 1)
                    {
                        sb.Append(".");
                    }
                }

                return sb.ToString();
            }
        }

        public static void Override(Manifest manifest)
        {
            var key = manifest.name;
            if (NameWithManifests.TryGetValue(key, out var value))
            {
                value.Override(manifest);
                return;
            }

            Manifests.Add(manifest);
            NameWithManifests.Add(key, manifest);
        }

        /// <summary>
        ///     清理历史版本数据
        /// </summary>
        /// <returns></returns>
        public static ClearHistory ClearAsync()
        {
            var clearAsync = new ClearHistory();
            clearAsync.Start();
            return clearAsync;
        }

        /// <summary>
        ///     清理所有下载数据
        /// </summary>
        public static void ClearDownload()
        {
            if (Directory.Exists(DownloadDataPath))
            {
                Directory.Delete(DownloadDataPath, true);
            }
        }

        /// <summary>
        ///     初始化，主要加载本地的清单文件，默认会优先加载下载目录的，如果下载目录的存在并且版本号比较新，就不会加载包内的清单了。
        ///     所以打包的时候，不要中断版本号，否则覆盖安装可能会出现新包加载旧资源的情况
        /// </summary>
        /// <returns></returns>
        public static InitializeVersions InitializeAsync()
        {
            var operation = new InitializeVersions();
            operation.Start();
            return operation;
        }

        /// <summary>
        ///     更新清单文件，可以使用带 hash 的版本。
        /// </summary>
        /// <param name="manifests"></param>
        /// <returns></returns>
        public static UpdateVersions UpdateAsync(params string[] manifests)
        {
            var operation = new UpdateVersions
            {
                manifests = manifests
            };
            operation.Start();
            return operation;
        }

        /// <summary>
        ///     根据资源名称获取更新大小
        /// </summary>
        /// <param name="assetNames">资源名称，可以是加载的相对路径，也可以是不带hash的bundle名字</param>
        /// <returns></returns>
        public static GetDownloadSize GetDownloadSizeAsync(params string[] assetNames)
        {
            var getDownloadSize = new GetDownloadSize();
            getDownloadSize.bundles.AddRange(GetBundlesWithAssets(Manifests, assetNames));
            getDownloadSize.Start();
            return getDownloadSize;
        }

        /// <summary>
        ///     获取一个清单的更新大小。
        /// </summary>
        /// <param name="manifest">清单名字（不带hash）</param>
        /// <returns></returns>
        public static GetDownloadSize GetDownloadSizeAsyncWithManifest(string manifest)
        {
            var getDownloadSize = new GetDownloadSize();
            if (NameWithManifests.TryGetValue(manifest, out var value))
            {
                getDownloadSize.bundles.AddRange(value.bundles);
                getDownloadSize.Start();
            }

            return getDownloadSize;
        }

        /// <summary>
        ///     批量下载指定集合的内容。
        /// </summary>
        /// <param name="items">要下载内容</param>
        /// <returns></returns>
        public static DownloadVersions DownloadAsync(params DownloadInfo[] items)
        {
            var download = new DownloadVersions();
            download.items.AddRange(items);
            download.Start();
            return download;
        }

        /// <summary>
        ///     解压二进制文件
        /// </summary>
        /// <param name="name">文件名</param>
        /// <returns></returns>
        public static UnpackBinary UnpackAsync(string name)
        {
            var unpack = new UnpackBinary
            {
                name = name
            };
            unpack.Start();
            return unpack;
        }

        /// <summary>
        ///     判断 bundle 是否已经下载
        /// </summary>
        /// <param name="bundle"></param>
        /// <returns></returns>
        public static bool IsDownloaded(ManifestBundle bundle)
        {
            if (bundle == null)
            {
                return false;
            }
            if (OfflineMode || builtinAssets.ContainsKey(bundle.nameWithAppendHash))
            {
                return true;
            }
            var path = GetDownloadDataPath(bundle.nameWithAppendHash);
            var file = new FileInfo(path);
            return file.Exists && file.Length >= bundle.size && Utility.ComputeCRC32(path) == bundle.crc;
        }

        /// <summary>
        ///     判断加载路径对应的 bundle 是否下载
        /// </summary>
        /// <param name="path">加载路径</param>
        /// <param name="checkDependencies">是否统计依赖</param>
        /// <returns></returns>
        public static bool IsDownloaded(string path, bool checkDependencies)
        {
            if (!checkDependencies || !GetDependencies(path, out var bundle, out var dependencies))
            {
                return IsDownloaded(GetBundle(path));
            }

            foreach (var dependency in dependencies)
            {
                if (!IsDownloaded(dependency))
                {
                    return false;
                }
            }
            return IsDownloaded(bundle);
        }

        /// <summary>
        ///     获取指定资源的依赖
        /// </summary>
        /// <param name="assetPath">加载路径</param>
        /// <param name="mainBundle">主 bundle</param>
        /// <param name="dependencies">依赖的 bundle 集合</param>
        /// <returns></returns>
        public static bool GetDependencies(string assetPath, out ManifestBundle mainBundle, out ManifestBundle[] dependencies)
        {
            foreach (var manifest in Manifests)
            {
                if (manifest.Contains(assetPath))
                {
                    mainBundle = manifest.GetBundle(assetPath);
                    dependencies = manifest.GetDependencies(mainBundle);
                    return true;
                }
            }

            mainBundle = null;
            dependencies = null;
            return false;
        }

        /// <summary>
        ///     判断资源是否包含在当前版本中。
        /// </summary>
        /// <param name="assetPath"></param>
        /// <returns></returns>
        public static bool Contains(string assetPath)
        {
            foreach (var manifest in Manifests)
            {
                if (manifest.Contains(assetPath))
                {
                    return true;
                }
            }
            return false;
        }

        public static ManifestBundle GetBundle(string bundle)
        {
            foreach (var manifest in Manifests)
            {
                var manifestBundle = manifest.GetBundle(bundle);
                if (manifestBundle != null)
                {
                    return manifestBundle;
                }
            }

            return null;
        }

        public static ulong GetOffset(ManifestBundle info)
        {
            return builtinAssets.TryGetValue(info.nameWithAppendHash, out var value) ? value.offset : 0;
        }

        private static bool GetBundles(ICollection<ManifestBundle> bundles, Manifest manifest, string assetPath)
        {
            var bundle = manifest.GetBundle(assetPath);
            if (bundle == null)
            {
                return false;
            }

            if (!bundles.Contains(bundle))
            {
                bundles.Add(bundle);
            }

            foreach (var dependency in manifest.GetDependencies(bundle))
            {
                if (builtinAssets.ContainsKey(dependency.nameWithAppendHash) || bundles.Contains(dependency))
                {
                    continue;
                }
                bundles.Add(dependency);
            }
            return true;
        }

        private static IEnumerable<ManifestBundle> GetBundlesWithAssets(IEnumerable<Manifest> manifests, ICollection<string> assets)
        {
            var bundles = new List<ManifestBundle>();
            if (OfflineMode)
            {
                return bundles;
            }

            if (assets != null && assets.Count != 0)
            {
                foreach (var manifest in manifests)
                foreach (var asset in assets)
                {
                    if (manifest.IsDirectory(asset))
                    {
                        var children = manifest.GetAssetsWithDirectory(asset, true);
                        foreach (var child in children)
                        {
                            if (!GetBundles(bundles, manifest, child))
                            {
                                break;
                            }
                        }
                    }
                    else
                    {
                        if (!GetBundles(bundles, manifest, asset))
                        {
                            break;
                        }
                    }
                }
            }
            else
            {
                foreach (var manifest in manifests)
                {
                    bundles.AddRange(manifest.bundles);
                }
            }

            return bundles;
        }
    }

    public static partial class Versions
    {
        private static readonly Dictionary<string, Manifest> NameWithManifests = new Dictionary<string, Manifest>();
        private static readonly Dictionary<string, string> BundleWithPathOrUrLs = new Dictionary<string, string>();
        private static readonly Dictionary<string, string> NameWithPaths = new Dictionary<string, string>();
        public static Func<string, string> CustomDownloadURL;
        public static string PlayerDataPath { get; set; } = $"{Application.streamingAssetsPath}/{Utility.buildPath}";
        public static string DownloadURL { get; set; }
        public static string DownloadDataPath { get; set; } = $"{Application.persistentDataPath}/{Utility.buildPath}";
        internal static string LocalProtocol { get; set; }
        public static string PlatformName { get; set; } = Utility.GetPlatformName();
        public static Func<string, string> customLoadPath { get; set; }

        public static void OnReadAsset(string assetPath)
        {
            var loadPath = CustomLoadPath(assetPath);
            if (string.IsNullOrEmpty(loadPath))
            {
                return;
            }

            if (!NameWithPaths.TryGetValue(loadPath, out var address))
            {
                NameWithPaths[loadPath] = assetPath;
            }
            else
            {
                if (!address.Equals(assetPath))
                {
                    Logger.W($"{loadPath} already exist {address}");
                }
            }
        }

        public static void GetActualPath(ref string path)
        {
            if (NameWithPaths.TryGetValue(path, out var value))
            {
                path = value;
            }
        }

        private static string CustomLoadPath(string assetPath)
        {
            return customLoadPath(assetPath);
        }

        public static string GetDownloadDataPath(string file)
        {
            var path = $"{DownloadDataPath}/{file}";
            Utility.CreateDirectoryIfNecessary(path);
            return path;
        }

        public static string GetPlayerDataURL(string file)
        {
            return $"{LocalProtocol}{PlayerDataPath}/{file}";
        }

        private static string GetPlayerDataPath(string file)
        {
            return $"{PlayerDataPath}/{file}";
        }

        public static string GetDownloadURL(string file)
        {
            if (CustomDownloadURL == null)
            {
                return $"{DownloadURL}{PlatformName}/{file}";
            }
            var url = CustomDownloadURL(file);
            return !string.IsNullOrEmpty(url) ? url : $"{DownloadURL}{PlatformName}/{file}";
        }

        public static string GetTemporaryPath(string file)
        {
            var path = $"{Application.temporaryCachePath}/{file}";
            Utility.CreateDirectoryIfNecessary(path);
            return path;
        }

        internal static void SetBundlePathOrURl(string assetBundleName, string url)
        {
            BundleWithPathOrUrLs[assetBundleName] = url;
        }

        internal static string GetBundlePathOrURL(ManifestBundle bundle)
        {
            var assetBundleName = bundle.nameWithAppendHash;
            if (BundleWithPathOrUrLs.TryGetValue(assetBundleName, out var path))
            {
                return path;
            }

            var containsKey = builtinAssets.ContainsKey(assetBundleName);
            if (OfflineMode || containsKey)
            {
                if (BinaryMode && containsKey)
                {
                    path = GetPlayerDataPath(BinaryData);
                }
                else
                {
                    path = GetPlayerDataPath(assetBundleName);
                }

                BundleWithPathOrUrLs[assetBundleName] = path;
                return path;
            }

            if (IsDownloaded(bundle))
            {
                path = GetDownloadDataPath(assetBundleName);
                BundleWithPathOrUrLs[assetBundleName] = path;
                return path;
            }

            path = GetDownloadURL(assetBundleName);
            BundleWithPathOrUrLs[assetBundleName] = path;
            return path;
        }

        public static string[] GetAssetsWithDirectory(string dir, bool recursion)
        {
            foreach (var manifest in Manifests)
            {
                if (manifest.IsDirectory(dir))
                {
                    return manifest.GetAssetsWithDirectory(dir, recursion);
                }
            }
            return Array.Empty<string>();
        }
    }
}