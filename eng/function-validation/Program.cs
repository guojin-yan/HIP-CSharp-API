using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using JYPPX.ROCm.HipSharp.Interop;
using JYPPX.ROCm.HipSharp.Types;

// Test-only, PackageReference-only consumer. Each fixture runs in a fresh process.
internal static unsafe partial class Program
{
    private static readonly HipRuntimeNativeApi Raw = new();
    private static readonly List<object> Calls = new();
    private static readonly List<string> Unsupported = new();
    private static void Require(bool value, string reason)
    {
        if (!value) throw new InvalidOperationException(reason);
    }

    // Native NotSupported is a recorded platform restriction, never successful function evidence.
    // A restriction is reported separately and never becomes positive function evidence.
    private static bool Capability(HipError result, [CallerArgumentExpression(nameof(result))] string expression = "")
    {
        if (result != HipError.NotSupported) { Check(result, expression: expression); return true; }
        var call = new { expression, result = (int)result, expected = (int)result, phase = "native-not-supported" };
        Calls.Add(call); Console.WriteLine("NATIVE_CALL=" + JsonSerializer.Serialize(call));
        Unsupported.Add(expression);
        Console.WriteLine("NATIVE_RESTRICTION=" + expression + ":NotSupported");
        return false;
    }

    private static void CheckText(IntPtr result, [CallerArgumentExpression(nameof(result))] string expression = "")
    {
        string? text = Marshal.PtrToStringUTF8(result);
        Require(!string.IsNullOrWhiteSpace(text), "Missing native text: " + expression);
        var call = new { expression, result = text, expected = text, phase = "positive", returnKind = "validated-text" };
        Calls.Add(call); Console.WriteLine("NATIVE_CALL=" + JsonSerializer.Serialize(call));
    }

    private static void CheckScalar(int result, int expected, [CallerArgumentExpression(nameof(result))] string expression = "")
    {
        Require(result == expected, expression + ": scalar result mismatch.");
        var call = new { expression, result, expected, phase = "positive", returnKind = "validated-scalar" };
        Calls.Add(call); Console.WriteLine("NATIVE_CALL=" + JsonSerializer.Serialize(call));
    }

    private static void Check(HipError result, HipError expected = HipError.Success,
        [CallerArgumentExpression(nameof(result))] string expression = "")
    {
        var call = new { expression, result = (int)result, expected = (int)expected,
            phase = expected == HipError.Success ? "positive" : "negative" };
        Calls.Add(call);
        Console.WriteLine("NATIVE_CALL=" + JsonSerializer.Serialize(call));
        Require(result == expected, expression + ": expected " + expected + ", actual " + result);
    }

    private static int Main(string[] args)
    {
        Require(args.Length == 1, "One fixture name is required.");
        string fixture = args[0];
        Console.WriteLine("FIXTURE_START=" + fixture);
        Check(Raw.Init(0));
        switch (fixture)
        {
            case "same-device-peer-copies": SameDevicePeerCopies(); break;
            case "driver-link-lifetime": DriverLinkLifetime(); break;
            case "registered-texture-border": LegacyTextureParameters(false, 0); break;
            case "registered-texture-mip-filter": LegacyTextureParameters(false, 1); break;
            case "registered-texture-mip-bias": LegacyTextureParameters(false, 2); break;
            case "registered-texture-mip-clamp": LegacyTextureParameters(false, 3); break;
            case "module-texture-border": LegacyTextureParameters(true, 0); break;
            case "module-texture-mip-filter": LegacyTextureParameters(true, 1); break;
            case "module-texture-mip-bias": LegacyTextureParameters(true, 2); break;
            case "module-texture-mip-clamp": LegacyTextureParameters(true, 3); break;
            case "d3d12-external-memory": D3D12ExternalMemory(); break;
            case "d3d12-external-semaphore": D3D12ExternalSemaphore(false); break;
            case "d3d12-external-graph": D3D12ExternalSemaphore(true); break;
            case "d3d12-managed-memory": D3D12ManagedMemory(); break;
            case "d3d12-managed-semaphore": D3D12ManagedSemaphore(); break;
            case "device": Device(); break;
            case "device-config": DeviceConfig(); break;
            case "context": Context(); break;
            case "context-flags": ContextFlags(); break;
            case "primary-context": PrimaryContext(); break;
            case "primary-context-flags": PrimaryContextFlags(); break;
            case "copy-aliases": CopyAliases(); break;
            case "memset-1d": Memset1D(); break;
            case "memset-2d8": Memset2D(1); break;
            case "memset-2d16": Memset2D(2); break;
            case "memset-2d32": Memset2D(4); break;
            case "memset-2d16-async": Memset2D(2, true); break;
            case "memset-2d32-async": Memset2D(4, true); break;
            case "host": Host(); break;
            case "pointer": Pointer(); break;
            case "stream-event": StreamEvent(); break;
            case "capture": Capture(); break;
            case "graph-topology": GraphTopology(); break;
            case "graph-events": GraphEvents(); break;
            case "graph-host": GraphHost(); break;
            case "graph-memory": GraphMemory(); break;
            case "graph-child": GraphChild(); break;
            case "rtc-code": RtcCode(); break;
            case "rtc-link": RtcLink(); break;
            case "rtc-negative": RtcNegative(); break;
            case "module": Module(); break;
            case "virtual-memory": VirtualMemory(); break;
            case "memory-pool": MemoryPool(); break;
            case "array-copy": ArrayCopy(); break;
            case "array-driver": ArrayDriver(); break;
            case "negative-parameters": NegativeParameters(); break;
            case "driver-copy": DriverCopy(); break;
            case "callbacks": Callbacks(); break;
            case "user-object": UserObject(); break;
            case "graph-update": GraphUpdate(); break;
            case "array-2d": Array2D(); break;
            case "mipmapped-array": MipmappedArray(); break;
            case "texture-queries": TextureQueries(); break;
            case "device-identity": DeviceIdentity(); break;
            case "library": Library(); break;
            case "driver-link": DriverLink(); break;
            case "graph-kernel": GraphKernel(); break;
            case "pitched-runtime": PitchedRuntime(); break;
            case "extension-stream": ExtensionStream(); break;
            case "graph-allocation": GraphAllocation(); break;
            case "generic-graph": GenericGraph(); break;
            case "proc-address": ProcAddress(); break;
            case "driver-texture": DriverTextureObject(); break;
            case "driver-mipmapped": DriverMipmapped(); break;
            case "host-runtime": HostRuntime(); break;
            case "host-attributes": HostAttributes(); break;
            case "host-symbol": HostSymbol(); break;
            case "runtime-graph-copy": RuntimeGraphCopy(); break;
            case "driver-graph-memset": DriverGraphMemset(); break;
            case "stream-values": StreamValues(); break;
            case "stream-batch": BatchMemory(false); break;
            case "graph-batch": BatchMemory(true); break;
            case "capture-to-graph": CaptureToGraph(); break;
            case "primary-lifetime": PrimaryLifetime(); break;
            case "device-reset": DeviceReset(); break;
            case "batch-copy": BatchCopies(false); break;
            case "batch-copy-3d": BatchCopies(true); break;
            case "registered-texture-parameters": RegisteredTextureParameters(); break;
            case "registered-texture-array": RegisteredTextureArray(); break;
            case "registered-texture-linear": RegisteredTextureLinear(false); break;
            case "registered-texture-pitched": RegisteredTextureLinear(true); break;
            case "negative-memory": NegativeMemory(); break;
            case "negative-streams": NegativeStreams(); break;
            case "negative-events": NegativeEvents(); break;
            case "negative-graphs": NegativeGraphs(); break;
            case "selection-diagnostics": SelectionAndDiagnostics(); break;
            case "profiler": Profiler(); break;
            case "module-global": ModuleGlobal(); break;
            case "module-texture-array": ModuleTexture(0); break;
            case "module-texture-linear": ModuleTexture(1); break;
            case "module-texture-pitched": ModuleTexture(2); break;
            case "module-texture-mipmapped": ModuleTexture(3); break;
            case "registered-texture-mipmapped": RegisteredTextureMipmapped(); break;
            case "legacy-device-properties": LegacyDeviceProperties(); break;
            case "peer-single-device": PeerSingleDevice(); break;
            case "stream-attach": StreamAttach(); break;
            case "profiler-stop": ProfilerStopOnly(); break;
            case "managed-basic": ManagedBasic(); break;
            case "driver-graph-allocation": DriverGraphAllocation(); break;
            case "texture-resource-view": TextureResourceView(); break;
            default: throw new ArgumentException("Unknown fixture: " + fixture);
        }
        Console.WriteLine("FUNCTION_FIXTURE=" + JsonSerializer.Serialize(new {
            fixture, passed = true, assertionsCompleted = true, unsupported = Unsupported, calls = Calls }));
        return 0;
    }

    private static void DeviceIdentity()
    {
        byte* uuid = stackalloc byte[16];
        new Span<byte>(uuid, 16).Clear(); Check(Raw.DeviceGetUuid((IntPtr)uuid, 0));
        Require(new ReadOnlySpan<byte>(uuid, 16).IndexOfAnyExcept((byte)0) >= 0, "Device UUID is all zero.");
        int units = 0; Check(Raw.DeviceGetAttribute((IntPtr)(&units), 63, 0)); Require(units > 0, "Compute unit count.");
        var desc = new HipChannelFormatDescriptor(32, 0, 0, 0, HipChannelFormatKind.FloatingPoint);
        UIntPtr width = 0; Check(Raw.DeviceGetTexture1DLinearMaxWidth((IntPtr)(&width), (IntPtr)(&desc), 0));
        Require(width > 0, "Texture linear width limit.");
        Check(Raw.ExtGetLastError());
    }

    private static void Device()
    {
        int count = 0, device = -1, ordinal = -1, major = -1, minor = -1, driver = 0, runtime = 0;
        Check(Raw.GetDeviceCount((IntPtr)(&count))); Require(count > 0, "No GPU.");
        Check(Raw.GetDevice((IntPtr)(&device)));
        Check(Raw.SetDevice(device));
        Check(Raw.DeviceGet((IntPtr)(&ordinal), device)); Require(ordinal == device, "Device ordinal mismatch.");
        Check(Raw.DeviceComputeCapability((IntPtr)(&major), (IntPtr)(&minor), device));
        Require(major > 0 && minor >= 0, "Invalid compute capability.");
        Check(Raw.DriverGetVersion((IntPtr)(&driver))); Check(Raw.RuntimeGetVersion((IntPtr)(&runtime)));
        Require(driver > 0 && runtime > 0, "Invalid version.");
        UIntPtr total = 0, free = 0, reportedTotal = 0;
        Check(Raw.DeviceTotalMem((IntPtr)(&total), device));
        Check(Raw.MemGetInfo((IntPtr)(&free), (IntPtr)(&reportedTotal)));
        Require(total > 0 && total == reportedTotal && free <= total, "Memory query mismatch.");
        byte* name = stackalloc byte[256];
        Check(Raw.DeviceGetName((IntPtr)name, 256, device));
        Require(!string.IsNullOrWhiteSpace(Marshal.PtrToStringAnsi((IntPtr)name)), "Empty device name.");
        byte* pci = stackalloc byte[64];
        Check(Raw.DeviceGetPCIBusId((IntPtr)pci, 64, device));
        ordinal = -1;
        Check(Raw.DeviceGetByPCIBusId((IntPtr)(&ordinal), (IntPtr)pci)); Require(ordinal == device, "PCI round trip.");
        CheckText(Raw.GetErrorName(HipError.InvalidValue));
        CheckText(Raw.GetErrorString(HipError.InvalidValue));
        IntPtr text = 0;
        Check(Raw.DrvGetErrorName(HipError.InvalidValue, (IntPtr)(&text)));
        Require(!string.IsNullOrEmpty(Marshal.PtrToStringAnsi(text)), "Driver name.");
        Check(Raw.DrvGetErrorString(HipError.InvalidValue, (IntPtr)(&text)));
        Require(!string.IsNullOrEmpty(Marshal.PtrToStringAnsi(text)), "Driver text.");
        Check(Raw.GetDeviceCount(IntPtr.Zero), HipError.InvalidValue);
        // Error state is queried, consumed, then checked again; no assumed last-error state.
        HipError peek = Raw.PeekAtLastError();
        Check(Raw.GetLastError(), peek); Check(Raw.PeekAtLastError()); Check(Raw.GetLastError());
        Check(Raw.DeviceSynchronize());
    }

    private static void NegativeParameters()
    {
        int value = -1; IntPtr pointer = 0;
        Check(Raw.Init(1), HipError.InvalidValue);
        Check(Raw.GetDeviceCount(IntPtr.Zero), HipError.InvalidValue);
        Check(Raw.GetDevice(IntPtr.Zero), HipError.InvalidValue);
        Check(Raw.SetDevice(-1), HipError.InvalidDevice);
        Check(Raw.DeviceGet((IntPtr)(&value), -1), HipError.InvalidDevice);
        Check(Raw.DeviceComputeCapability((IntPtr)(&value), (IntPtr)(&value), -1), HipError.InvalidDevice);
        Check(Raw.Malloc(IntPtr.Zero, 16), HipError.InvalidValue);
        Check(Raw.Malloc((IntPtr)(&pointer), 256));
        try { Check(Raw.MemcpyHtoD(pointer, IntPtr.Zero, 16), HipError.InvalidValue); }
        finally { Check(Raw.Free(pointer)); }
        Check(Raw.Free(IntPtr.Zero));
    }

    private static void DeviceConfig()
    {
        uint flags = 0; int cache = -1, shared = -1, least = 0, greatest = 0;
        Check(Raw.GetDeviceFlags((IntPtr)(&flags))); Check(Raw.SetDeviceFlags(flags));
        Check(Raw.DeviceGetCacheConfig((IntPtr)(&cache))); Check(Raw.DeviceSetCacheConfig(cache));
        Check(Raw.DeviceGetSharedMemConfig((IntPtr)(&shared))); Check(Raw.DeviceSetSharedMemConfig(shared));
        Check(Raw.DeviceGetStreamPriorityRange((IntPtr)(&least), (IntPtr)(&greatest)));
        Require(least >= greatest, "Priority range inverted.");
        UIntPtr limit = 0; Check(Raw.DeviceGetLimit((IntPtr)(&limit), 0));
        Require(limit > 0, "Stack limit is zero."); Check(Raw.DeviceSetLimit(0, limit));
        UIntPtr after = 0; Check(Raw.DeviceGetLimit((IntPtr)(&after), 0)); Require(after >= limit, "Limit round trip.");
    }

    private static void Context()
    {
        IntPtr context = 0, current = 0, popped = 0; int device = -1, cache = -1, shared = -1; uint version = 0;
        Check(Raw.CtxCreate((IntPtr)(&context), 0, 0)); Require(context != 0, "Null context.");
        try
        {
            Check(Raw.CtxGetCurrent((IntPtr)(&current))); Require(current == context, "Current context mismatch.");
            Check(Raw.CtxGetDevice((IntPtr)(&device))); Require(device == 0, "Context device mismatch.");
            if (Capability(Raw.CtxGetApiVersion(context, (IntPtr)(&version)))) Require(version > 0, "Context version.");
            if (Capability(Raw.CtxGetCacheConfig((IntPtr)(&cache)))) Require(cache >= 0 && cache <= 3, "Cache configuration.");
            Capability(Raw.CtxSetCacheConfig(cache < 0 ? 0 : cache));
            if (Capability(Raw.CtxGetSharedMemConfig((IntPtr)(&shared)))) Require(shared >= 0 && shared <= 2, "Shared configuration.");
            Capability(Raw.CtxSetSharedMemConfig(shared < 0 ? 0 : shared));
            Check(Raw.CtxPopCurrent((IntPtr)(&popped))); Require(popped == context, "Pop mismatch.");
            Check(Raw.CtxPushCurrent(context)); Check(Raw.CtxSetCurrent(context)); Capability(Raw.CtxSynchronize());
        }
        finally { Check(Raw.CtxDestroy(context)); }
    }

    private static void ContextFlags()
    {
        IntPtr context = 0; uint flags = uint.MaxValue;
        Check(Raw.CtxCreate((IntPtr)(&context), 0, 0));
        try { if (Capability(Raw.CtxGetFlags((IntPtr)(&flags)))) Require(flags == 0, "Context flags mismatch."); }
        finally { Check(Raw.CtxDestroy(context)); }
    }

    private static void PrimaryContext()
    {
        uint flags = 0; int active = -1; IntPtr context = 0;
        Check(Raw.DevicePrimaryCtxReset(0));
        Check(Raw.DevicePrimaryCtxGetState(0, (IntPtr)(&flags), (IntPtr)(&active)));
        Check(Raw.DevicePrimaryCtxRetain((IntPtr)(&context), 0)); Require(context != 0, "Null primary context.");
        try
        {
            Check(Raw.DevicePrimaryCtxGetState(0, (IntPtr)(&flags), (IntPtr)(&active)));
            Console.WriteLine("PRIMARY_CONTEXT_AFTER_RETAIN=active:" + active);
            Require(active == 1, "Primary not active after retain.");
        }
        finally { Check(Raw.DevicePrimaryCtxRelease(0)); Check(Raw.DevicePrimaryCtxReset(0)); Check(Raw.DeviceReset()); }
    }

    private static void PrimaryContextFlags()
    {
        uint flags = 0; int active = -1;
        Check(Raw.DevicePrimaryCtxReset(0));
        Check(Raw.DevicePrimaryCtxGetState(0, (IntPtr)(&flags), (IntPtr)(&active)));
        Require(active == 0 || active == 1, "Invalid primary context active state.");
        Console.WriteLine("PRIMARY_CONTEXT_STATE=active:" + active + ";flags:" + flags);
        Check(Raw.DevicePrimaryCtxSetFlags(0, flags), active == 1 ? (HipError)216 : HipError.Success);
    }

    private static void CopyAliases()
    {
        const int n = 4096;
        IntPtr first = 0, second = 0, host = 0, stream = 0;
        Check(Raw.Malloc((IntPtr)(&first), n));
        try
        {
            Check(Raw.Malloc((IntPtr)(&second), n));
            Check(Raw.HostMalloc((IntPtr)(&host), n, 0)); Check(Raw.StreamCreate((IntPtr)(&stream)));
            for (int i = 0; i < n; i++) ((byte*)host)[i] = (byte)(i * 37 + 11);
            Check(Raw.MemcpyHtoD(first, host, n)); Check(Raw.MemcpyDtoD(second, first, n));
            new Span<byte>((void*)host, n).Clear(); Check(Raw.MemcpyDtoH(host, second, n)); VerifyPattern(host, n);
            Check(Raw.MemcpyHtoDAsync(first, host, n, stream)); Check(Raw.MemcpyDtoDAsync(second, first, n, stream));
            Check(Raw.StreamSynchronize(stream)); new Span<byte>((void*)host, n).Clear();
            Check(Raw.MemcpyDtoHAsync(host, second, n, stream)); Check(Raw.StreamSynchronize(stream)); VerifyPattern(host, n);
            Check(Raw.MemcpyHtoD(first, IntPtr.Zero, 16), HipError.InvalidValue);
        }
        finally
        {
            if (stream != 0) Check(Raw.StreamDestroy(stream));
            if (host != 0) Check(Raw.HostFree(host));
            if (second != 0) Check(Raw.Free(second)); Check(Raw.Free(first));
        }
    }

    private static void VerifyPattern(IntPtr host, int n)
    {
        for (int i = 0; i < n; i++) Require(((byte*)host)[i] == (byte)(i * 37 + 11), "Copy mismatch at " + i);
    }

    private static void VerifyFill(IntPtr pointer, int elementSize, uint value, int pitch = 256, int width = 256, int height = 1)
    {
        byte* data = stackalloc byte[pitch * height];
        Check(Raw.MemcpyDtoH((IntPtr)data, pointer, (UIntPtr)(pitch * height)));
        for (int row = 0; row < height; row++)
            for (int column = 0; column < pitch; column++)
            {
                byte expected = column < width ? (byte)(value >> (8 * (column % elementSize))) : (byte)0xCD;
                Require(data[row * pitch + column] == expected, "Memset data/padding mismatch at row=" + row + ", byte=" + column
                    + ", expected=" + expected + ", actual=" + data[row * pitch + column]);
            }
    }

    private static void Memset1D()
    {
        IntPtr pointer = 0, stream = 0; Check(Raw.Malloc((IntPtr)(&pointer), 256));
        try
        {
            Check(Raw.StreamCreate((IntPtr)(&stream)));
            Check(Raw.MemsetD8(pointer, 0xAB, 256)); VerifyFill(pointer, 1, 0xAB);
            Check(Raw.MemsetD16(pointer, 0x1234, 128)); VerifyFill(pointer, 2, 0x1234);
            Check(Raw.MemsetD32(pointer, 0x12345678, 64)); VerifyFill(pointer, 4, 0x12345678);
            Check(Raw.MemsetD8Async(pointer, 0xEF, 256, stream)); Check(Raw.StreamSynchronize(stream)); VerifyFill(pointer, 1, 0xEF);
            Check(Raw.MemsetD16Async(pointer, 0x3456, 128, stream)); Check(Raw.StreamSynchronize(stream)); VerifyFill(pointer, 2, 0x3456);
            Check(Raw.MemsetD32Async(pointer, 0x34567812, 64, stream)); Check(Raw.StreamSynchronize(stream)); VerifyFill(pointer, 4, 0x34567812);
        }
        finally { if (stream != 0) Check(Raw.StreamDestroy(stream)); Check(Raw.Free(pointer)); }
    }

    private static void Memset2D(int elementSize, bool asyncOnly = false)
    {
        IntPtr pointer = 0, stream = 0; Check(Raw.Malloc((IntPtr)(&pointer), 1024));
        try
        {
            Check(Raw.StreamCreate((IntPtr)(&stream)));
            Check(Raw.Memset(pointer, 0xCD, 1024));
            if (elementSize == 1)
            {
                Check(Raw.MemsetD2D8(pointer, 256, 0xAB, 128, 4)); VerifyFill(pointer, 1, 0xAB, 256, 128, 4);
                Check(Raw.MemsetD2D8Async(pointer, 256, 0xEF, 128, 4, stream)); Check(Raw.StreamSynchronize(stream)); VerifyFill(pointer, 1, 0xEF, 256, 128, 4);
            }
            else if (elementSize == 2)
            {
                if (!asyncOnly) { Check(Raw.MemsetD2D16(pointer, 256, 0x1234, 64, 4)); VerifyFill(pointer, 2, 0x1234, 256, 128, 4); }
                Check(Raw.MemsetD2D16Async(pointer, 256, 0x3456, 64, 4, stream)); Check(Raw.StreamSynchronize(stream)); VerifyFill(pointer, 2, 0x3456, 256, 128, 4);
            }
            else
            {
                if (!asyncOnly) { Check(Raw.MemsetD2D32(pointer, 256, 0x12345678, 32, 4)); VerifyFill(pointer, 4, 0x12345678, 256, 128, 4); }
                Check(Raw.MemsetD2D32Async(pointer, 256, 0x34567812, 32, 4, stream)); Check(Raw.StreamSynchronize(stream)); VerifyFill(pointer, 4, 0x34567812, 256, 128, 4);
            }
        }
        finally { if (stream != 0) Check(Raw.StreamDestroy(stream)); Check(Raw.Free(pointer)); }
    }

    private static void Host()
    {
        IntPtr host = 0, mapped = 0; uint flags = 0;
        Check(Raw.HostAlloc((IntPtr)(&host), 4096, 2));
        try
        {
            Check(Raw.HostGetFlags((IntPtr)(&flags), host)); Require((flags & 2) != 0, "Mapped flag lost.");
            Check(Raw.HostGetDevicePointer((IntPtr)(&mapped), host, 0)); Require(mapped != 0, "Mapped pointer missing.");
            ((int*)host)[0] = 123456; int result = 0;
            Check(Raw.MemcpyDtoH((IntPtr)(&result), mapped, sizeof(int))); Require(result == 123456, "Mapped bytes mismatch.");
        }
        finally { Check(Raw.FreeHost(host)); }
        host = 0; Check(Raw.MallocHost((IntPtr)(&host), 4096)); Check(Raw.FreeHost(host));
        host = 0; Check(Raw.MemAllocHost((IntPtr)(&host), 4096)); Check(Raw.FreeHost(host));
        host = (IntPtr)NativeMemory.AlignedAlloc(4096, 4096);
        try
        {
            Check(Raw.HostRegister(host, 4096, 0));
            try
            {
                mapped = 0; Check(Raw.HostGetDevicePointer((IntPtr)(&mapped), host, 0));
                ((int*)host)[0] = 7654321; int result = 0;
                Check(Raw.MemcpyDtoH((IntPtr)(&result), mapped, sizeof(int))); Require(result == 7654321, "Registered bytes mismatch.");
            }
            finally { Check(Raw.HostUnregister(host)); }
        }
        finally { NativeMemory.AlignedFree((void*)host); }
    }

    private static void Pointer()
    {
        IntPtr pointer = 0; Check(Raw.Malloc((IntPtr)(&pointer), 4096));
        try
        {
            IntPtr start = 0; UIntPtr length = 0;
            Check(Raw.MemGetAddressRange((IntPtr)(&start), (IntPtr)(&length), pointer + 17));
            Require(start == pointer && length >= 4096, "Allocation range mismatch.");
            length = 0; Check(Raw.MemPtrGetInfo(pointer, (IntPtr)(&length))); Require(length >= 4096, "Pointer allocation size.");
            IntPtr devicePointer = 0;
            Check(Raw.PointerGetAttribute((IntPtr)(&devicePointer), 3, pointer)); Require(devicePointer == pointer, "Pointer attribute mismatch.");
            int ordinal = -1;
            Check(Raw.PointerGetAttribute((IntPtr)(&ordinal), 9, pointer)); Require(ordinal == 0, "Pointer device ordinal.");
            int* attributes = stackalloc int[2] { 3, 9 };
            IntPtr* outputs = stackalloc IntPtr[2] { (IntPtr)(&devicePointer), (IntPtr)(&ordinal) };
            devicePointer = 0; ordinal = -1;
            Check(Raw.DrvPointerGetAttributes(2, (IntPtr)attributes, (IntPtr)outputs, pointer));
            Require(devicePointer == pointer && ordinal == 0, "Multiple pointer attributes mismatch.");
            HipPointerAttributes details = default;
            Check(Raw.PointerGetAttributes((IntPtr)(&details), pointer));
            Require(details.DevicePointer == pointer && details.DeviceOrdinal == 0 && details.IsManaged == 0, "Pointer structure attributes.");
        }
        finally { Check(Raw.Free(pointer)); }
    }
}
