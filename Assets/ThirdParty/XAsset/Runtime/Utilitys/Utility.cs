using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace VEngine
{
    public static class Utility
    {
        public const string buildPath = "Bundles";

        public const string unsupported = "Unsupported";

        private static readonly double[] byteUnits =
        {
            1073741824.0, 1048576.0, 1024.0, 1
        };

        private static readonly string[] byteUnitsNames =
        {
            "GB", "MB", "KB", "B"
        };

        private static readonly MD5 md5 = MD5.Create();

        public static string GetPlatformName()
        {
            if (Application.platform == RuntimePlatform.Android)
            {
                return "Android";
            }
            if (Application.platform == RuntimePlatform.WindowsPlayer)
            {
                return "Windows";
            }
            if (Application.platform == RuntimePlatform.IPhonePlayer)
            {
                return "iOS";
            }
            return Application.platform == RuntimePlatform.WebGLPlayer ? "WebGL" : unsupported;
        }

        public static string FormatBytes(long bytes)
        {
            var size = "0 B";
            if (bytes == 0)
            {
                return size;
            }

            for (var index = 0; index < byteUnits.Length; index++)
            {
                var unit = byteUnits[index];
                if (bytes >= unit)
                {
                    size = $"{bytes / unit:##.##} {byteUnitsNames[index]}";
                    break;
                }
            }

            return size;
        }

        public static uint ComputeCRC32(Stream stream)
        {
            var crc32 = new CRC32();
            return crc32.Compute(stream);
        }

        public static uint ComputeCRC32(string filename)
        {
            if (!File.Exists(filename))
            {
                return 0;
            }

            using (var stream = File.OpenRead(filename))
            {
                return ComputeCRC32(stream);
            }
        }

        private static string ToHash(byte[] data)
        {
            var sb = new StringBuilder();
            foreach (var t in data)
            {
                sb.Append(t.ToString("x2"));
            }
            return sb.ToString();
        }

        public static string GetMD5(string input)
        {
            var data = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
            return ToHash(data);
        }

        public static bool CreateDirectoryIfNecessary(string path)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
                return true;
            }
            return false;
        }
    }
}