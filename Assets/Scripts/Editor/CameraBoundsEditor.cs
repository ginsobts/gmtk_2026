using UnityEditor;
using UnityEngine;

/// <summary>
/// 相机范围框的编辑器支持：
/// 1) 菜单「GMTK/创建相机范围框」在场景里放一个 CameraBounds（默认取 GameConfig 的框大小）。
/// 2) 选中它时，在 Scene 视图里拖动四条边即可"画/改"这个框；拖动物体本身则整体平移。
/// </summary>
[CustomEditor(typeof(CameraBounds))]
public class CameraBoundsEditor : Editor
{
    void OnSceneGUI()
    {
        var b = (CameraBounds)target;
        Vector3 p = b.transform.position;
        float cx = p.x, cz = p.z;
        float minX = cx - b.size.x * 0.5f, maxX = cx + b.size.x * 0.5f;
        float minZ = cz - b.size.y * 0.5f, maxZ = cz + b.size.y * 0.5f;

        // 半透明填充 + 描边
        Vector3[] verts =
        {
            new Vector3(minX, 0f, minZ), new Vector3(maxX, 0f, minZ),
            new Vector3(maxX, 0f, maxZ), new Vector3(minX, 0f, maxZ)
        };
        Handles.DrawSolidRectangleWithOutline(verts, new Color(0.3f, 0.7f, 1f, 0.08f), new Color(0.3f, 0.7f, 1f, 0.9f));

        float hs = HandleUtility.GetHandleSize(p) * 0.12f;
        EditorGUI.BeginChangeCheck();

        // 四条边中点各一个拖拽手柄（沿对应轴滑动）
        Vector3 eNew = Handles.Slider(new Vector3(maxX, 0f, cz), Vector3.right, hs, Handles.CubeHandleCap, 0f);
        Vector3 wNew = Handles.Slider(new Vector3(minX, 0f, cz), Vector3.right, hs, Handles.CubeHandleCap, 0f);
        Vector3 nNew = Handles.Slider(new Vector3(cx, 0f, maxZ), Vector3.forward, hs, Handles.CubeHandleCap, 0f);
        Vector3 sNew = Handles.Slider(new Vector3(cx, 0f, minZ), Vector3.forward, hs, Handles.CubeHandleCap, 0f);

        if (EditorGUI.EndChangeCheck())
        {
            // 拖某条边：对边固定，更新 size 与中心
            float nMinX = Mathf.Min(wNew.x, eNew.x - 0.2f);
            float nMaxX = Mathf.Max(eNew.x, wNew.x + 0.2f);
            float nMinZ = Mathf.Min(sNew.z, nNew.z - 0.2f);
            float nMaxZ = Mathf.Max(nNew.z, sNew.z + 0.2f);

            Undo.RecordObject(b, "Resize Camera Bounds");
            Undo.RecordObject(b.transform, "Resize Camera Bounds");
            b.size = new Vector2(nMaxX - nMinX, nMaxZ - nMinZ);
            b.transform.position = new Vector3((nMinX + nMaxX) * 0.5f, p.y, (nMinZ + nMaxZ) * 0.5f);
            EditorUtility.SetDirty(b);
        }

        Handles.Label(new Vector3(cx, 0f, maxZ) + Vector3.forward * 0.6f,
            $"相机范围框  {b.size.x:0.#} x {b.size.y:0.#}");
    }

    [MenuItem("GMTK/创建相机范围框", priority = 64)]
    public static void CreateCameraBounds()
    {
        var existing = Object.FindFirstObjectByType<CameraBounds>();
        if (existing != null)
        {
            Selection.activeGameObject = existing.gameObject;
            EditorGUIUtility.PingObject(existing);
            Debug.Log("场景里已有相机范围框，已选中它。拖动物体平移、拖四条边改大小。");
            return;
        }

        var cfg = GameConfig.Instance;
        var go = new GameObject("Camera Bounds");
        Undo.RegisterCreatedObjectUndo(go, "Create Camera Bounds");
        var cb = go.AddComponent<CameraBounds>();
        cb.size = cfg.cameraBoundsSize;
        go.transform.position = new Vector3(cfg.cameraBoundsCenter.x, 0f, cfg.cameraBoundsCenter.y);

        Selection.activeGameObject = go;
        Debug.Log("已创建相机范围框：在 Scene 视图里拖动物体平移、拖四条边改大小。Play 时镜头只在这个框内跟随角色。记得 Ctrl+S 保存场景。");
    }
}
