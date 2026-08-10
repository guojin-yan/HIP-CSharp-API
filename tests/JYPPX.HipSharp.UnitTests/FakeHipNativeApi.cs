using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using JYPPX.HipSharp.Interop;
using JYPPX.HipSharp.Memory;
using JYPPX.HipSharp.Types;

namespace JYPPX.HipSharp.UnitTests;

internal sealed class FakeHipNativeApi : IHipNativeApi, IDisposable
{
    private readonly Dictionary<IntPtr, int> _allocations = new();
    private readonly HashSet<IntPtr> _streams = new();
    private readonly HashSet<IntPtr> _events = new();

    internal HipError MallocResult { get; set; } = HipError.Success;

    internal HipError ModuleLoadResult { get; set; } = HipError.Success;

    internal HipError ModuleUnloadResult { get; set; } = HipError.Success;

    internal HipError ModuleGetFunctionResult { get; set; } = HipError.Success;

    internal HipError ModuleLaunchResult { get; set; } = HipError.Success;

    internal HipError SynchronizeResult { get; set; } = HipError.Success;

    internal HipError StreamSynchronizeResult { get; set; } = HipError.Success;

    internal HipError StreamQueryResult { get; set; } = HipError.Success;

    internal HipError EventSynchronizeResult { get; set; } = HipError.Success;

    internal HipError EventQueryResult { get; set; } = HipError.Success;

    internal IList<bool> ExpectedKernelPointerArguments { get; } = new List<bool>();

    internal IList<long> LastKernelArgumentValues { get; } = new List<long>();

    internal byte[] LastModuleCodeObject { get; private set; } = Array.Empty<byte>();

    internal string LastKernelName { get; private set; } = string.Empty;

    internal uint LastInitFlags { get; private set; }

    internal int LastSetDevice { get; private set; }

    internal int FreeCount { get; private set; }

    internal int SynchronizeCount { get; private set; }

    internal int ModuleUnloadCount { get; private set; }

    internal int ModuleLaunchCount { get; private set; }

    internal int AsyncCopyCount { get; private set; }

    internal int StreamDestroyCount { get; private set; }

    internal int EventDestroyCount { get; private set; }

    public HipError Init(uint flags)
    {
        LastInitFlags = flags;
        return HipError.Success;
    }

    public HipError RuntimeGetVersion(out int runtimeVersion)
    {
        runtimeVersion = 70200001;
        return HipError.Success;
    }

    public HipError DriverGetVersion(out int driverVersion)
    {
        driverVersion = 70200000;
        return HipError.Success;
    }

    public HipError GetDeviceCount(out int count)
    {
        count = 2;
        return HipError.Success;
    }

    public HipError GetDevice(out int deviceId)
    {
        deviceId = LastSetDevice;
        return HipError.Success;
    }

    public HipError SetDevice(int deviceId)
    {
        if (deviceId < 0 || deviceId >= 2)
        {
            return HipError.InvalidDevice;
        }

        LastSetDevice = deviceId;
        return HipError.Success;
    }

    public HipError DeviceGetName(int deviceId, out string name)
    {
        name = deviceId switch
        {
            0 => "Fake Radeon 0",
            1 => "Fake Radeon 1",
            _ => string.Empty,
        };
        return deviceId >= 0 && deviceId < 2 ? HipError.Success : HipError.InvalidDevice;
    }

    public HipError DeviceGetAttribute(out int value, HipDeviceAttribute attribute, int deviceId)
    {
        value = attribute == HipDeviceAttribute.MaxThreadsPerBlock ? 1024 : 0;
        return deviceId >= 0 && deviceId < 2 ? HipError.Success : HipError.InvalidDevice;
    }

    public HipError Malloc(out IntPtr pointer, UIntPtr byteCount)
    {
        if (MallocResult != HipError.Success)
        {
            pointer = IntPtr.Zero;
            return MallocResult;
        }

        ulong size = byteCount.ToUInt64();
        if (size > int.MaxValue)
        {
            pointer = IntPtr.Zero;
            return HipError.OutOfMemory;
        }

        pointer = Marshal.AllocHGlobal((int)size);
        _allocations.Add(pointer, (int)size);
        return HipError.Success;
    }

    public HipError Free(IntPtr pointer)
    {
        if (!_allocations.Remove(pointer))
        {
            return HipError.InvalidValue;
        }

        Marshal.FreeHGlobal(pointer);
        FreeCount++;
        return HipError.Success;
    }

    public HipError Memcpy(IntPtr destination, IntPtr source, UIntPtr byteCount, HipMemoryCopyKind kind)
    {
        int count = checked((int)byteCount.ToUInt64());
        var buffer = new byte[count];
        Marshal.Copy(source, buffer, 0, count);
        Marshal.Copy(buffer, 0, destination, count);
        return HipError.Success;
    }

    public HipError MemcpyAsync(IntPtr destination, IntPtr source, UIntPtr byteCount, HipMemoryCopyKind kind, IntPtr stream)
    {
        AsyncCopyCount++;
        return Memcpy(destination, source, byteCount, kind);
    }

    public HipError HostMalloc(out IntPtr pointer, UIntPtr byteCount, uint flags) => Malloc(out pointer, byteCount);

    public HipError HostFree(IntPtr pointer) => Free(pointer);

    public HipError DeviceSynchronize()
    {
        SynchronizeCount++;
        return SynchronizeResult;
    }

    public HipError StreamCreateWithFlags(out IntPtr stream, uint flags)
    {
        stream = new IntPtr(0x5000 + _streams.Count + 1);
        _streams.Add(stream);
        return HipError.Success;
    }

    public HipError StreamDestroy(IntPtr stream)
    {
        if (!_streams.Remove(stream)) return HipError.InvalidValue;
        StreamDestroyCount++;
        return HipError.Success;
    }

    public HipError StreamSynchronize(IntPtr stream) => _streams.Contains(stream) ? StreamSynchronizeResult : HipError.InvalidValue;

    public HipError StreamQuery(IntPtr stream) => _streams.Contains(stream) ? StreamQueryResult : HipError.InvalidValue;

    public HipError EventCreateWithFlags(out IntPtr eventHandle, uint flags)
    {
        eventHandle = new IntPtr(0x6000 + _events.Count + 1);
        _events.Add(eventHandle);
        return HipError.Success;
    }

    public HipError EventDestroy(IntPtr eventHandle)
    {
        if (!_events.Remove(eventHandle)) return HipError.InvalidValue;
        EventDestroyCount++;
        return HipError.Success;
    }

    public HipError EventRecord(IntPtr eventHandle, IntPtr stream) => _events.Contains(eventHandle) && _streams.Contains(stream) ? HipError.Success : HipError.InvalidValue;

    public HipError EventSynchronize(IntPtr eventHandle) => _events.Contains(eventHandle) ? EventSynchronizeResult : HipError.InvalidValue;

    public HipError EventQuery(IntPtr eventHandle) => _events.Contains(eventHandle) ? EventQueryResult : HipError.InvalidValue;

    public HipError EventElapsedTime(out float milliseconds, IntPtr start, IntPtr end)
    {
        milliseconds = 1.25f;
        return _events.Contains(start) && _events.Contains(end) ? HipError.Success : HipError.InvalidValue;
    }

    public HipError ModuleLoadData(byte[] codeObject, out IntPtr module)
    {
        LastModuleCodeObject = (byte[])codeObject.Clone();
        module = ModuleLoadResult == HipError.Success ? new IntPtr(0x2000) : IntPtr.Zero;
        return ModuleLoadResult;
    }

    public HipError ModuleUnload(IntPtr module)
    {
        if (ModuleUnloadResult == HipError.Success)
        {
            ModuleUnloadCount++;
        }

        return ModuleUnloadResult;
    }

    public HipError ModuleGetFunction(IntPtr module, string kernelName, out IntPtr function)
    {
        LastKernelName = kernelName;
        function = ModuleGetFunctionResult == HipError.Success ? new IntPtr(0x3000) : IntPtr.Zero;
        return ModuleGetFunctionResult;
    }

    public HipError ModuleLaunchKernel(
        IntPtr function,
        uint gridX,
        uint gridY,
        uint gridZ,
        uint blockX,
        uint blockY,
        uint blockZ,
        uint sharedMemoryBytes,
        IntPtr stream,
        IntPtr kernelParameters)
    {
        ModuleLaunchCount++;
        LastKernelArgumentValues.Clear();
        for (int index = 0; index < ExpectedKernelPointerArguments.Count; index++)
        {
            IntPtr valueStorage = Marshal.ReadIntPtr(kernelParameters, index * IntPtr.Size);
            long value = ExpectedKernelPointerArguments[index]
                ? Marshal.ReadIntPtr(valueStorage).ToInt64()
                : Marshal.ReadInt32(valueStorage);
            LastKernelArgumentValues.Add(value);
        }

        return ModuleLaunchResult;
    }

    public string GetErrorName(HipError error) => error == HipError.OutOfMemory ? "hipErrorOutOfMemory" : "hipErrorUnknown";

    public string GetErrorString(HipError error) => error == HipError.OutOfMemory ? "out of memory" : "unknown HIP error";

    public void Dispose()
    {
        foreach (IntPtr pointer in _allocations.Keys)
        {
            Marshal.FreeHGlobal(pointer);
        }

        _allocations.Clear();
    }
}
