namespace JYPPX.HipSharp.Types;

/// <summary>
/// 表示 HIP Runtime 错误码；未列出的原生数值仍会原样保留 / Represents a HIP Runtime error code; unlisted native values remain preserved.
/// </summary>
public enum HipError
{
    /// <summary>操作成功 / The operation succeeded.</summary>
    Success = 0,

    /// <summary>参数值无效 / An argument value is invalid.</summary>
    InvalidValue = 1,

    /// <summary>设备内存不足 / Device memory is exhausted.</summary>
    OutOfMemory = 2,

    /// <summary>HIP Runtime 尚未初始化 / The HIP Runtime is not initialized.</summary>
    NotInitialized = 3,

    /// <summary>HIP Runtime 已反初始化 / The HIP Runtime has been deinitialized.</summary>
    Deinitialized = 4,

    /// <summary>内存复制方向无效 / The memory copy direction is invalid.</summary>
    InvalidMemcpyDirection = 21,

    /// <summary>没有可用的 HIP 设备 / No HIP device is available.</summary>
    NoDevice = 100,

    /// <summary>设备序号无效 / The device ordinal is invalid.</summary>
    InvalidDevice = 101,

    /// <summary>异步操作尚未完成 / An asynchronous operation is not complete.</summary>
    NotReady = 600,

    /// <summary>当前设备已经启用 peer access / Peer access is already enabled for the current device.</summary>
    PeerAccessAlreadyEnabled = 704,

    /// <summary>当前设备尚未启用 peer access / Peer access is not enabled for the current device.</summary>
    PeerAccessNotEnabled = 705,

    /// <summary>当前平台或设备不支持该操作 / The operation is unsupported by the current platform or device.</summary>
    NotSupported = 801,

    /// <summary>stream capture 不支持该操作 / The operation is unsupported during stream capture.</summary>
    StreamCaptureUnsupported = 900,

    /// <summary>stream capture 已失效 / The stream capture has been invalidated.</summary>
    StreamCaptureInvalidated = 901,

    /// <summary>stream capture begin/end 不匹配 / Stream capture begin and end calls are unmatched.</summary>
    StreamCaptureUnmatched = 903,
}
