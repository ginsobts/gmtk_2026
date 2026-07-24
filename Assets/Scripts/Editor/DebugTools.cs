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
}
