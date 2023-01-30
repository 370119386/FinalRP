using System;

namespace VEngine.Editor.Builds
{
    [Serializable]
    public class PlayerConfig
    {
        public string name;
        public AssetGroup assetGroup;
        public bool splitBuildWithGroup;
        public bool blacklistMode;
    }
}