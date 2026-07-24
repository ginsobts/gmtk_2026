using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 编辑器场景预览：不按 Play，也能在 Scene 视图里看到地面、游戏相机取景、
/// 玩家与每个出生点上的角色占位，方便直接对着调相机参数和摆位。
/// 预览对象带 DontSave 标记：不会存进场景、不会进包；进入 Play 前会自动清除。
/// 菜单：
///   GMTK/预览场景（编辑器）   Ctrl+Shift+P
///   GMTK/清除场景预览
///   GMTK/Scene 视角对齐游戏相机
/// </summary>
[InitializeOnLoad]
public static class ScenePreview
{
    const string RootName = "__GMTK_PREVIEW__";
    const HideFlags Ephemeral = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;

    static ScenePreview()
    {
        // 进入 Play 前清掉预览，避免和运行时生成的场景重叠
        EditorApplication.playModeStateChanged += s =>
        {
            if (s == PlayModeStateChange.ExitingEditMode) Clear();
        };
    }

    static GameConfig Cfg()
    {
        var cfg = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/Resources/GameData/GameConfig.asset");
        return cfg != null ? cfg : ScriptableObject.CreateInstance<GameConfig>();
    }

    [MenuItem("GMTK/预览场景（编辑器） %#p", priority = 62)]
    public static void Build()
    {
        Clear();
        var cfg = Cfg();

        var root = new GameObject(RootName) { hideFlags = Ephemeral };

        // 地面
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "PreviewGround";
        ground.transform.SetParent(root.transform);
        ground.transform.localScale = new Vector3(4f, 1f, 4f);
        var mat = new Material(Shader.Find("Standard")) { hideFlags = Ephemeral };
        mat.mainTexture = GeneratedArt.GroundTexture;
        mat.mainTextureScale = new Vector2(5f, 5f);
        ground.GetComponent<Renderer>().sharedMaterial = mat;

        // 游戏相机（用于取景/对齐 Scene 视角）
        Vector3 playerPos = cfg.playerStart;
        var playerSp = Object.FindFirstObjectByType<PlayerSpawnPoint>();
        if (playerSp != null) playerPos = playerSp.transform.position;

        var camGO = new GameObject("PreviewCamera");
        camGO.transform.SetParent(root.transform);
        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.14f, 0.16f, 0.2f);
        cam.fieldOfView = cfg.cameraFieldOfView;
        camGO.transform.rotation = Quaternion.Euler(cfg.cameraTilt, 0f, 0f);
        camGO.transform.position = playerPos + cfg.cameraOffset;
        Quaternion camRot = camGO.transform.rotation;

        // 玩家占位
        MakeCard(root.transform, "PreviewPlayer", GeneratedArt.PlayerSprite, playerPos, cfg.playerScale, 0f, camRot);

        // NPC 占位：优先按出生点，否则用一排默认网格
        var spawns = new List<NpcSpawnPoint>(Object.FindObjectsByType<NpcSpawnPoint>(FindObjectsSortMode.None));
        spawns.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

        var chars = GameContent.Characters;
        int count = spawns.Count > 0 ? spawns.Count : Mathf.Min(8, chars.Count);

        for (int i = 0; i < count; i++)
        {
            var def = chars[i % Mathf.Max(1, chars.Count)];
            Sprite sprite = GeneratedArt.GetCharacterSprite(def.artFolder);

            Vector3 pos;
            float yaw;
            if (spawns.Count > 0)
            {
                pos = spawns[i].transform.position;
                yaw = spawns[i].ResolvedYaw;
            }
            else
            {
                pos = new Vector3(-9f + (i % 4) * 6f, 0f, 6f - (i / 4) * 8f);
                yaw = cfg.npcDefaultYaw;
            }
            MakeCard(root.transform, "PreviewNPC_" + i, sprite, pos, cfg.npcScale, yaw, camRot);
        }

        AlignSceneViewTo(camGO.transform);
        Debug.Log("[预览] 已生成编辑器预览。移动出生点 / 改 GameConfig 后，重跑一次即可刷新。进入 Play 会自动清除。");
    }

    [MenuItem("GMTK/清除场景预览", priority = 63)]
    public static void Clear()
    {
        var existing = GameObject.Find(RootName);
        if (existing != null) Object.DestroyImmediate(existing);
    }

    [MenuItem("GMTK/Scene 视角对齐游戏相机", priority = 64)]
    public static void AlignSceneViewToGameCamera()
    {
        var cfg = Cfg();
        Vector3 playerPos = cfg.playerStart;
        var playerSp = Object.FindFirstObjectByType<PlayerSpawnPoint>();
        if (playerSp != null) playerPos = playerSp.transform.position;

        var temp = new GameObject("__align_tmp__") { hideFlags = HideFlags.HideAndDontSave };
        temp.transform.rotation = Quaternion.Euler(cfg.cameraTilt, 0f, 0f);
        temp.transform.position = playerPos + cfg.cameraOffset;
        AlignSceneViewTo(temp.transform);
        Object.DestroyImmediate(temp);
    }

    static void AlignSceneViewTo(Transform t)
    {
        var sv = SceneView.lastActiveSceneView;
        if (sv != null) { sv.AlignViewToObject(t); sv.Repaint(); }
    }

    static void MakeCard(Transform parent, string name, Sprite sprite, Vector3 pos, float scale, float yaw, Quaternion camRot)
    {
        var go = new GameObject(name) { hideFlags = Ephemeral };
        go.transform.SetParent(parent);
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * scale;
        go.transform.rotation = Quaternion.AngleAxis(yaw, Vector3.up) * camRot;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 10;
        if (sprite != null) sr.sprite = sprite;
        else sr.color = new Color(0.8f, 0.4f, 0.4f); // 缺图时给个占位色
    }
}
