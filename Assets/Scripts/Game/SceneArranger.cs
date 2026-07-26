using System.Text;
using UnityEngine;

/// <summary>
/// 运行时「摆位模式」：在真正的游戏画面里用鼠标拖动 NPC 棋子，调好后一键写回 spawns.txt。
/// 因为整张地图是运行时程序化生成的、在 Scene 视图里没有实体可拖，所以直接在 Play 里就地摆。
/// 仅编辑器 / 开发包挂载（见 GameManager.BuildPlayerAndCamera）。
///
/// 用法：
///   F2   进入 / 退出摆位模式（进入时会冻结场景，棋子不再抖动）
///   左键 在棋子附近按下并拖动 = 把该棋子挪到落点（NPC 和「玩家」都能拖）
///   保存 点面板上的按钮，把当前所有 NPC 位置写回 spawns.txt；玩家出生点写回 GameConfig（仅基准）
/// </summary>
public class SceneArranger : MonoBehaviour
{
    Camera _cam;
    bool _active;
    bool _prevFrozen;
    Npc _draggingNpc;
    bool _draggingPlayer;
    Transform _player;
    string _status = "";
    int _savePhase = 1;   // 1 = 基准(spawns.txt)；>=2 = 该阶段(phase_spawns.txt)
    readonly Plane _ground = new Plane(Vector3.up, Vector3.zero);

    const float PickRadius = 2.8f;

    public void Init(Camera cam) { _cam = cam; }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F2)) Toggle();
        if (!_active || _cam == null) return;

        // 鼠标在面板区域内时不拖棋子，避免和「保存」按钮冲突
        Vector2 gui = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
        if (_panelRect.Contains(gui)) { if (Input.GetMouseButtonUp(0)) ClearDrag(); return; }

        if (Input.GetMouseButtonDown(0)) Pick();
        if (Input.GetMouseButtonUp(0)) ClearDrag();

        if ((_draggingNpc != null || _draggingPlayer) && Input.GetMouseButton(0) && TryGround(out var p))
        {
            var cfg = GameConfig.Instance;
            if (_draggingPlayer)
            {
                // 玩家夹到空气墙范围内
                p.x = Mathf.Clamp(p.x, -cfg.mapHalfX, cfg.mapHalfX);
                p.z = Mathf.Clamp(p.z, -cfg.mapHalfZ, cfg.mapHalfZ);
                var pl = Player();
                if (pl != null) pl.position = new Vector3(p.x, 0f, p.z);
            }
            else
            {
                p.x = Mathf.Clamp(p.x, cfg.spawnAreaX.x, cfg.spawnAreaX.y);
                p.z = Mathf.Clamp(p.z, cfg.spawnAreaZ.x, cfg.spawnAreaZ.y);
                _draggingNpc.transform.position = new Vector3(p.x, 0f, p.z);
            }
        }
    }

    void ClearDrag() { _draggingNpc = null; _draggingPlayer = false; }

    void Toggle()
    {
        _active = !_active;
        if (_active) { _prevFrozen = DebugControl.Frozen; DebugControl.Frozen = true; }
        else { DebugControl.Frozen = _prevFrozen; ClearDrag(); }
    }

    Transform Player()
    {
        if (_player == null)
        {
            var pc = Object.FindFirstObjectByType<PlayerController>();
            if (pc != null) _player = pc.transform;
        }
        return _player;
    }

    /// <summary>在落点附近选中最近的棋子（NPC 或玩家）。</summary>
    void Pick()
    {
        ClearDrag();
        if (!TryGround(out var p)) return;
        var gm = GameManager.Instance;
        if (gm == null) return;
        float bestDist = PickRadius * PickRadius;
        foreach (var n in gm.Npcs)
        {
            if (n == null) continue;
            Vector3 d = n.transform.position - p; d.y = 0f;
            float sq = d.sqrMagnitude;
            if (sq < bestDist) { bestDist = sq; _draggingNpc = n; _draggingPlayer = false; }
        }
        var pl = Player();
        if (pl != null)
        {
            Vector3 d = pl.position - p; d.y = 0f;
            float sq = d.sqrMagnitude;
            if (sq < bestDist) { bestDist = sq; _draggingNpc = null; _draggingPlayer = true; }
        }
    }

    bool TryGround(out Vector3 point)
    {
        point = Vector3.zero;
        Ray ray = _cam.ScreenPointToRay(Input.mousePosition);
        if (_ground.Raycast(ray, out float enter)) { point = ray.GetPoint(enter); return true; }
        return false;
    }

    Rect _panelRect = new Rect(12, 12, 360, 220);

    void OnGUI()
    {
        if (!_active) return;
        _panelRect = GUILayout.Window(0xA11ACE, _panelRect, DrawPanel, "摆位模式 (F2 退出)");
    }

    void DrawPanel(int id)
    {
        GUILayout.Label("左键在棋子附近按住并拖动 = 挪动该棋子（含玩家）");
        string sel = _draggingPlayer ? "玩家" : (_draggingNpc != null ? _draggingNpc.name : null);
        GUILayout.Label(sel != null ? "拖动中：" + sel : "未选中棋子");
        GUILayout.Space(4);

        GUILayout.BeginHorizontal();
        GUILayout.Label(_savePhase <= 1 ? "目标：基准(全阶段默认)" : "目标：阶段 " + _savePhase);
        if (GUILayout.Button("-", GUILayout.Width(28))) _savePhase = Mathf.Max(1, _savePhase - 1);
        if (GUILayout.Button("+", GUILayout.Width(28))) _savePhase = Mathf.Min(9, _savePhase + 1);
        GUILayout.EndHorizontal();

        if (GUILayout.Button("载入该目标已存位置")) LoadIntoScene(_savePhase);
        if (GUILayout.Button(_savePhase <= 1 ? "保存到 spawns.txt(基准)" : $"保存到 phase_spawns.txt(阶段{_savePhase})")) Save(_savePhase);
        if (!string.IsNullOrEmpty(_status)) GUILayout.Label(_status);
        GUI.DragWindow();
    }

    /// <summary>把某目标(基准/某阶段)已存的坐标载入到当前棋子上，方便对着编辑。</summary>
    void LoadIntoScene(int phase)
    {
        var gm = GameManager.Instance;
        if (gm == null) return;
        int moved = 0;
        foreach (var n in gm.Npcs)
        {
            if (n == null || string.IsNullOrEmpty(n.charId)) continue;
            var s = phase <= 1 ? GameContent.GetSpawn(n.charId) : GameContent.GetPhaseSpawn(n.charId, phase);
            if (s == null) continue;
            n.transform.position = new Vector3(s.x, 0f, s.z);
            n.SetFacing(s.yaw, s.faceCamera);
            moved++;
        }
        // 玩家没有分阶段，只在基准时把它摆到 GameConfig 里的起点
        if (phase <= 1)
        {
            var pl = Player();
            if (pl != null) pl.position = GameConfig.Instance.playerStart;
        }
        _status = $"已载入 {moved} 个棋子（{(phase <= 1 ? "基准" : "阶段" + phase)}）";
    }

    void Save(int phase)
    {
        var gm = GameManager.Instance;
        if (gm == null) { _status = "没有 GameManager"; return; }

        if (phase <= 1) SaveBase(gm);
        else SavePhase(gm, phase);
    }

    void SaveBase(GameManager gm)
    {
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
        WriteFile("spawns.txt", sb.ToString());
        SavePlayerStart();   // 玩家出生点随基准一起保存（写回 GameConfig）
    }

    /// <summary>把当前玩家棋子的位置写回 GameConfig.playerStart（仅编辑器持久化）。</summary>
    void SavePlayerStart()
    {
        var pl = Player();
        if (pl == null) return;
        Vector3 pos = pl.position;
#if UNITY_EDITOR
        var cfg = GameConfig.Instance;
        string path = "Assets/Resources/GameData/GameConfig.asset";
        if (!UnityEditor.AssetDatabase.Contains(cfg))
        {
            System.IO.Directory.CreateDirectory("Assets/Resources/GameData");
            var asset = ScriptableObject.CreateInstance<GameConfig>();
            UnityEditor.AssetDatabase.CreateAsset(asset, path);
            cfg = asset; GameConfig.SetInstance(cfg);
        }
        cfg.playerStart = new Vector3(pos.x, 0f, pos.z);
        UnityEditor.EditorUtility.SetDirty(cfg);
        UnityEditor.AssetDatabase.SaveAssets();
        _status += "；玩家出生点→GameConfig";
        if (Object.FindFirstObjectByType<PlayerSpawnPoint>() != null)
            _status += "（注意：场景里有 PlayerSpawn 物体会覆盖它）";
        Debug.Log($"[SceneArranger] 玩家出生点已写回 GameConfig: {cfg.playerStart}");
#else
        Debug.Log($"[SceneArranger] 玩家出生点(仅编辑器可写盘): {pos}");
#endif
    }

    /// <summary>写 phase_spawns.txt 里「本阶段」的行，保留其它阶段已有的行。</summary>
    void SavePhase(GameManager gm, int phase)
    {
        var kept = new System.Collections.Generic.List<string>();
#if UNITY_EDITOR
        string path = System.IO.Path.Combine(Application.dataPath, "Resources/GameData/phase_spawns.txt");
        if (System.IO.File.Exists(path))
        {
            foreach (var raw in System.IO.File.ReadAllLines(path))
            {
                string line = raw.TrimEnd('\r');
                if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#")) continue;
                var c = line.Split('\t');
                if (c.Length < 2) continue;
                if (c[0].Trim() == "charId") continue;                 // 表头
                if (int.TryParse(c[1].Trim(), out int p) && p == phase) continue; // 丢掉本阶段旧行
                kept.Add(line);
            }
        }
#endif
        var sb = new StringBuilder();
        sb.AppendLine("# 每阶段坐标覆盖（Tab 分隔）。charId phase x z yaw faceCamera。");
        sb.AppendLine("# 只需要写「和基准不同」的角色/阶段；没写的 = 沿用基准 spawns.txt，位置不变。");
        sb.AppendLine("charId\tphase\tx\tz\tyaw\tfaceCamera");
        foreach (var k in kept) sb.AppendLine(k);
        foreach (var n in gm.Npcs)
        {
            if (n == null || string.IsNullOrEmpty(n.charId)) continue;
            Vector3 pos = n.transform.position;
            n.TryGetFacing(out float yaw, out bool face);
            sb.AppendLine($"{n.charId}\t{phase}\t{pos.x:0.##}\t{pos.z:0.##}\t{yaw:0.##}\t{(face ? 1 : 0)}");
        }
        WriteFile("phase_spawns.txt", sb.ToString());
    }

    void WriteFile(string fileName, string content)
    {
#if UNITY_EDITOR
        string path = System.IO.Path.Combine(Application.dataPath, "Resources/GameData/" + fileName);
        System.IO.File.WriteAllText(path, content);
        UnityEditor.AssetDatabase.Refresh();
        _status = "已保存：" + fileName;
        Debug.Log("[SceneArranger] 已写回 " + path);
#else
        _status = "仅编辑器可写盘。内容已打印到 Console。";
        Debug.Log("[SceneArranger] " + fileName + "\n" + content);
#endif
    }
}
