using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace VEngine
{
    public sealed class InitializeVersions : Operation
    {
        private readonly List<ManifestAsset> assets = new List<ManifestAsset>();
        private readonly List<string> errors = new List<string>();
        public string[] manifests;

        public override void Start()
        {
            base.Start();
            InitLocalProtocol();
            var settings = Resources.Load<PlayerSettings>(nameof(PlayerSettings));
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<PlayerSettings>();
            }
            var appVersion = PlayerPrefs.GetString(Versions.AppVersion, settings.version);
            if (appVersion != settings.version)
            {
                // 覆盖安装删除下载目录的清单文件防止新包加载旧资源
                foreach (var manifest in settings.manifests)
                {
                    var path = Versions.GetDownloadDataPath(manifest);
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }
                    path = Versions.GetDownloadDataPath(Manifest.GetVersionFile(manifest));
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }
                }
                PlayerPrefs.SetString(Versions.AppVersion, settings.version);
            }
            foreach (var asset in settings.assets)
            {
                Versions.builtinAssets[asset.name] = asset;
            }
            Versions.SimulationMode = settings.simulationMode;
            Versions.OfflineMode = settings.offlineMode;
            Versions.BinaryMode = settings.binaryMode;
            manifests = settings.manifests;
            foreach (var manifest in settings.manifests)
            {
                assets.Add(ManifestAsset.LoadAsync(manifest, true));
            }
        }

        private static void InitLocalProtocol()
        {
            if (Application.platform != RuntimePlatform.OSXEditor &&
                Application.platform != RuntimePlatform.OSXPlayer &&
                Application.platform != RuntimePlatform.IPhonePlayer)
            {
                if (Application.platform == RuntimePlatform.WindowsEditor ||
                    Application.platform == RuntimePlatform.WindowsPlayer)
                {
                    Versions.LocalProtocol = "file:///";
                }
                else
                {
                    Versions.LocalProtocol = string.Empty;
                }
            }
            else
            {
                Versions.LocalProtocol = "file://";
            }
        }

        protected override void Update()
        {
            if (status != OperationStatus.Processing)
            {
                return;
            }
            for (var index = 0; index < assets.Count; index++)
            {
                var asset = assets[index];
                if (!asset.isDone)
                {
                    continue;
                }
                assets.RemoveAt(index);
                index--;
                if (!string.IsNullOrEmpty(asset.error))
                {
                    errors.Add(asset.error);
                }
                else
                {
                    asset.Override();
                    asset.Release();
                }
            }

            if (assets.Count == 0)
            {
                Finish(errors.Count == 0 ? null : string.Join("\n", errors.ToArray()));
            }
        }
    }
}