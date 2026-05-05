using System;
using FairyGUI;
using Newtonsoft.Json.Linq;
using UnityEngine;
using VitaMj.Config;

/// <summary>
/// 统一管理 FairyGUI Audio 包内音效：通过配置表 tag 查到包内音效资源名，再交给 Unity <see cref="AudioSource"/> 播放。
/// 配置表二进制由导出器生成，需与表中「音效资源名列」对齐（或通过 Inspector 指定列名）。
/// </summary>
[DefaultExecutionOrder(-200)]
public sealed class AudioManager : MonoBehaviour
{
    static AudioManager _instance;

    /// <summary>若场景内无主音管理器则在首次访问时自动创建常驻节点。</summary>
    public static AudioManager Instance =>
        _instance != null ? _instance : EnsureInstance();

    [Header("FGUI Audio 包")]
    [Tooltip("与 UIPackage.AddPackage 一致，通常为 res/Audio")]
    [SerializeField]
    string audioPackageResourcePath = "res/Audio";

    [Tooltip("FairyGUI 包名（与编辑器里 Audio 包名一致）")]
    [SerializeField]
    string audioPackageName = "Audio";

    [Header("配置")]
    [Tooltip("Resources/ExportConfig 下二进制表名（不含扩展名），如 Audio")]
    [SerializeField]
    string audioConfigTableKey = "Audio";

    [Tooltip("Audio 表中除 tag 外、填写 FairyGUI 包内音效条目的名称（与导出 JSON 字段名一致）")]
    [SerializeField]
    string soundItemPropertyName = "sound";

    [Header("播放器")]
    [SerializeField]
    AudioSource musicSource;

    [SerializeField]
    AudioSource sfxSource;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureAudioSources();
    }

    void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    static AudioManager EnsureInstance()
    {
        var found = FindObjectOfType<AudioManager>();
        if (found != null)
            return found;

        var go = new GameObject("AudioManager");
        var mgr = go.AddComponent<AudioManager>();
        DontDestroyOnLoad(go);
        return mgr;
    }

    void EnsureAudioSources()
    {
        if (musicSource == null)
        {
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.playOnAwake = false;
            musicSource.loop = true;
            musicSource.spatialBlend = 0f;
        }

        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.loop = false;
            sfxSource.spatialBlend = 0f;
        }
    }

    /// <summary>按 Audio 表中 tag 解析资源名并作为背景音乐播放。</summary>
    public bool PlayMusicByTag(string tag, bool loop = true)
    {
        if (!TryGetClipFromConfig(tag, out AudioClip clip))
            return false;

        EnsureAudioSources();
        musicSource.loop = loop;
        musicSource.clip = clip;
        musicSource.Play();
        return true;
    }

    /// <summary>按 tag 单次播放音效（可同时叠放）。</summary>
    public bool PlaySfxByTag(string tag)
    {
        if (!TryGetClipFromConfig(tag, out AudioClip clip))
            return false;

        EnsureAudioSources();
        sfxSource.PlayOneShot(clip);
        return true;
    }

    public void StopMusic()
    {
        if (musicSource != null && musicSource.isPlaying)
            musicSource.Stop();
    }

    public void SetMusicVolume(float linear01)
    {
        EnsureAudioSources();
        musicSource.volume = Mathf.Clamp01(linear01);
    }

    public void SetSfxVolume(float linear01)
    {
        EnsureAudioSources();
        sfxSource.volume = Mathf.Clamp01(linear01);
    }

    bool TryGetClipFromConfig(string tag, out AudioClip clip)
    {
        clip = null;
        if (!ConfigReader.TryGetRawRow(audioConfigTableKey, tag, out JObject row))
        {
            Debug.LogWarning($"[AudioManager] 未读到配置：{audioConfigTableKey}.{tag}");
            return false;
        }

        string itemName = ResolveSoundItemName(row);
        if (string.IsNullOrEmpty(itemName))
        {
            Debug.LogWarning($"[AudioManager] tag「{tag}」行中没有可用的 FairyGUI 音效名（请先检查导出字段名是否与 Inspector 「{soundItemPropertyName}」一致）。");
            return false;
        }

        EnsurePackageLoaded();

        object asset = UIPackage.GetItemAsset(audioPackageName, itemName);
        if (asset is not NAudioClip nac || nac.nativeClip == null)
        {
            Debug.LogWarning($"[AudioManager] 包「{audioPackageName}」中未载入音效条目「{itemName}」。");
            return false;
        }

        clip = nac.nativeClip;
        return true;
    }

    string ResolveSoundItemName(JObject row)
    {
        string primary = TryPropertyString(row, soundItemPropertyName);
        if (!string.IsNullOrEmpty(primary))
            return primary;

        string[] fallbacks =
        {
            "sound",
            "soundName",
            "audio",
            "audioName",
            "name",
            "resName",
            "resource",
        };

        foreach (string k in fallbacks)
        {
            if (string.Equals(k, soundItemPropertyName, StringComparison.OrdinalIgnoreCase))
                continue;
            string v = TryPropertyString(row, k);
            if (!string.IsNullOrEmpty(v))
                return v;
        }

        return null;
    }

    static string TryPropertyString(JObject obj, string name)
    {
        if (obj == null || string.IsNullOrEmpty(name))
            return null;

        foreach (JProperty p in obj.Properties())
        {
            if (!string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
                continue;
            if (p.Value == null || p.Value.Type == JTokenType.Null)
                return null;
            return p.Value.Value<string>()?.Trim();
        }

        return null;
    }

    void EnsurePackageLoaded()
    {
        if (UIPackage.GetByName(audioPackageName) != null)
            return;

        if (string.IsNullOrEmpty(audioPackageResourcePath))
        {
            Debug.LogError("[AudioManager] audioPackageResourcePath 为空。");
            return;
        }

        UIPackage.AddPackage(audioPackageResourcePath);
    }
}
