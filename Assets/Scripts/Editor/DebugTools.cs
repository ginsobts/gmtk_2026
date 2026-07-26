using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>调试/布景相关的编辑器菜单：创建配置资产、生成出生点脚手架。</summary>
public static class DebugTools
{
    const string ConfigPath = "Assets/Resources/GameData/GameConfig.asset";

    [MenuItem("GMTK/创建 GameConfig 资产", priority = 60)]
    public static void CreateGameConfig()
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
        if (existing != null)
        {
            Selection.activeObject = existing;
            EditorGUIUtility.PingObject(existing);
            Debug.Log($"GameConfig 已存在：{ConfigPath}");
            return;
        }

        Directory.CreateDirectory("Assets/Resources/GameData");
        var cfg = ScriptableObject.CreateInstance<GameConfig>();
        AssetDatabase.CreateAsset(cfg, ConfigPath);
        AssetDatabase.SaveAssets();
        Selection.activeObject = cfg;
        EditorGUIUtility.PingObject(cfg);
        Debug.Log($"已创建 GameConfig：{ConfigPath}（在 Inspector 里调参，改完下次 Play 生效）");
    }

    [MenuItem("GMTK/创建出生点脚手架", priority = 61)]
    public static void CreateSpawnScaffold()
    {
        var root = new GameObject("SpawnPoints");
        Undo.RegisterCreatedObjectUndo(root, "Create Spawn Points");

        var player = new GameObject("PlayerSpawn");
        player.AddComponent<PlayerSpawnPoint>();
        player.transform.SetParent(root.transform);
        player.transform.position = new Vector3(0f, 0f, -6f);

        // 8 个 NPC 出生点，摆成两排，默认朝向相机
        int idx = 0;
        for (int row = 0; row < 2; row++)
            for (int col = 0; col < 4; col++)
            {
                var sp = new GameObject($"NpcSpawn_{idx:00}");
                sp.AddComponent<NpcSpawnPoint>();
                sp.transform.SetParent(root.transform);
                sp.transform.position = new Vector3(-9f + col * 6f, 0f, 6f - row * 8f);
                idx++;
            }

        Selection.activeGameObject = root;
        Debug.Log("已在场景创建出生点脚手架：拖动 NpcSpawn 改位置、旋转 Y 轴改朝向。多于出生点数量的 NPC 会随机生成。");
    }

    [MenuItem("GMTK/树木：生成可调标记", priority = 62)]
    public static void GenerateTreeMarkers()
    {
        var existing = Object.FindObjectsByType<TreeMarker>(FindObjectsSortMode.None);
        if (existing.Length > 0 && !EditorUtility.DisplayDialog("生成树木标记",
            $"场景里已有 {existing.Length} 个树木标记。继续会再生成一份默认布局（不会删旧的，可能重叠）。是否继续？", "继续", "取消"))
            return;

        var root = new GameObject("Tree Markers");
        Undo.RegisterCreatedObjectUndo(root, "Generate Tree Markers");

        var layout = GameManager.DefaultTreeLayout();
        int i = 0;
        foreach (var d in layout)
        {
            var go = new GameObject($"Tree_{i:000}_{(d.isBush ? "bush" : "tree")}");
            var m = go.AddComponent<TreeMarker>();
            m.kind = d.isBush ? TreeMarker.Kind.Bush : TreeMarker.Kind.Tree;
            m.scale = d.scale;
            m.sorting = d.sorting;
            m.obstacle = d.obstacle;
            m.obstacleRadius = d.radius;
            go.transform.SetParent(root.transform);
            go.transform.position = d.pos;
            i++;
        }

        Selection.activeGameObject = root;
        Debug.Log($"已生成 {i} 个树木标记（Tree Markers）。在 Scene 视图里拖动/增删/改参数，Play 时森林会【完全】按这些标记生成。" +
                  "删掉全部标记则回退到内置默认布局。（记得 Ctrl+S 保存场景）");
    }

    [MenuItem("GMTK/树木：清除全部标记", priority = 63)]
    public static void ClearTreeMarkers()
    {
        var markers = Object.FindObjectsByType<TreeMarker>(FindObjectsSortMode.None);
        if (markers.Length == 0) { Debug.Log("场景里没有树木标记。"); return; }

        var parents = new System.Collections.Generic.HashSet<Transform>();
        foreach (var m in markers)
        {
            if (m.transform.parent != null) parents.Add(m.transform.parent);
            Undo.DestroyObjectImmediate(m.gameObject);
        }
        foreach (var p in parents)
            if (p != null && p.name == "Tree Markers" && p.childCount == 0)
                Undo.DestroyObjectImmediate(p.gameObject);

        Debug.Log("已清除全部树木标记，Play 时回退到内置默认布局。");
    }
}
