using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace VEngine.Editor.Builds
{
    public static class CommandLine
    {
        public static void CreateTools(string script, string method, string args)
        {
            var cmd =
                $"\"{EditorApplication.applicationPath}\" -quit -batchmode -logfile BuildBundles.log -projectPath \"{Environment.CurrentDirectory}\" -executeMethod {script}.{method} {args}";
            File.WriteAllText(method + ".bat", cmd);
            File.WriteAllText(method + ".sh", cmd);
        }

        public static string GetArg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length; i++)
            {
                if (args[i] == name && args.Length > i + 1)
                {
                    return args[i + 1];
                }
            }

            return null;
        }

        public static void BuildBundles()
        {
            var build = GetArg("-build");
            Debug.LogFormat("BatchMode.BuildBundles {0}", build);
            var version = GetArg("-version");
            if (!int.TryParse(version, out var buildVersion))
            {
                buildVersion = 0;
            }
            var builds = Build.GetAllBuilds();
            var target = Array.Find(builds, m => m.name.Equals(build, StringComparison.OrdinalIgnoreCase));
            if (target != null)
            {
                BuildScript.BuildBundles(new BuildTask(target, buildVersion));
            }
            else
            {
                BuildScript.BuildBundles();
            }
        }

        public static void BuildPlayer()
        {
            var config = GetArg("-config");
            var offlineMode = GetArg("-offline");
            Debug.LogFormat("BatchMode.BuildPlayer {0}", config);
            if (!string.IsNullOrEmpty(config))
            {
                var settings = Settings.GetDefaultSettings();
                var playerConfigs = settings.playerConfigs;
                for (var index = 0; index < playerConfigs.Count; index++)
                {
                    var playerConfig = playerConfigs[index];
                    if (playerConfig.name.Equals(config))
                    {
                        settings.buildPlayerConfigIndex = index;
                        break;
                    }
                }
            }

            if (!string.IsNullOrEmpty(offlineMode))
            {
                var settings = Settings.GetDefaultSettings();
                if (!bool.TryParse(offlineMode, out settings.offlineMode))
                {
                    settings.offlineMode = false;
                }
            }

            BuildScript.BuildPlayer();
        }
    }
}