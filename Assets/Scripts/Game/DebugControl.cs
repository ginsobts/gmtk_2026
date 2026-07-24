/// <summary>
/// 调试全局开关。Frozen = true 时，相机跟随、billboard 转向、呼吸/摇摆、掉帧抖动等
/// 每帧改写 Transform 的逻辑都会暂停，方便在 Play 模式下进 Scene 视图自由观察/拖动而不被改回去。
/// </summary>
public static class DebugControl
{
    public static bool Frozen;
}
