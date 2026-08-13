namespace JYPPX.ROCm.HipSharp.Types;

/// <summary>
/// 控制 stream capture 与线程中不安全 API 的交互 / Controls stream-capture interaction with unsafe API calls in threads.
/// </summary>
public enum HipStreamCaptureMode
{
    /// <summary>全局捕获规则 / Global capture rules.</summary>
    Global = 0,
    /// <summary>当前线程捕获规则 / Current-thread capture rules.</summary>
    ThreadLocal = 1,
    /// <summary>宽松捕获规则 / Relaxed capture rules.</summary>
    Relaxed = 2,
}
