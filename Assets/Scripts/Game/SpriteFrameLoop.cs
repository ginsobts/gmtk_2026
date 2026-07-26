using UnityEngine;

/// <summary>在 SpriteRenderer 上循环播放帧序列。</summary>
public class SpriteFrameLoop : MonoBehaviour
{
    public SpriteRenderer target;
    public Sprite[] frames;
    public float fps = 10f;

    bool _playing;
    float _timer;
    int _index;

    void Awake()
    {
        if (target == null) target = GetComponent<SpriteRenderer>();
    }

    public void SetFrames(Sprite[] sequence)
    {
        frames = sequence;
        _index = 0;
        _timer = 0f;
        if (frames != null && frames.Length > 0 && target != null)
            target.sprite = frames[0];
    }

    public void SetPlaying(bool play)
    {
        _playing = play;
        if (!_playing && frames != null && frames.Length > 0 && target != null)
        {
            _index = 0;
            _timer = 0f;
            target.sprite = frames[0];
        }
    }

    void Update()
    {
        if (!_playing || DebugControl.Frozen || frames == null || frames.Length <= 1 || target == null) return;

        _timer += Time.deltaTime;
        float frameTime = 1f / Mathf.Max(fps, 0.01f);
        while (_timer >= frameTime)
        {
            _timer -= frameTime;
            _index = (_index + 1) % frames.Length;
            target.sprite = frames[_index];
        }
    }
}
