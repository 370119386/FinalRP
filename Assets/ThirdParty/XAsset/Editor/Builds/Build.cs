using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace VEngine.Editor.Builds
{
    [CreateAssetMenu(menuName = "Versions/Build", fileName = "Build", order = 0)]
    public class Build : ScriptableObject
    {
        public bool packBinary;
        public bool autoGroup = true;
        public BundleMode autoGroupBundleMode = BundleMode.PackByDirectory;
        public BundleMode defaultBundleMode = BundleMode.PackByDirectory;
        public BuildAssetBundleOptions buildAssetBundleOptions = BuildAssetBundleOptions.ChunkBasedCompression;
        public List<Group> groups = new List<Group>();

        public static Manifest GetManifest(string buildName)
        {
            var manifest = CreateInstance<Manifest>();
            manifest.name = buildName;
            var path = Settings.GetBuildPath(manifest.name);
            if (!File.Exists(path))
            {
                return manifest;
            }
            manifest.Load(path);
            return manifest;
        }

        public static Build[] GetAllBuilds()
        {
            var builds = new List<Build>();
            var guilds = AssetDatabase.FindAssets("t:" + typeof(Build).FullName);
            foreach (var guild in guilds)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guild);
                if (string.IsNullOrEmpty(assetPath))
                {
                    continue;
                }
                var asset = AssetDatabase.LoadAssetAtPath<Build>(assetPath);
                if (asset == null)
                {
                    continue;
                }
                builds.Add(asset);
            }

            return builds.ToArray();
        }
    }
}