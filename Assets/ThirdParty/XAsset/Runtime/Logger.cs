namespace VEngine
{
    using System.Diagnostics;
    using UnityEngine;
    using Debug = UnityEngine.Debug;
    public static class Logger
    {
        public const int Normal = 1 << 0;
        public const int Flow = 1 << 1;
        public const int Warning = 1 << 2;
        public const int Error = 1 << 3;
        static int _LogLevel = -1;
        public static int LogLevel
        {
            get
            {
                if(_LogLevel == -1)
                    _LogLevel = PlayerPrefs.GetInt("AppConfig.LogLevel", Error);
                return _LogLevel;
            }
            set
            {
                _LogLevel = value;
                PlayerPrefs.SetInt("AppConfig.LogLevel", value);
            }
        }

        public static void E(string format, params object[] args)
        {
            if ((LogLevel & Error) == 0)
                return;

            if (null == args || args.Length == 0)
                Debug.LogError(format);
            else
                Debug.LogErrorFormat(format, args);
        }

        public static void W(string format, params object[] args)
        {
            if ((LogLevel & Warning) == 0)
                return;

            if (null == args || args.Length == 0)
                Debug.LogWarning(format);
            else
                Debug.LogWarningFormat(format, args);
        }

        public static void I(string format, params object[] args)
        {
            if ((LogLevel & Normal) == 0)
                return;

            if (null == args || args.Length == 0)
                Debug.Log(format);
            else
                Debug.LogFormat(format, args);
        }

        public static void F(string format, params object[] args)
        {
            if ((LogLevel & Flow) == 0)
                return;

            if (null == args || args.Length == 0)
                Debug.Log($"<color=#3d8eff>[Flow]:{format}</color>");
            else
                Debug.Log($"<color=#3d8eff>[Flow]:{string.Format(format, args)}</color>");
        }
    }
}