using System;
using JYPPX.HipSharp.Memory;

namespace JYPPX.HipSharp.Modules;

/// <summary>
/// 表示支持的 kernel 参数值 / Represents a supported kernel argument value.
/// </summary>
public sealed class HipKernelArgument
{
    private HipKernelArgument(HipKernelArgumentKind kind, IHipPointerOwner? pointerOwner, int int32Value)
    {
        Kind = kind;
        PointerOwner = pointerOwner;
        Int32Value = int32Value;
    }

    /// <summary>
    /// 创建设备指针参数 / Creates a device-pointer argument.
    /// </summary>
    /// <param name="memory">设备内存 / Device memory.</param>
    /// <returns>kernel 参数 / Kernel argument.</returns>
    /// <exception cref="ArgumentNullException">设备内存为 null / Device memory is <see langword="null"/>.</exception>
    public static HipKernelArgument DevicePointer(HipDeviceMemory memory) =>
        new(HipKernelArgumentKind.DevicePointer, memory ?? throw new ArgumentNullException(nameof(memory)), 0);

    /// <summary>创建 managed-memory 指针参数 / Creates a managed-memory pointer argument.</summary>
    public static HipKernelArgument DevicePointer(HipManagedMemory memory) =>
        new(HipKernelArgumentKind.DevicePointer, memory ?? throw new ArgumentNullException(nameof(memory)), 0);

    /// <summary>创建 stream-ordered 内存指针参数 / Creates a stream-ordered memory pointer argument.</summary>
    public static HipKernelArgument DevicePointer(HipAsyncDeviceMemory memory) =>
        new(HipKernelArgumentKind.DevicePointer, memory ?? throw new ArgumentNullException(nameof(memory)), 0);

    /// <summary>
    /// 创建 32 位有符号整数参数 / Creates a signed 32-bit integer argument.
    /// </summary>
    /// <param name="value">整数值 / Integer value.</param>
    /// <returns>kernel 参数 / Kernel argument.</returns>
    public static HipKernelArgument Scalar32(int value) => new(HipKernelArgumentKind.Scalar32, null, value);

    internal HipKernelArgumentKind Kind { get; }

    internal IHipPointerOwner? PointerOwner { get; }

    internal int Int32Value { get; }
}

internal enum HipKernelArgumentKind
{
    DevicePointer,
    Scalar32,
}
