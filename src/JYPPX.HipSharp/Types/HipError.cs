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
}
