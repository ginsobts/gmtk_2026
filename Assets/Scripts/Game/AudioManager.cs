using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 极简音频系统：一路 BGM(循环) + 一路 SFX(一次性叠加播放)。
/// 资源按约定放在：
///   Resources/Audio/BGM/&lt;key&gt;   （循环背景乐）
///   Resources/Audio/SFX/&lt;key&gt;   （一次性音效）
/// 文件名不带扩展名即可（.wav/.mp3/.ogg 都行）。**缺资源时静默，不报错**。
/// 事件 key 清单见 README「音频」一节。
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Range(0f, 1f)] public float bgmVolume = 0.6f;
    [Range(0f, 1f)] public float sfxVolume = 0.9f;

    AudioSource _bgm;
    AudioSource _sfx;
    string _currentBgmKey;
    readonly Dictionary<string, AudioClip> _cache = new Dictionary<string, AudioClip>();

    void Awake()
    {
        Instance = this;
        _bgm = gameObject.AddComponent<AudioSource>();
        _bgm.loop = true; _bgm.playOnAwake = false; _bgm.spatialBlend = 0f; _bgm.volume = bgmVolume;
        _sfx = gameObject.AddComponent<AudioSource>();
        _sfx.loop = false; _sfx.playOnAwake = false; _sfx.spatialBlend = 0f; _sfx.volume = sfxVolume;
    }

    AudioClip Load(string folder, string key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        string path = folder + "/" + key;
        if (_cache.TryGetValue(path, out var c)) return c;   // 命中缓存（含 null，避免反复 IO）
        c = Resources.Load<AudioClip>("Audio/" + path);
        _cache[path] = c;
        return c;
    }

    /// <summary>切换循环 BGM。同一 key 不重播；key 为空或缺资源则停止/静默。</summary>
    public void PlayBgm(string key)
    {
        if (_currentBgmKey == key && _bgm.isPlaying) return;
        _currentBgmKey = key;
        var clip = Load("BGM", key);
        // 当前先接入一首通用的“平静但诡异”BGM。菜单与未单独配乐的调查阶段
        // 自动回退到 phase1，避免切阶段后因为 phase2/phase3 缺文件而突然静音。
        if (clip == null && (key == "menu" || (!string.IsNullOrEmpty(key) && key.StartsWith("phase"))))
            clip = Load("BGM", "phase1");
        if (clip == null) { _bgm.Stop(); _bgm.clip = null; return; }
        // 回退后若仍是同一首音乐，保持当前播放进度，不从头重播。
        if (_bgm.isPlaying && _bgm.clip == clip) return;
        _bgm.clip = clip; _bgm.volume = bgmVolume; _bgm.Play();
    }

    public void StopBgm() { _currentBgmKey = null; _bgm.Stop(); _bgm.clip = null; }

    /// <summary>播放一次性音效；缺资源则静默。</summary>
    public void PlaySfx(string key)
        => PlaySfx(key, 1f);

    /// <summary>播放一次性音效，并允许调用方按类型调节相对音量。</summary>
    public void PlaySfx(string key, float volumeScale)
    {
        var clip = Load("SFX", key);
        if (clip != null) _sfx.PlayOneShot(clip, sfxVolume * Mathf.Clamp01(volumeScale));
    }
}
