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

    internal HipError MallocResult { get; set; } = HipError.Success;

    internal HipError ModuleLoadResult { get; set; } = HipError.Success;

    internal HipError ModuleUnloadResult { get; set; } = HipError.Success;

    internal HipError ModuleGetFunctionResult { get; set; } = HipError.Success;

    internal HipError ModuleLaunchResult { get; set; } = HipError.Success;

    internal HipError SynchronizeResult { get; set; } = HipError.Success;

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

    public HipError DeviceSynchronize()
    {
        SynchronizeCount++;
        return SynchronizeResult;
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
