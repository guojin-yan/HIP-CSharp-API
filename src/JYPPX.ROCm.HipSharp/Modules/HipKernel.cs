using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using JYPPX.ROCm.HipSharp.Interop;
using JYPPX.ROCm.HipSharp.Memory;
using JYPPX.ROCm.HipSharp.Streams;
using JYPPX.ROCm.HipSharp.Types;

namespace JYPPX.ROCm.HipSharp.Modules;

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
    /// 获取此 module kernel 的可移植资源属性 / Gets portable resource attributes for this module kernel.
    /// </summary>
    /// <returns>不可变 kernel 属性 / Immutable kernel attributes.</returns>
    /// <exception cref="InvalidOperationException">module 设备不是当前设备，或 HIP 返回非法属性 / The module device is not current, or HIP returns an invalid attribute.</exception>
    /// <exception cref="ObjectDisposedException">module 已释放 / The module has been released.</exception>
    /// <exception cref="HipException">属性查询失败，包括入口不可用 / The attribute query fails, including an unavailable entry point.</exception>
    public HipKernelAttributes GetAttributes() => _module.Invoke(moduleHandle =>
    {
        EnsureModuleDeviceIsCurrent();
        return new HipKernelAttributes(
            ReadPositiveFunctionAttribute(HipFunctionAttributeNative.MaxThreadsPerBlock),
            ReadByteFunctionAttribute(HipFunctionAttributeNative.SharedSizeBytes),
            ReadByteFunctionAttribute(HipFunctionAttributeNative.ConstantSizeBytes),
            ReadByteFunctionAttribute(HipFunctionAttributeNative.LocalSizeBytes),
            ReadNonNegativeFunctionAttribute(HipFunctionAttributeNative.NumberOfRegisters),
            ReadNonNegativeFunctionAttribute(HipFunctionAttributeNative.BinaryVersion),
            ReadByteFunctionAttribute(HipFunctionAttributeNative.MaxDynamicSharedSizeBytes));
    });

    /// <summary>
    /// 查询给定 block 配置的 occupancy 常驻估算 / Queries the occupancy residency estimate for a block configuration.
    /// </summary>
    /// <param name="blockSize">每个 block 的线程数 / Threads per block.</param>
    /// <param name="dynamicSharedMemoryBytes">每个 block 的动态共享内存字节数 / Dynamic shared-memory bytes per block.</param>
    /// <param name="flags">typed occupancy flags / Typed occupancy flags.</param>
    /// <returns>occupancy 常驻估算；它不是性能承诺 / Occupancy residency estimate; it is not a performance promise.</returns>
    /// <exception cref="ArgumentOutOfRangeException">block size、字节数或 flags 无效 / The block size, byte count, or flags are invalid.</exception>
    /// <exception cref="InvalidOperationException">module 设备不是当前设备，或 HIP 返回非法结果 / The module device is not current, or HIP returns an invalid result.</exception>
    /// <exception cref="HipException">occupancy 查询失败，包括入口不可用 / The occupancy query fails, including an unavailable entry point.</exception>
    public HipOccupancyInfo GetOccupancy(
        int blockSize,
        ulong dynamicSharedMemoryBytes = 0,
        HipOccupancyFlags flags = HipOccupancyFlags.Default)
    {
        if (blockSize <= 0) throw new ArgumentOutOfRangeException(nameof(blockSize));
        ValidateOccupancyFlags(flags);
        UIntPtr dynamicBytes = ToUIntPtr(dynamicSharedMemoryBytes, nameof(dynamicSharedMemoryBytes));
        return _module.Invoke(moduleHandle =>
        {
            EnsureModuleDeviceIsCurrent();
            return GetOccupancyCore(blockSize, dynamicSharedMemoryBytes, dynamicBytes, flags);
        });
    }

    /// <summary>
    /// 查询 HIP 建议的 occupancy launch plan / Queries a HIP-suggested occupancy launch plan.
    /// </summary>
    /// <param name="dynamicSharedMemoryBytes">每个 block 的动态共享内存字节数 / Dynamic shared-memory bytes per block.</param>
    /// <param name="blockSizeLimit">线程数上限；0 表示使用 kernel 最大值 / Thread limit; zero means use the kernel maximum.</param>
    /// <param name="flags">typed occupancy flags / Typed occupancy flags.</param>
    /// <returns>建议的最小 grid block 数、block 线程数和常驻估算 / Suggested minimum grid blocks, block threads, and residency estimate.</returns>
    /// <exception cref="ArgumentOutOfRangeException">字节数、limit 或 flags 无效 / The byte count, limit, or flags are invalid.</exception>
    /// <exception cref="InvalidOperationException">module 设备不是当前设备，或 HIP 返回非法结果 / The module device is not current, or HIP returns an invalid result.</exception>
    /// <exception cref="HipException">occupancy 查询失败，包括入口不可用 / The occupancy query fails, including an unavailable entry point.</exception>
    public HipOccupancyPlan GetOccupancyPlan(
        ulong dynamicSharedMemoryBytes = 0,
        int blockSizeLimit = 0,
        HipOccupancyFlags flags = HipOccupancyFlags.Default)
    {
        if (blockSizeLimit < 0) throw new ArgumentOutOfRangeException(nameof(blockSizeLimit));
        ValidateOccupancyFlags(flags);
        UIntPtr dynamicBytes = ToUIntPtr(dynamicSharedMemoryBytes, nameof(dynamicSharedMemoryBytes));
        return _module.Invoke(moduleHandle =>
        {
            EnsureModuleDeviceIsCurrent();
            HipError error;
            int minimumGridSize;
            int blockSize;
            if (flags == HipOccupancyFlags.Default)
            {
                error = _module.NativeApi.ModuleOccupancyMaxPotentialBlockSize(
                    out minimumGridSize, out blockSize, _function, dynamicBytes, blockSizeLimit);
            }
            else
            {
                error = _module.NativeApi.ModuleOccupancyMaxPotentialBlockSizeWithFlags(
                    out minimumGridSize, out blockSize, _function, dynamicBytes, blockSizeLimit, (uint)flags);
            }
            HipCall.ThrowIfFailed(_module.NativeApi, error,
                flags == HipOccupancyFlags.Default
                    ? "hipModuleOccupancyMaxPotentialBlockSize"
                    : "hipModuleOccupancyMaxPotentialBlockSizeWithFlags");
            if (minimumGridSize <= 0 || blockSize <= 0)
            {
                throw new InvalidOperationException("HIP returned a non-positive occupancy launch plan.");
            }

            return new HipOccupancyPlan(
                minimumGridSize,
                GetOccupancyCore(blockSize, dynamicSharedMemoryBytes, dynamicBytes, flags));
        });
    }

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
        ValidateLaunchArguments(grid, block, arguments);

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
        ValidateLaunchArguments(grid, block, arguments);
        if (!ReferenceEquals(_module.NativeApi, stream.NativeApi)) throw new ArgumentException("Kernel and stream belong to different HIP Runtime clients.", nameof(stream));
        _module.Invoke(moduleHandle =>
        {
            LaunchCore(stream, grid, block, arguments, sharedMemoryBytes);
            return moduleHandle;
        });
    }

    /// <summary>
    /// 在 default stream 上提交单设备 cooperative kernel / Submits a single-device cooperative kernel on the default stream.
    /// </summary>
    /// <param name="grid">grid 尺寸（block 数） / Grid dimensions, in blocks.</param>
    /// <param name="block">block 尺寸（线程数） / Block dimensions, in threads.</param>
    /// <param name="arguments">按 kernel 签名顺序排列的参数 / Arguments ordered according to the kernel signature.</param>
    /// <param name="sharedMemoryBytes">每个 block 的动态共享内存字节数 / Dynamic shared-memory bytes per block.</param>
    /// <exception cref="InvalidOperationException">设备不支持 cooperative launch、设备不匹配或 grid 超过常驻容量 / The device lacks cooperative launch support, the device is not current, or the grid exceeds resident capacity.</exception>
    /// <exception cref="HipException">capability、occupancy 或 launch 调用失败 / A capability, occupancy, or launch call fails.</exception>
    /// <remarks>没有普通 launch fallback；调用方应在释放 owner 前同步 default stream / There is no ordinary-launch fallback; synchronize the default stream before releasing owners.</remarks>
    public void LaunchCooperative(
        HipLaunchDimensions grid,
        HipLaunchDimensions block,
        IReadOnlyList<HipKernelArgument> arguments,
        uint sharedMemoryBytes = 0)
    {
        ValidateLaunchArguments(grid, block, arguments);
        _module.Invoke(moduleHandle =>
        {
            LaunchCore(null, grid, block, arguments, sharedMemoryBytes, cooperative: true);
            return moduleHandle;
        });
    }

    /// <summary>
    /// 在显式 stream 上提交单设备 cooperative kernel，并保留所有 owner 到完成 / Submits a single-device cooperative kernel on an explicit stream and retains all owners until completion.
    /// </summary>
    /// <param name="stream">与 module 同 Runtime、同设备的目标 stream / Target stream on the same Runtime and device as the module.</param>
    /// <param name="grid">grid 尺寸（block 数） / Grid dimensions, in blocks.</param>
    /// <param name="block">block 尺寸（线程数） / Block dimensions, in threads.</param>
    /// <param name="arguments">按 kernel 签名顺序排列的参数 / Arguments ordered according to the kernel signature.</param>
    /// <param name="sharedMemoryBytes">每个 block 的动态共享内存字节数 / Dynamic shared-memory bytes per block.</param>
    /// <exception cref="ArgumentNullException">stream 或 arguments 为 null / The stream or arguments are null.</exception>
    /// <exception cref="ArgumentException">stream、module 或参数 owner 不兼容 / The stream, module, or argument owners are incompatible.</exception>
    /// <exception cref="InvalidOperationException">设备不支持 cooperative launch、设备不匹配或 grid 超过常驻容量 / The device lacks cooperative launch support, the device is not current, or the grid exceeds resident capacity.</exception>
    /// <exception cref="HipException">capability、occupancy 或 launch 调用失败 / A capability, occupancy, or launch call fails.</exception>
    public void LaunchCooperative(
        HipStream stream,
        HipLaunchDimensions grid,
        HipLaunchDimensions block,
        IReadOnlyList<HipKernelArgument> arguments,
        uint sharedMemoryBytes = 0)
    {
        if (stream is null) throw new ArgumentNullException(nameof(stream));
        ValidateLaunchArguments(grid, block, arguments);
        if (!ReferenceEquals(_module.NativeApi, stream.NativeApi)) throw new ArgumentException("Kernel and stream belong to different HIP Runtime clients.", nameof(stream));
        if (_module.DeviceOrdinal != stream.DeviceOrdinal) throw new ArgumentException("Kernel and stream belong to different HIP devices.", nameof(stream));
        _module.Invoke(moduleHandle =>
        {
            LaunchCore(stream, grid, block, arguments, sharedMemoryBytes, cooperative: true);
            return moduleHandle;
        });
    }

    private void LaunchCore(
        HipStream? stream,
        HipLaunchDimensions grid,
        HipLaunchDimensions block,
        IReadOnlyList<HipKernelArgument> arguments,
        uint sharedMemoryBytes,
        bool cooperative = false)
    {
        var valueStorage = new List<IntPtr>(arguments.Count);
        var acquiredMemory = new List<IHipPointerOwner>();
        IntPtr parameterArray = IntPtr.Zero;
        bool moduleReference = false;
        bool transferred = false;
        IntPtr streamHandle = IntPtr.Zero;
        try
        {
            if (stream is not null)
            {
                _module.AcquireAsyncReference();
                moduleReference = true;
                streamHandle = stream.DangerousGetHandle();
            }
            if (arguments.Count != 0)
            {
                parameterArray = Marshal.AllocHGlobal(checked(arguments.Count * IntPtr.Size));
            }

            for (int index = 0; index < arguments.Count; index++)
            {
                HipKernelArgument argument = arguments[index] ?? throw new ArgumentNullException(nameof(arguments), "Kernel arguments cannot contain null elements.");
                IntPtr valuePointer;
                if (argument.Kind == HipKernelArgumentKind.GraphMemoryPointer)
                {
                    throw new ArgumentException("Graph-local memory arguments can only be used by explicit graph kernel nodes.", nameof(arguments));
                }
                if (argument.Kind == HipKernelArgumentKind.DevicePointer)
                {
                    IHipPointerOwner memory = argument.PointerOwner!;
                    if (!ReferenceEquals(_module.NativeApi, memory.NativeApi))
                    {
                        throw new ArgumentException("Kernel device-memory arguments must belong to the same HIP Runtime client as the module.", nameof(arguments));
                    }
                    if (cooperative && memory.DeviceOrdinal.HasValue && memory.DeviceOrdinal.Value != _module.DeviceOrdinal)
                    {
                        throw new ArgumentException("Cooperative kernel device-memory arguments must belong to the module device.", nameof(arguments));
                    }
                    if (memory.RequiredStream is not null && !ReferenceEquals(stream, memory.RequiredStream))
                    {
                        throw new ArgumentException("Stream-ordered memory arguments must be launched on their allocation stream.", nameof(arguments));
                    }

                    bool addedReference = false;
                    IntPtr devicePointer = memory.AcquirePointer(out addedReference);
                    if (!addedReference)
                    {
                        throw new ObjectDisposedException(nameof(HipKernelArgument));
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

            if (cooperative)
            {
                ValidateCooperativeLaunch(grid, block, sharedMemoryBytes);
            }
            HipError error = cooperative
                ? _module.NativeApi.ModuleLaunchCooperativeKernel(
                    _function, grid.X, grid.Y, grid.Z, block.X, block.Y, block.Z,
                    sharedMemoryBytes, streamHandle, parameterArray)
                : _module.NativeApi.ModuleLaunchKernel(
                    _function, grid.X, grid.Y, grid.Z, block.X, block.Y, block.Z,
                    sharedMemoryBytes, streamHandle, parameterArray);
            HipCall.ThrowIfFailed(_module.NativeApi, error,
                cooperative ? "hipModuleLaunchCooperativeKernel" : "hipModuleLaunchKernel");
            if (stream is not null)
            {
                stream.AddPendingLease(new HipAsyncLease(() =>
                {
                    while (acquiredMemory.Count != 0)
                    {
                        int releaseIndex = acquiredMemory.Count - 1;
                        acquiredMemory[releaseIndex].ReleasePointer();
                        acquiredMemory.RemoveAt(releaseIndex);
                    }
                    if (moduleReference)
                    {
                        _module.ReleaseAsyncReference();
                        moduleReference = false;
                    }
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
                    acquiredMemory[index].ReleasePointer();
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

    private static void ValidateLaunchArguments(
        HipLaunchDimensions grid,
        HipLaunchDimensions block,
        IReadOnlyList<HipKernelArgument> arguments)
    {
        if (arguments is null) throw new ArgumentNullException(nameof(arguments));
        ValidateDimensions(grid, nameof(grid));
        ValidateDimensions(block, nameof(block));
        if (arguments.Count > int.MaxValue / IntPtr.Size)
        {
            throw new ArgumentOutOfRangeException(nameof(arguments), "The kernel parameter array is too large for the current process.");
        }
    }

    private void ValidateCooperativeLaunch(
        HipLaunchDimensions grid,
        HipLaunchDimensions block,
        uint dynamicSharedMemoryBytes)
    {
        EnsureModuleDeviceIsCurrent();
        int capability = GetDeviceAttribute(HipDeviceAttribute.CooperativeLaunch);
        if (capability != 0 && capability != 1)
        {
            throw new InvalidOperationException("HIP returned an invalid cooperative-launch capability value.");
        }
        if (capability == 0)
        {
            throw new InvalidOperationException("The module device does not support cooperative kernel launch.");
        }

        ulong blockThreads = GetDimensionProduct(block);
        if (blockThreads > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(block), "The block thread count exceeds Int32.");
        }
        HipOccupancyInfo occupancy = GetOccupancyCore(
            (int)blockThreads,
            dynamicSharedMemoryBytes,
            new UIntPtr(dynamicSharedMemoryBytes),
            HipOccupancyFlags.Default);
        ulong gridBlocks = GetDimensionProduct(grid);
        if (gridBlocks > (ulong)occupancy.MaximumResidentBlocks)
        {
            throw new InvalidOperationException("The cooperative grid exceeds the device's estimated resident-block capacity.");
        }
    }

    private HipOccupancyInfo GetOccupancyCore(
        int blockSize,
        ulong dynamicSharedMemoryBytes,
        UIntPtr dynamicBytes,
        HipOccupancyFlags flags)
    {
        HipError error;
        int activeBlocks;
        if (flags == HipOccupancyFlags.Default)
        {
            error = _module.NativeApi.ModuleOccupancyMaxActiveBlocksPerMultiprocessor(
                out activeBlocks, _function, blockSize, dynamicBytes);
        }
        else
        {
            error = _module.NativeApi.ModuleOccupancyMaxActiveBlocksPerMultiprocessorWithFlags(
                out activeBlocks, _function, blockSize, dynamicBytes, (uint)flags);
        }
        HipCall.ThrowIfFailed(_module.NativeApi, error,
            flags == HipOccupancyFlags.Default
                ? "hipModuleOccupancyMaxActiveBlocksPerMultiprocessor"
                : "hipModuleOccupancyMaxActiveBlocksPerMultiprocessorWithFlags");
        if (activeBlocks <= 0)
        {
            throw new InvalidOperationException("HIP returned a non-positive active-block count.");
        }

        int multiprocessorCount = GetDeviceAttribute(HipDeviceAttribute.MultiprocessorCount);
        if (multiprocessorCount <= 0)
        {
            throw new InvalidOperationException("HIP returned a non-positive multiprocessor count.");
        }
        return new HipOccupancyInfo(blockSize, dynamicSharedMemoryBytes, activeBlocks, multiprocessorCount);
    }

    private int ReadPositiveFunctionAttribute(HipFunctionAttributeNative attribute)
    {
        int value = ReadNonNegativeFunctionAttribute(attribute);
        if (value == 0) throw new InvalidOperationException("HIP returned a zero function attribute for " + attribute + ".");
        return value;
    }

    private ulong ReadByteFunctionAttribute(HipFunctionAttributeNative attribute)
    {
        int value = ReadFunctionAttribute(attribute);
        // AMD ROCm reports constMemSize - 1, so an unused constant region is encoded as -1.
        if (attribute == HipFunctionAttributeNative.ConstantSizeBytes && value == -1)
        {
            return 0;
        }
        if (value < 0) throw new InvalidOperationException("HIP returned a negative function attribute for " + attribute + ".");
        return (ulong)value;
    }

    private int ReadNonNegativeFunctionAttribute(HipFunctionAttributeNative attribute)
    {
        int value = ReadFunctionAttribute(attribute);
        if (value < 0) throw new InvalidOperationException("HIP returned a negative function attribute for " + attribute + ".");
        return value;
    }

    private int ReadFunctionAttribute(HipFunctionAttributeNative attribute)
    {
        HipCall.ThrowIfFailed(_module.NativeApi,
            _module.NativeApi.FuncGetAttribute(out int value, attribute, _function),
            "hipFuncGetAttribute");
        return value;
    }

    private void EnsureModuleDeviceIsCurrent()
    {
        HipCall.ThrowIfFailed(_module.NativeApi, _module.NativeApi.GetDevice(out int currentDevice), "hipGetDevice");
        if (currentDevice != _module.DeviceOrdinal)
        {
            throw new InvalidOperationException("The device on which the module was loaded must be current for this operation.");
        }
    }

    private int GetDeviceAttribute(HipDeviceAttribute attribute)
    {
        HipCall.ThrowIfFailed(_module.NativeApi,
            _module.NativeApi.DeviceGetAttribute(out int value, attribute, _module.DeviceOrdinal),
            "hipDeviceGetAttribute");
        return value;
    }

    private static void ValidateOccupancyFlags(HipOccupancyFlags flags)
    {
        if (flags != HipOccupancyFlags.Default && flags != HipOccupancyFlags.DisableCachingOverride)
        {
            throw new ArgumentOutOfRangeException(nameof(flags));
        }
    }

    private static UIntPtr ToUIntPtr(ulong value, string parameterName)
    {
        if (UIntPtr.Size == sizeof(uint) && value > uint.MaxValue)
        {
            throw new ArgumentOutOfRangeException(parameterName, "The byte count exceeds native size_t.");
        }
        return new UIntPtr(value);
    }

    private static ulong GetDimensionProduct(HipLaunchDimensions dimensions) =>
        checked((ulong)dimensions.X * dimensions.Y * dimensions.Z);

    internal HipModule Module => _module;

    internal IntPtr Function => _function;

    internal static void ValidateGraphDimensions(HipLaunchDimensions dimensions, string parameterName) => ValidateDimensions(dimensions, parameterName);
}
