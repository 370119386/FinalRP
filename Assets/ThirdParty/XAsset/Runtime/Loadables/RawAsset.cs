using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine.Networking;

namespace VEngine
{
    public sealed class RawAsset : Loadable
    {
        public static readonly Dictionary<string, RawAsset> Cache = new Dictionary<string, RawAsset>();
        public Action<RawAsset> completed;
        private ManifestBundle info;
        public string name;
        private UnityWebRequest request;
        public string savePath { get; private set; }

        public Task<RawAsset> Task
        {
            get
            {
                var tcs = new TaskCompletionSource<RawAsset>();
                completed += operation =>
                {
                    tcs.SetResult(this);
                };
                return tcs.Task;
            }
        }

        protected override void OnLoad()
        {
            info = Versions.GetBundle(name);
            if (info == null)
            {
                Finish("File not found.");
                return;
            }

            pathOrURL = Versions.GetBundlePathOrURL(info);
            savePath = Versions.GetDownloadDataPath(info.nameWithAppendHash);
            status = LoadableStatus.CheckVersion;
        }

        protected override void OnUnload()
        {
            if (request != null)
            {
                request.Dispose();
                request = null;
            }

            Cache.Remove(name);
        }

        protected override void OnComplete()
        {
            if (completed == null)
            {
                return;
            }

            var saved = completed;
            completed?.Invoke(this);

            completed -= saved;
        }

        protected override void OnUpdate()
        {
            switch (status)
            {
                case LoadableStatus.CheckVersion:
                    UpdateChecking();
                    break;
                case LoadableStatus.Loading:
                    UpdateLoading();
                    break;
            }
        }

        protected override void OnUnused()
        {
            completed = null;
            if (Unused.Contains(this))
            {
                return;
            }
            Unused.Add(this);
        }

        private void UpdateLoading()
        {
            if (request == null)
            {
                Finish("request == null");
                return;
            }

            if (!request.isDone)
            {
                return;
            }

            if (!string.IsNullOrEmpty(request.error))
            {
                Finish(request.error);
                return;
            }

            Finish();
        }

        private void UpdateChecking()
        {
            var file = new FileInfo(savePath);
            if (file.Exists)
            {
                if (info.size == file.Length && Utility.ComputeCRC32(savePath) == info.crc)
                {
                    Finish();
                    return;
                }

                File.Delete(savePath);
            }

            request = UnityWebRequest.Get(pathOrURL);
            request.downloadHandler = new DownloadHandlerFile(savePath);
            request.SendWebRequest();
            status = LoadableStatus.Loading;
        }

        public static RawAsset LoadAsync(string filename)
        {
            if (!Cache.TryGetValue(filename, out var asset))
            {
                asset = new RawAsset
                {
                    name = filename
                };
                Cache.Add(filename, asset);
            }

            asset.Load();
            return asset;
        }
    }
}