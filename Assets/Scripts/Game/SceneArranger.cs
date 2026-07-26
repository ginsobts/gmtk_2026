using System.Text;
using UnityEngine;

/// <summary>
/// 运行时「摆位模式」：在真正的游戏画面里用鼠标拖动 NPC 棋子，调好后一键写回 spawns.txt。
/// 因为整张地图是运行时程序化生成的、在 Scene 视图里没有实体可拖，所以直接在 Play 里就地摆。
/// 仅编辑器 / 开发包挂载（见 GameManager.BuildPlayerAndCamera）。
///
/// 用法：
///   F2   进入 / 退出摆位模式（进入时会冻结场景，棋子不再抖动）
///   左键 在棋子附近按下并拖动 = 把该棋子挪到落点
///   保存 点面板上的按钮，把当前所有棋子位置写回 Assets/Resources/GameData/spawns.txt
/// </summary>
public class SceneArranger : MonoBehaviour
{
    Camera _cam;
    bool _active;
    bool _prevFrozen;
    Npc _dragging;
    string _status = "";
    readonly Plane _ground = new Plane(Vector3.up, Vector3.zero);

    const float PickRadius = 2.8f;

    public void Init(Camera cam) { _cam = cam; }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F2)) Toggle();
        if (!_active || _cam == null) return;

        // 鼠标在面板区域内时不拖棋子，避免和「保存」按钮冲突
        Vector2 gui = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
        if (_panelRect.Contains(gui)) { if (Input.GetMouseButtonUp(0)) _dragging = null; return; }

        if (Input.GetMouseButtonDown(0)) _dragging = PickNpc();
        if (Input.GetMouseButtonUp(0)) _dragging = null;

        if (_dragging != null && Input.GetMouseButton(0) && TryGround(out var p))
        {
            var cfg = GameConfig.Instance;
            p.x = Mathf.Clamp(p.x, cfg.spawnAreaX.x, cfg.spawnAreaX.y);
            p.z = Mathf.Clamp(p.z, cfg.spawnAreaZ.x, cfg.spawnAreaZ.y);
            _dragging.transform.position = new Vector3(p.x, 0f, p.z);
        }
    }

    void Toggle()
    {
        _active = !_active;
        if (_active) { _prevFrozen = DebugControl.Frozen; DebugControl.Frozen = true; }
        else { DebugControl.Frozen = _prevFrozen; _dragging = null; }
    }

    Npc PickNpc()
    {
        if (!TryGround(out var p)) return null;
        var gm = GameManager.Instance;
        if (gm == null) return null;
        Npc best = null;
        float bestDist = PickRadius * PickRadius;
        foreach (var n in gm.Npcs)
        {
            if (n == null) continue;
            Vector3 d = n.transform.position - p; d.y = 0f;
            float sq = d.sqrMagnitude;
            if (sq < bestDist) { bestDist = sq; best = n; }
        }
        return best;
    }

    bool TryGround(out Vector3 point)
    {
        point = Vector3.zero;
        Ray ray = _cam.ScreenPointToRay(Input.mousePosition);
        if (_ground.Raycast(ray, out float enter)) { point = ray.GetPoint(enter); return true; }
        return false;
    }

    Rect _panelRect = new Rect(12, 12, 340, 150);

    void OnGUI()
    {
        if (!_active) return;
        _panelRect = GUILayout.Window(0xA11ACE, _panelRect, DrawPanel, "摆位模式 (F2 退出)");
    }

    void DrawPanel(int id)
    {
        GUILayout.Label("左键在棋子附近按住并拖动 = 挪动该棋子");
        GUILayout.Label(_dragging != null ? "拖动中：" + _dragging.name : "未选中棋子");
        GUILayout.Space(4);
        if (GUILayout.Button("保存位置到 spawns.txt")) Save();
        if (!string.IsNullOrEmpty(_status)) GUILayout.Label(_status);
        GUI.DragWindow();
    }

    void Save()
    {
        var gm = GameManager.Instance;
        if (gm == null) { _status = "没有 GameManager"; return; }

        var sb = new StringBuilder();
        sb.AppendLine("# 固定出生点（Tab 分隔）。每个角色一行，坐标为世界坐标(x,z)，y 恒为 0。");
        sb.AppendLine("# yaw：绕世界 Y 的朝向（度）；faceCamera=1 时始终正对相机(billboard)，yaw 被忽略。");
        sb.AppendLine("# 可在运行时(编辑器/开发版)按 F2 进入「摆位模式」，鼠标拖动棋子后按「保存」写回本表。");
        sb.AppendLine("charId\tx\tz\tyaw\tfaceCamera");
        foreach (var n in gm.Npcs)
        {
            if (n == null || string.IsNullOrEmpty(n.charId)) continue;
            Vector3 pos = n.transform.position;
            n.TryGetFacing(out float yaw, out bool face);
            sb.AppendLine($"{n.charId}\t{pos.x:0.##}\t{pos.z:0.##}\t{yaw:0.##}\t{(face ? 1 : 0)}");
        }

#if UNITY_EDITOR
        string path = System.IO.Path.Combine(Application.dataPath, "Resources/GameData/spawns.txt");
        System.IO.File.WriteAllText(path, sb.ToString());
        UnityEditor.AssetDatabase.Refresh();
        _status = "已保存：" + path;
        Debug.Log("[SceneArranger] " + _status);
#else
        _status = "仅编辑器可写回 spawns.txt。当前布局：\n" + sb;
        Debug.Log("[SceneArranger] 运行时布局：\n" + sb);
#endif
    }
}
