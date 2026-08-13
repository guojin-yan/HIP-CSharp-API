namespace JYPPX.ROCm.HipSharp.Rtc;

/// <summary>
/// 定义 HIPRTC 7.2.1 返回码 / Defines HIPRTC 7.2.1 result codes.
/// </summary>
public enum HipRtcResult
{
    /// <summary>操作成功 / The operation completed successfully.</summary>
    Success = 0,

    /// <summary>内存不足 / Memory allocation failed.</summary>
    OutOfMemory = 1,

    /// <summary>程序创建失败 / Program creation failed.</summary>
    ProgramCreationFailure = 2,

    /// <summary>输入无效 / The input is invalid.</summary>
    InvalidInput = 3,

    /// <summary>程序句柄无效 / The program handle is invalid.</summary>
    InvalidProgram = 4,

    /// <summary>编译选项无效 / A compiler option is invalid.</summary>
    InvalidOption = 5,

    /// <summary>程序编译失败 / Program compilation failed.</summary>
    Compilation = 6,

    /// <summary>内置头文件处理失败 / Built-in header processing failed.</summary>
    BuiltinOperationFailure = 7,

    /// <summary>编译后未添加名称表达式 / No name expressions were added before compilation.</summary>
    NoNameExpressionsAfterCompilation = 8,

    /// <summary>在编译前请求了降低后的名称 / A lowered name was requested before compilation.</summary>
    NoLoweredNamesBeforeCompilation = 9,

    /// <summary>名称表达式无效 / A name expression is invalid.</summary>
    NameExpressionNotValid = 10,

    /// <summary>发生内部错误 / An internal error occurred.</summary>
    InternalError = 11,

    /// <summary>链接阶段发生错误 / Linking failed.</summary>
    Linking = 100,
}
