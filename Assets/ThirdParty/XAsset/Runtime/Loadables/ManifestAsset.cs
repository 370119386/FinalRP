using System;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace VEngine
{
    public class DefaultCertificateHandler : CertificateHandler
    {
        protected override bool ValidateCertificate(byte[] certificateData)
        {
            return true;
        }
    }

    public class ManifestAsset : Loadable
    {
        private bool _builtin;
        private UnityWebRequest _request;
        public static Func<string, bool, ManifestAsset> Creator { get; set; } = Create;
        protected Manifest asset { get; private set; }
        public ManifestVersion assetVersion { get; private set; }
        protected string name { get; set; }

        public static DefaultCertificateHandler certificateHandler { get; set; } = new DefaultCertificateHandler();

        public bool changed
        {
            get
            {
                if (assetVersion == null)
                {
                    return false;
                }

                var find = Versions.Manifests.Find(m => asset.name.Equals(m.name));
                if (find != null)
                {
                    return find.version < assetVersion.version;
                }

                return true;
            }
        }

        private static ManifestAsset CreateInstance(string name, bool builtin)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException(nameof(name));
            }

            return Creator(name, builtin);
        }

        protected override void OnLoad()
        {
            asset = ScriptableObject.CreateInstance<Manifest>();
            if (Versions.customLoadPath != null)
            {
                asset.onReadAsset = Versions.OnReadAsset;
            }

            var split = name.Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries);
            asset.name = split.Length > 1 ? split[0] : name;
            pathOrURL = _builtin ? Versions.GetPlayerDataURL(name) : Versions.GetDownloadURL(name);
            var file = Manifest.GetVersionFile(name);
            var url = _builtin ? Versions.GetPlayerDataURL(file) : Versions.GetDownloadURL(file);
            DownloadAsync(url, GetTemporaryPath(file));
            status = LoadableStatus.CheckVersion;
        }

        public virtual void Override()
        {
            if (assetVersion == null)
            {
                return;
            }

            if (_builtin)
            {
                var path = Versions.GetDownloadDataPath(Manifest.GetVersionFile(asset.name));
                var file = ManifestVersion.Load(path);
                if (file.version > assetVersion.version)
                {
                    path = Versions.GetDownloadDataPath(asset.name);
                    if (File.Exists(path))
                    {
                        using (var stream = File.OpenRead(path))
                        {
                            if (Utility.ComputeCRC32(stream) == file.crc)
                            {
                                asset.Load(path);
                                Versions.Override(asset);
                                return;
                            }
                        }
                    }
                }

                asset.Load(GetTemporaryPath(name));
                Versions.Override(asset);
            }
            else
            {
                if (!changed)
                {
                    return;
                }
                var from = GetTemporaryPath(name);
                var dest = Versions.GetDownloadDataPath(name).Replace(name, asset.name);
                if (File.Exists(from))
                {
                    Logger.I("Copy {0} to {1}.", from, dest);
                    File.Copy(from, dest, true);
                }

                var versionName = Manifest.GetVersionFile(name);
                from = GetTemporaryPath(versionName);
                if (File.Exists(from))
                {
                    var path = Versions.GetDownloadDataPath(versionName).Replace(name, asset.name);
                    Logger.I("Copy {0} to {1}.", from, path);
                    File.Copy(from, path, true);
                }
            }

            Versions.Override(asset);
        }

        public static ManifestAsset LoadAsync(string name, bool builtin = false)
        {
            var manifestAsset = CreateInstance(name, builtin);
            manifestAsset.Load();
            return manifestAsset;
        }

        private static ManifestAsset Create(string name, bool builtin)
        {
            return new ManifestAsset
            {
                name = name,
                _builtin = builtin
            };
        }

        private void DownloadAsync(string url, string savePath)
        {
            if (File.Exists(savePath))
            {
                File.Delete(savePath);
            }

            Logger.I("Load {0}", url);
            _request = UnityWebRequest.Get(url);
            _request.certificateHandler = certificateHandler;
            _request.downloadHandler = new DownloadHandlerFile(savePath);
            _request.SendWebRequest();
        }

        private string GetTemporaryPath(string filename)
        {
            return Versions.GetTemporaryPath(string.Format(_builtin ? "Builtin/{0}" : "{0}", filename));
        }

        protected override void OnUpdate()
        {
            if (status == LoadableStatus.CheckVersion)
            {
                UpdateVersion();
            }
            else if (status == LoadableStatus.Downloading)
            {
                UpdateDownloading();
            }
            else if (status == LoadableStatus.Loading)
            {
                if (changed && !_builtin)
                {
                    var assetPath = GetTemporaryPath(name);
                    asset.Load(assetPath);
                }

                Finish();
            }
        }

        private void UpdateDownloading()
        {
            if (_request == null)
            {
                Finish("request == nul with " + status);
                return;
            }

            progress = 0.2f + _request.downloadProgress;
            if (!_request.isDone)
            {
                return;
            }

            if (!string.IsNullOrEmpty(_request.error))
            {
                Finish(_request.error);
                return;
            }

            _request.Dispose();
            _request = null;

            status = LoadableStatus.Loading;
        }

        private void UpdateVersion()
        {
            if (_request == null)
            {
                Finish("request == null with " + status);
                return;
            }

            progress = 0.2f * _request.downloadProgress;
            if (!_request.isDone)
            {
                return;
            }

            if (!string.IsNullOrEmpty(_request.error))
            {
                Finish(_request.error);
                return;
            }

            _request.Dispose();
            _request = null;

            var savePath = GetTemporaryPath(Manifest.GetVersionFile(name));
            if (!File.Exists(savePath))
            {
                Finish("version not exist.");
                return;
            }

            assetVersion = ManifestVersion.Load(savePath);
            Logger.I("Read {0} with version {1} crc {2}", name, assetVersion.version, assetVersion.crc);
            var path = GetTemporaryPath(name);
            if (File.Exists(path))
            {
                using (var stream = File.OpenRead(path))
                {
                    if (Utility.ComputeCRC32(stream) == assetVersion.crc)
                    {
                        Logger.I("Skip to download {0}, because nothing to update.", name);
                        status = LoadableStatus.Loading;
                        return;
                    }
                }
            }

            if (File.Exists(path))
            {
                File.Delete(path);
            }

            DownloadAsync(pathOrURL, path);
            status = LoadableStatus.Downloading;
        }
    }
}