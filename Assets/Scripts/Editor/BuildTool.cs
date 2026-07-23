using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// 一键打包工具。菜单栏 GMTK ▸ … 里点一下即可出包。
/// 也可命令行调用（CI）：
///   Unity -quit -batchmode -projectPath . -executeMethod BuildTool.BuildWindows
/// 输出在项目根目录的 Build/ 下（已在 .gitignore 里忽略，不会进仓库）。
/// </summary>
public static class BuildTool
{
    const string OutputRoot = "Build";

    [MenuItem("GMTK/一键打包（当前平台） %#b", priority = 0)]
    public static void BuildCurrentPlatform()
    {
        Build(EditorUserBuildSettings.activeBuildTarget);
    }

    [MenuItem("GMTK/打包 Windows", priority = 20)]
    public static void BuildWindows()
    {
        Build(BuildTarget.StandaloneWindows64);
    }

    [MenuItem("GMTK/打包 WebGL", priority = 21)]
    public static void BuildWebGL()
    {
        Build(BuildTarget.WebGL);
    }

    [MenuItem("GMTK/打开打包输出目录", priority = 40)]
    public static void OpenOutputFolder()
    {
        string full = Path.GetFullPath(OutputRoot);
        Directory.CreateDirectory(full);
        EditorUtility.RevealInFinder(full);
    }

    static void Build(BuildTarget target)
    {
        string[] scenes = GetScenes();
        if (scenes.Length == 0)
        {
            EditorUtility.DisplayDialog("打包失败", "没有可用场景。请在 Assets/Scenes 里保留至少一个场景。", "好");
            return;
        }

        string subDir = Path.Combine(OutputRoot, target.ToString());
        string product = string.IsNullOrEmpty(PlayerSettings.productName) ? "game" : PlayerSettings.productName;
        string location = target == BuildTarget.StandaloneWindows64
            ? Path.Combine(subDir, product + ".exe")
            : subDir; // WebGL / 其它平台输出到文件夹

        Directory.CreateDirectory(subDir);

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = location,
            target = target,
            targetGroup = BuildPipeline.GetBuildTargetGroup(target),
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            string outFull = Path.GetFullPath(subDir);
            double mb = summary.totalSize / (1024.0 * 1024.0);
            Debug.Log($"[BuildTool] 打包成功（{target}）：{outFull}  |  大小 {mb:F1} MB  |  用时 {summary.totalTime.TotalSeconds:F0}s");
            if (EditorUtility.DisplayDialog("打包成功",
                    $"平台：{target}\n输出：{outFull}\n大小：{mb:F1} MB", "打开目录", "关闭"))
                EditorUtility.RevealInFinder(outFull);
        }
        else
        {
            Debug.LogError($"[BuildTool] 打包失败（{target}）：{summary.result}，错误 {summary.totalErrors} 个");
            EditorUtility.DisplayDialog("打包失败",
                $"平台：{target}\n结果：{summary.result}\n请查看 Console 里的报错。", "好");
        }
    }

    /// <summary>优先用 Build Settings 里勾选的场景；为空则回退到工程里找到的所有场景。</summary>
    static string[] GetScenes()
    {
        var enabled = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        if (enabled.Length > 0) return enabled;

        return AssetDatabase.FindAssets("t:Scene")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => p.StartsWith("Assets/"))
            .ToArray();
    }
}
