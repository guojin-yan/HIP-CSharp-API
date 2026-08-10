using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using JYPPX.HipSharp.Interop;
using JYPPX.HipSharp.Memory;
using JYPPX.HipSharp.Streams;
using JYPPX.HipSharp.Types;

namespace JYPPX.HipSharp.Modules;

/// <summary>
/// 表示由 module 拥有的 HIP kernel function / Represents a HIP kernel function owned by a module.
/// </summary>
public sealed class HipKernel
{
    private readonly HipModule _module;
    private readonly IntPtr _function;

    internal HipKernel(HipModule module, IntPtr function, string name)
    {
        _module = module;
        _function = function;
        Name = name;
    }

    /// <summary>获取 kernel 名称 / Gets the kernel name.</summary>
    public string Name { get; }

    /// <summary>
    /// 在 default stream 上启动 kernel；调用方随后应显式同步 / Launches the kernel on the default stream; the caller should synchronize explicitly afterward.
    /// </summary>
    /// <param name="grid">网格尺寸 / Grid dimensions.</param>
    /// <param name="block">线程块尺寸 / Block dimensions.</param>
    /// <param name="arguments">按 kernel 签名顺序排列的参数 / Arguments ordered according to the kernel signature.</param>
    /// <param name="sharedMemoryBytes">每个线程块的动态共享内存字节数 / Dynamic shared-memory bytes per block.</param>
    /// <exception cref="ArgumentNullException">参数集合或元素为 null / The argument collection or an element is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">设备内存来自不同 Runtime / Device memory belongs to a different Runtime.</exception>
    /// <exception cref="ArgumentOutOfRangeException">维度乘法或参数数组大小溢出 / Dimension multiplication or argument-array size overflows.</exception>
    /// <exception cref="ObjectDisposedException">module 或设备内存已释放 / The module or device memory has been released.</exception>
    /// <exception cref="HipException">kernel 启动失败 / The kernel launch fails.</exception>
    /// <remarks>
    /// 启动期间不得在其他线程显式释放参数中的设备内存 / Device memory used as an argument must not be explicitly disposed concurrently with the launch.
    /// </remarks>
    public void Launch(
        HipLaunchDimensions grid,
        HipLaunchDimensions block,
        IReadOnlyList<HipKernelArgument> arguments,
        uint sharedMemoryBytes = 0)
    {
        if (arguments is null)
        {
            throw new ArgumentNullException(nameof(arguments));
        }

        ValidateDimensions(grid, nameof(grid));
        ValidateDimensions(block, nameof(block));
        if (arguments.Count > int.MaxValue / IntPtr.Size)
        {
            throw new ArgumentOutOfRangeException(nameof(arguments), "The kernel parameter array is too large for the current process.");
        }

        _module.Invoke(moduleHandle =>
        {
            LaunchCore(null, grid, block, arguments, sharedMemoryBytes);
            return moduleHandle;
        });
    }

    /// <summary>
    /// 在显式 stream 上启动 kernel；stream 完成前保留参数所有权 / Launches on an explicit stream and retains argument ownership until completion.
    /// </summary>
    /// <param name="stream">目标 stream / Target stream.</param>
    /// <param name="grid">网格尺寸 / Grid dimensions.</param>
    /// <param name="block">线程块尺寸 / Block dimensions.</param>
    /// <param name="arguments">kernel 参数 / Kernel arguments.</param>
    /// <param name="sharedMemoryBytes">动态共享内存字节数 / Dynamic shared-memory bytes.</param>
    /// <exception cref="ArgumentNullException">stream 或 arguments 为 null / stream or arguments is null.</exception>
    /// <exception cref="ArgumentException">stream 或内存来自其他 Runtime / stream or memory belongs to another Runtime.</exception>
    /// <exception cref="HipException">kernel 启动失败 / Kernel launch fails.</exception>
    public void Launch(
        HipStream stream,
        HipLaunchDimensions grid,
        HipLaunchDimensions block,
        IReadOnlyList<HipKernelArgument> arguments,
        uint sharedMemoryBytes = 0)
    {
        if (stream is null) throw new ArgumentNullException(nameof(stream));
        if (arguments is null) throw new ArgumentNullException(nameof(arguments));
        ValidateDimensions(grid, nameof(grid));
        ValidateDimensions(block, nameof(block));
        if (arguments.Count > int.MaxValue / IntPtr.Size) throw new ArgumentOutOfRangeException(nameof(arguments));
        if (!ReferenceEquals(_module.NativeApi, stream.NativeApi)) throw new ArgumentException("Kernel and stream belong to different HIP Runtime clients.", nameof(stream));
        _module.Invoke(moduleHandle =>
        {
            LaunchCore(stream, grid, block, arguments, sharedMemoryBytes);
            return moduleHandle;
        });
    }

    private void LaunchCore(
        HipStream? stream,
        HipLaunchDimensions grid,
        HipLaunchDimensions block,
        IReadOnlyList<HipKernelArgument> arguments,
        uint sharedMemoryBytes)
    {
        var valueStorage = new List<IntPtr>(arguments.Count);
        var acquiredMemory = new List<HipDeviceMemory>();
        IntPtr parameterArray = IntPtr.Zero;
        bool moduleReference = false;
        bool transferred = false;
        try
        {
            if (stream is not null)
            {
                _module.AcquireAsyncReference();
                moduleReference = true;
            }
            if (arguments.Count != 0)
            {
                parameterArray = Marshal.AllocHGlobal(checked(arguments.Count * IntPtr.Size));
            }

            for (int index = 0; index < arguments.Count; index++)
            {
                HipKernelArgument argument = arguments[index] ?? throw new ArgumentNullException(nameof(arguments), "Kernel arguments cannot contain null elements.");
                IntPtr valuePointer;
                if (argument.Kind == HipKernelArgumentKind.DevicePointer)
                {
                    HipDeviceMemory memory = argument.DeviceMemory!;
                    if (!ReferenceEquals(_module.NativeApi, memory.NativeApi))
                    {
                        throw new ArgumentException("Kernel device-memory arguments must belong to the same HIP Runtime client as the module.", nameof(arguments));
                    }

                    bool addedReference = false;
                    IntPtr devicePointer = memory.DangerousAcquireHandle(out addedReference);
                    if (!addedReference)
                    {
                        throw new ObjectDisposedException(nameof(HipDeviceMemory));
                    }

                    acquiredMemory.Add(memory);
                    if (devicePointer == IntPtr.Zero)
                    {
                        throw new ArgumentOutOfRangeException(nameof(arguments), "A device pointer cannot be null.");
                    }

                    valuePointer = Marshal.AllocHGlobal(IntPtr.Size);
                    Marshal.WriteIntPtr(valuePointer, devicePointer);
                }
                else
                {
                    valuePointer = Marshal.AllocHGlobal(sizeof(int));
                    Marshal.WriteInt32(valuePointer, argument.Int32Value);
                }

                valueStorage.Add(valuePointer);
                Marshal.WriteIntPtr(parameterArray, index * IntPtr.Size, valuePointer);
            }

            HipError error = _module.NativeApi.ModuleLaunchKernel(
                _function,
                grid.X,
                grid.Y,
                grid.Z,
                block.X,
                block.Y,
                block.Z,
                sharedMemoryBytes,
                stream?.DangerousGetHandle() ?? IntPtr.Zero,
                parameterArray);
            HipCall.ThrowIfFailed(_module.NativeApi, error, "hipModuleLaunchKernel");
            if (stream is not null)
            {
                var memorySnapshot = acquiredMemory.ToArray();
                stream.AddPendingLease(new HipAsyncLease(() =>
                {
                    for (int index = memorySnapshot.Length - 1; index >= 0; index--) memorySnapshot[index].DangerousReleaseHandle();
                    _module.ReleaseAsyncReference();
                }));
                transferred = true;
            }
        }
        finally
        {
            if (!transferred)
            {
                for (int index = acquiredMemory.Count - 1; index >= 0; index--)
                {
                    acquiredMemory[index].DangerousReleaseHandle();
                }
                if (moduleReference) _module.ReleaseAsyncReference();
            }

            foreach (IntPtr valuePointer in valueStorage)
            {
                Marshal.FreeHGlobal(valuePointer);
            }

            if (parameterArray != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(parameterArray);
            }

            GC.KeepAlive(_module);
        }
    }

    private static void ValidateDimensions(HipLaunchDimensions dimensions, string parameterName)
    {
        if (dimensions.X == 0 || dimensions.Y == 0 || dimensions.Z == 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Launch dimensions must be greater than zero.");
        }

        try
        {
            checked
            {
                _ = (ulong)dimensions.X * dimensions.Y * dimensions.Z;
            }
        }
        catch (OverflowException)
        {
            throw new ArgumentOutOfRangeException(parameterName, "The launch-dimension product exceeds UInt64.");
        }
    }
}
