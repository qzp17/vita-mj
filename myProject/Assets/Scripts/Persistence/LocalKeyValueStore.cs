using UnityEngine;

namespace VitaMj.Persistence
{
    /// <summary>
    /// 设备本地读写（Unity <see cref="PlayerPrefs"/>），前缀隔离项目键名；不涉及账号。
    /// </summary>
    public static class LocalKeyValueStore
    {
        const string Prefix = "VitaMj.v1.";

        public static string GetString(string key, string defaultValue = "")
        {
            return PlayerPrefs.GetString(Prefix + key, defaultValue);
        }

        public static void SetString(string key, string value)
        {
            PlayerPrefs.SetString(Prefix + key, value);
            PlayerPrefs.Save();
        }

        public static int GetInt(string key, int defaultValue = 0)
        {
            string full = Prefix + key;
            if (!PlayerPrefs.HasKey(full))
                return defaultValue;
            return PlayerPrefs.GetInt(full);
        }

        public static void SetInt(string key, int value)
        {
            PlayerPrefs.SetInt(Prefix + key, value);
            PlayerPrefs.Save();
        }

        public static void DeleteKey(string key)
        {
            PlayerPrefs.DeleteKey(Prefix + key);
            PlayerPrefs.Save();
        }
    }
}
