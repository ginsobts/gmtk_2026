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
        if (clip == null) { _bgm.Stop(); _bgm.clip = null; return; }
        _bgm.clip = clip; _bgm.volume = bgmVolume; _bgm.Play();
    }

    public void StopBgm() { _currentBgmKey = null; _bgm.Stop(); _bgm.clip = null; }

    /// <summary>播放一次性音效；缺资源则静默。</summary>
    public void PlaySfx(string key)
    {
        var clip = Load("SFX", key);
        if (clip != null) _sfx.PlayOneShot(clip, sfxVolume);
    }
}
