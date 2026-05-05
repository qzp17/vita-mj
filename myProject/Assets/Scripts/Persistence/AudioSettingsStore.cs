using UnityEngine;

namespace VitaMj.Persistence
{
    /// <summary>
    /// 背景音乐开关与主音量（0~1），与设置界面共用；读写 <see cref="LocalKeyValueStore"/>。
    /// </summary>
    public static class AudioSettingsStore
    {
        const string KeyMusicEnabled = "settings.audio.music_enabled";
        const string KeyMasterVolume01 = "settings.audio.master_volume_01";

        public static bool GetMusicEnabled() =>
            LocalKeyValueStore.GetInt(KeyMusicEnabled, 1) != 0;

        public static void SetMusicEnabled(bool enabled) =>
            LocalKeyValueStore.SetInt(KeyMusicEnabled, enabled ? 1 : 0);

        /// <summary>背景音乐与音效共用的线性音量系数，默认 1。</summary>
        public static float GetMasterVolume01() =>
            Mathf.Clamp01(LocalKeyValueStore.GetFloat(KeyMasterVolume01, 1f));

        public static void SetMasterVolume01(float linear01) =>
            LocalKeyValueStore.SetFloat(KeyMasterVolume01, Mathf.Clamp01(linear01));
    }
}
