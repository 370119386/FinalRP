using System.Collections.Generic;
using System.Text;

namespace VEngine
{
    public sealed class UpdateVersions : Operation
    {
        public readonly List<ManifestAsset> assets = new List<ManifestAsset>();
        private readonly List<string> errors = new List<string>();
        public string[] manifests;

        public string version
        {
            get
            {
                var sb = new StringBuilder();
                for (var index = 0; index < assets.Count; index++)
                {
                    var manifest = assets[index];
                    sb.Append(manifest.assetVersion.version);
                    if (index < assets.Count - 1)
                    {
                        sb.Append(".");
                    }
                }

                return sb.ToString();
            }
        }

        public bool changed
        {
            get
            {
                foreach (var asset in assets)
                {
                    if (asset.changed)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public override void Start()
        {
            base.Start();
            if (Versions.OfflineMode)
            {
                Finish();
                return;
            }

            foreach (var manifest in manifests)
            {
                assets.Add(ManifestAsset.LoadAsync(manifest));
            }
        }

        public void Override()
        {
            if (Versions.OfflineMode)
            {
                return;
            }
            foreach (var asset in assets)
            {
                asset.Override();
            }
        }

        public void Dispose()
        {
            foreach (var asset in assets)
            {
                if (asset.status != LoadableStatus.Unloaded)
                {
                    asset.Release();
                }
            }
            assets.Clear();
        }

        protected override void Update()
        {
            if (status != OperationStatus.Processing)
            {
                return;
            }
            foreach (var asset in assets)
            {
                if (!asset.isDone)
                {
                    return;
                }
                if (!string.IsNullOrEmpty(asset.error))
                {
                    errors.Add(asset.error);
                }
            }

            Finish(errors.Count == 0 ? null : string.Join("\n", errors.ToArray()));
        }
    }
}