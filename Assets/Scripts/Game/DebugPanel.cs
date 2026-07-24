using UnityEngine;

/// <summary>
/// 运行时调试面板（F1 呼出）。仅在编辑器 / 开发包里由 GameManager 挂载。
/// - 冻结：暂停相机跟随 / billboard / 抖动，方便在 Scene 视图里手动观察与拖动。
/// - 相机滑块：FOV / 俯角 / 偏移 / 跟随，实时生效。
/// - 保存到 GameConfig：把当前相机参数写回资产（编辑器内），下次 Play 直接用。
/// </summary>
public class DebugPanel : MonoBehaviour
{
    Camera _cam;
    CameraRig _rig;
    bool _show;

    float _fov, _tilt, _follow;
    Vector3 _offset;

    public void Init(Camera cam, CameraRig rig)
    {
        _cam = cam;
        _rig = rig;
        if (_cam != null) { _fov = _cam.fieldOfView; _tilt = _cam.transform.eulerAngles.x; }
        if (_rig != null) { _offset = _rig.offset; _follow = _rig.followLerp; }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F1)) _show = !_show;
    }

    void OnGUI()
    {
        if (!_show || _cam == null || _rig == null) return;

        const float w = 340f;
        GUILayout.BeginArea(new Rect(12, 12, w, 430), GUI.skin.box);
        GUILayout.Label("<b>调试面板 (F1)</b>", Rich());

        bool frozen = GUILayout.Toggle(DebugControl.Frozen, "  冻结跟随/转向（进 Scene 手调）");
        DebugControl.Frozen = frozen;

        GUILayout.Space(6);
        Slider("相机 FOV", ref _fov, 20f, 90f);
        Slider("俯角 Tilt", ref _tilt, 20f, 85f);
        Slider("偏移 X", ref _offset.x, -20f, 20f);
        Slider("偏移 Y", ref _offset.y, 2f, 30f);
        Slider("偏移 Z", ref _offset.z, -30f, -2f);
        Slider("跟随 Lerp", ref _follow, 1f, 20f);

        // 实时应用到相机
        _cam.fieldOfView = _fov;
        var e = _cam.transform.eulerAngles; _cam.transform.eulerAngles = new Vector3(_tilt, e.y, e.z);
        _rig.offset = _offset;
        _rig.followLerp = _follow;

        GUILayout.Space(8);
#if UNITY_EDITOR
        if (GUILayout.Button("保存到 GameConfig（资产）")) SaveToConfig();
#endif
        if (GUILayout.Button("打印当前数值到 Console")) LogValues();

        GUILayout.Space(4);
        GUILayout.Label("提示：NPC 位置/朝向请在场景里摆 NpcSpawnPoint。", Rich());
        GUILayout.EndArea();
    }

    void Slider(string label, ref float v, float min, float max)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(90));
        v = GUILayout.HorizontalSlider(v, min, max, GUILayout.Width(160));
        GUILayout.Label(v.ToString("0.0"), GUILayout.Width(50));
        GUILayout.EndHorizontal();
    }

    void LogValues()
    {
        Debug.Log($"[Debug] FOV={_fov:0.0} Tilt={_tilt:0.0} Offset=({_offset.x:0.0},{_offset.y:0.0},{_offset.z:0.0}) Follow={_follow:0.0}");
    }

    static GUIStyle _rich;
    static GUIStyle Rich()
    {
        if (_rich == null) _rich = new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true };
        return _rich;
    }

#if UNITY_EDITOR
    void SaveToConfig()
    {
        var cfg = GameConfig.Instance;
        // 如果当前是内存默认（没有资产），先创建资产
        string path = "Assets/Resources/GameData/GameConfig.asset";
        if (!UnityEditor.AssetDatabase.Contains(cfg))
        {
            System.IO.Directory.CreateDirectory("Assets/Resources/GameData");
            var asset = ScriptableObject.CreateInstance<GameConfig>();
            UnityEditor.AssetDatabase.CreateAsset(asset, path);
            cfg = asset;
            GameConfig.SetInstance(cfg);
        }
        cfg.cameraFieldOfView = _fov;
        cfg.cameraTilt = _tilt;
        cfg.cameraOffset = _offset;
        cfg.cameraFollowLerp = _follow;
        UnityEditor.EditorUtility.SetDirty(cfg);
        UnityEditor.AssetDatabase.SaveAssets();
        Debug.Log($"[Debug] 已保存相机参数到 {path}");
    }
#endif
}
