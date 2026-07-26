using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// 策划配置表工具：在 Excel 里维护内容，一键在 Excel 与游戏读取的 txt 之间转换。
/// 需要本机装有 Python 与 openpyxl（pip install openpyxl）。
/// </summary>
public static class ConfigTableTools
{
    static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;
    static string XlsxPath => Path.Combine(ProjectRoot, "tools", "game_tables.xlsx");

    [MenuItem("GMTK/配置表：Excel → txt（导入游戏）", priority = 40)]
    public static void ExcelToTxt()
    {
        if (RunPython("tools/export_tables.py"))
        {
            AssetDatabase.Refresh();
            Debug.Log("配置表已从 Excel 导出为 txt 并刷新，下次 Play 生效。");
        }
    }

    [MenuItem("GMTK/配置表：txt → Excel（生成可编辑表）", priority = 41)]
    public static void TxtToExcel()
    {
        if (RunPython("tools/import_tables.py"))
        {
            var path = File.Exists(XlsxPath)
                ? XlsxPath
                : Path.Combine(ProjectRoot, "tools", "game_tables.imported.xlsx");
            Debug.Log($"已生成可编辑 Excel：{path}");
            if (File.Exists(path)) EditorUtility.RevealInFinder(path);
        }
    }

    [MenuItem("GMTK/配置表：打开 Excel", priority = 42)]
    public static void OpenExcel()
    {
        if (File.Exists(XlsxPath))
            EditorUtility.RevealInFinder(XlsxPath);
        else
            Debug.LogWarning($"还没有 {XlsxPath}。先用菜单「配置表：txt → Excel」生成一个。");
    }

    /// <summary>依次尝试 py / python / python3 运行脚本，输出打到 Console。</summary>
    static bool RunPython(string scriptRelPath)
    {
        string[] candidates = { "py", "python", "python3" };
        foreach (var exe in candidates)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = "\"" + scriptRelPath + "\"",
                    WorkingDirectory = ProjectRoot,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };

                using (var p = Process.Start(psi))
                {
                    string stdout = p.StandardOutput.ReadToEnd();
                    string stderr = p.StandardError.ReadToEnd();
                    p.WaitForExit();

                    if (!string.IsNullOrWhiteSpace(stdout)) Debug.Log(stdout.Trim());
                    if (p.ExitCode == 0)
                        return true;

                    Debug.LogError($"[{exe}] 运行 {scriptRelPath} 失败（退出码 {p.ExitCode}）：\n{stderr.Trim()}");
                    return false; // 进程能起但脚本报错：不再换解释器，直接反馈
                }
            }
            catch (Exception)
            {
                // 该解释器不存在，尝试下一个
            }
        }

        Debug.LogError(
            "找不到 Python。请安装 Python 并 pip install openpyxl，" +
            "或在项目根目录手动运行： python tools/export_tables.py");
        return false;
    }
}
