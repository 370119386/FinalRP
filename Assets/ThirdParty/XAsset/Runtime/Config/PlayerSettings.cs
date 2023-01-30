using System;
using System.Collections.Generic;
using UnityEngine;

namespace VEngine
{
    [Serializable]
    public class AssetLocation
    {
        public string name;
        public ulong offset;
    }

    public class PlayerSettings : ScriptableObject
    {
        public List<AssetLocation> assets = new List<AssetLocation>();
        public string[] manifests;
        public bool offlineMode;
        public bool binaryMode;
        public bool simulationMode;
        public string version;
    }
}