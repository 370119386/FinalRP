using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using VEngine.Editor.Builds;

namespace VEngine.Editor
{
    public static class MenuItems
    {
        [MenuItem("Versions/Build Bundles", false, 1)]
        public static void BuildBundles()
        {
            //HybridCLR.CompileDllHelper.CompileDll(EditorUserBuildSettings.activeBuildTarget);
            BuildScript.BuildBundles();
            //HybridCLR.CompileDllHelper.CopyPreGameLoad(Settings.GetBuildPath("Configs"));
        }

        [MenuItem("Versions/Build Player", false, 1)]
        public static void BuildPlayer()
        {
            BuildScript.BuildPlayer();
        }

        [MenuItem("Versions/Copy To StreamingAssets", false, 1)]
        public static void CopyToStreamingAssets()
        {
            BuildScript.CopyToStreamingAssets();
        }

        [MenuItem("Versions/Create Command Line Tools", false, 1)]
        public static void CreateCommandTools()
        {
            CommandLine.CreateTools(typeof(CommandLine).FullName, nameof(CommandLine.BuildBundles), "-build %1 -version %2");
            CommandLine.CreateTools(typeof(CommandLine).FullName, nameof(CommandLine.BuildPlayer), "-config %1");
            EditorUtility.OpenWithDefaultApp(Environment.CurrentDirectory);
        }

        [MenuItem("Versions/Copy Path", false, 1000)]
        public static void CopyAssetPath()
        {
            EditorGUIUtility.systemCopyBuffer = AssetDatabase.GetAssetPath(Selection.activeObject);
        }

        [MenuItem("Versions/Compute CRC", false, 1000)]
        public static void ComputeCRC()
        {
            var target = Selection.activeObject;
            var path = AssetDatabase.GetAssetPath(target);
            var crc32 = Utility.ComputeCRC32(File.OpenRead(path));
            Debug.LogFormat("ComputeCRC for {0} with {1}", path, crc32);
        }

        [MenuItem("Versions/Clear Build from selection", false, 100)]
        public static void ClearBuildFromSelection()
        {
            BuildScript.ClearBuildFromSelection();
        }

        [MenuItem("Versions/Clear Build", false, 100)]
        public static void ClearBuild()
        {
            if (EditorUtility.DisplayDialog("提示", "清理构建数据将无法正常增量打包，确认清理？", "确定"))
            {
                var buildPath = Settings.PlatformBuildPath;
                Directory.Delete(buildPath, true);
            }
        }

        [MenuItem("Versions/Clear History", false, 100)]
        public static void ClearHistory()
        {
            BuildScript.ClearHistory();
        }

        [MenuItem("Versions/Clear Download", false, 100)]
        public static void ClearDownload()
        {
            Directory.Delete(Application.persistentDataPath, true);
        }

        [MenuItem("Versions/Clear Temporary", false, 100)]
        public static void ClearTemporary()
        {
            Directory.Delete(Application.temporaryCachePath, true);
        }

        [MenuItem("Versions/View Documentation", false, 200)]
        private static void ViewDocumentation()
        {
            Application.OpenURL("https://xasset.github.io");
        }

        [MenuItem("Versions/View Settings", false, 200)]
        public static void ViewSettings()
        {
            Settings.PingWithSelected(Settings.GetDefaultSettings());
        }

        [MenuItem("Versions/View Build", false, 200)]
        public static void ViewBuild()
        {
            EditorUtility.OpenWithDefaultApp(Settings.PlatformBuildPath);
        }

        [MenuItem("Versions/View Download", false, 200)]
        public static void ViewDownload()
        {
            EditorUtility.OpenWithDefaultApp(Application.persistentDataPath);
        }

        [MenuItem("Versions/View Temporary", false, 200)]
        public static void ViewTemporary()
        {
            EditorUtility.OpenWithDefaultApp(Application.temporaryCachePath);
        }

        [MenuItem("Versions/File a Bug", false, 1000)]
        public static void FileABug()
        {
            Application.OpenURL("https://github.com/xasset/xasset.github.io/issues");
        }
    }
}