using VEngine.Editor.Builds;

namespace VEngine.Editor.Simulation
{
    public class EditorManifestAsset : ManifestAsset
    {
        private int assetIndex;
        private Build build;

        private int groupIndex;

        protected override void OnLoad()
        {
            base.OnLoad();
            groupIndex = 0;
            assetIndex = 0;
            pathOrURL = name;
            foreach (var item in Build.GetAllBuilds())
            {
                var manifestName = $"{item.name}";
                if (name != manifestName)
                {
                    continue;
                }
                build = item;
                status = LoadableStatus.Loading;
                return;
            }

            Finish("File not found.");
        }

        public override void Override()
        {
            Versions.Override(asset);
        }

        protected override void OnUpdate()
        {
            if (status == LoadableStatus.Loading)
            {
                while (groupIndex < build.groups.Count)
                {
                    var group = build.groups[groupIndex];
                    var assets = group.GetFiles();
                    if (asset.onReadAsset == null)
                    {
                        while (assetIndex < assets.Length)
                        {
                            asset.AddAsset(assets[assetIndex]);
                            assetIndex++;
                        }
                    }
                    else
                    {
                        while (assetIndex < assets.Length)
                        {
                            var path = assets[assetIndex];
                            asset.AddAsset(path);
                            asset.onReadAsset(path);
                            assetIndex++;
                        }
                    }

                    assetIndex = 0;
                    groupIndex++;
                }

                Finish();
            }
        }

        public static EditorManifestAsset Create(string name, bool builtin)
        {
            var asset = new EditorManifestAsset
            {
                name = name
            };
            return asset;
        }
    }
}