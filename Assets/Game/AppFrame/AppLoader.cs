using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using VEngine;

namespace Framework.Game
{
    public enum LoadMode
    {
        LoadByName,
        LoadByNameWithoutExtension,
        LoadByFullPath,
    }
    public enum FrameRate
    {
        FPS_30 = 30,
        FPS_45 = 45,
        FPS_60 = 60,
        FPS_120 = 120,
    }
    public enum LogLevel
    {
        Normal = (1 << 0),
        Flow = (1 << 1),
        Warning = (1 << 2),
        Error = (1 << 3),
    }

    [RequireComponent(typeof(Updater))]
    [DisallowMultipleComponent]
    public class AppLoader : MonoBehaviour
    {
        [Tooltip("资源下载地址，指向平台目录的父目录")] public string downloadURL = "http://127.0.0.1:8080/";
        [Tooltip("是否启动后更新服务器版本信息")] public bool autoSyncVersion;

        [Header("Load")]
        [Tooltip("通过关键字进行路径匹配，为路径生成短链接，可以按需使用")]
        public string[] loadKeys = {
            "Scenes", "Prefabs"
        };
        [Tooltip("加载模式")] public LoadMode loadMode = LoadMode.LoadByFullPath;
        public string[] baseManifests = new string[]
        {
            "Base",
        };
        [Header("FrameRate")]
        public FrameRate frameRate = FrameRate.FPS_30;

        [SerializeField]
        LogLevel logLevel = LogLevel.Error;

        private void OnValidate()
        {
            Application.targetFrameRate = (int)this.frameRate;
        }

        // Start is called before the first frame update
        IEnumerator Start()
        {
            VEngine.Logger.LogLevel = (int)logLevel;
            Application.targetFrameRate = (int)this.frameRate;

            SetupCustomLoad();
            DontDestroyOnLoad(this.gameObject);
            Versions.DownloadURL = downloadURL;
            VEngine.Logger.F("[Start Versions.InitializeAsync]");
            var operation = VEngine.Versions.InitializeAsync();
            yield return operation;
            if (operation.status != OperationStatus.Success)
            {
                VEngine.Logger.E($"[GameError]: Game Start Failed For Versions.InitializeAsync");
                yield break;
            }
            VEngine.Logger.I($"[API Version]: {Versions.APIVersion}");
            VEngine.Logger.I($"[Manifests Version] : {Versions.ManifestsVersion}");
            VEngine.Logger.I($"[SimulationMode] : [{Versions.SimulationMode}]");
            VEngine.Logger.I($"[OfflineMode] : [{Versions.OfflineMode}]");
            VEngine.Logger.I($"[BinaryMode] : [{Versions.BinaryMode}]");
            VEngine.Logger.I($"[PlayerDataPath] : [{Versions.PlayerDataPath}]");
            VEngine.Logger.I($"[DownloadURL] : [{Versions.DownloadURL}]");

            if (autoSyncVersion && !Versions.OfflineMode)
            {
                VEngine.Logger.F("[Update Manifests]");
                var update = Versions.UpdateAsync(baseManifests);
                yield return update;

                if (update.status == OperationStatus.Success)
                {
                    update.Override();
                    update.Dispose();
                    VEngine.Logger.I("Success to update versions with version: {0}", Versions.ManifestsVersion);
                }
                else
                {
                    update.Dispose();
                    VEngine.Logger.E($"[GameError]: Game Update Manifests Failed");
                    yield break;
                }
            }

            VEngine.Logger.F($"[Application]: Enter Game [QualityLevel:{QualitySettings.GetQualityLevel()}]....");
        }

        IEnumerator LoadBytesFile(string config, System.Action<byte[]> onSucceed, System.Action onFailed)
        {
            string url = $"{downloadURL}{VEngine.Versions.PlatformName}/{config}";

            using (UnityWebRequest www = UnityWebRequest.Get(url))
            {
                www.timeout = 10;
                yield return www.SendWebRequest();
                if (!string.IsNullOrEmpty(www.error))
                {
                    Debug.LogError($"[LoadConfigs]:[url:{url}]:[reason:{www.error}]");
                    if (null != onFailed)
                        onFailed();
                    yield break;
                }
                if (null != onSucceed)
                    onSucceed(www.downloadHandler.data);
            }
        }

        void EnterGame()
        {

        }

        private void SetupCustomLoad()
        {
            switch (loadMode)
            {
                case LoadMode.LoadByName:
                    Versions.customLoadPath = LoadByName;
                    break;
                case LoadMode.LoadByNameWithoutExtension:
                    Versions.customLoadPath = LoadByNameWithoutExtension;
                    break;
                default:
                    Versions.customLoadPath = null;
                    break;
            }
        }

        private string LoadByNameWithoutExtension(string assetPath)
        {
            if (loadKeys == null || loadKeys.Length == 0)
            {
                return null;
            }

            if (!Array.Exists(loadKeys, assetPath.Contains))
            {
                return null;
            }

            var assetName = Path.GetFileNameWithoutExtension(assetPath);
            return assetName;
        }

        private string LoadByName(string assetPath)
        {
            if (loadKeys == null || loadKeys.Length == 0)
            {
                return null;
            }

            if (!Array.Exists(loadKeys, assetPath.Contains))
            {
                return null;
            }

            var assetName = Path.GetFileName(assetPath);
            return assetName;
        }

        private void OnDestroy()
        {

        }
    }
}