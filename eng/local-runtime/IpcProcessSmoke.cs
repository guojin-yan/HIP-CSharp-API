using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using JYPPX.ROCm.HipSharp;
using JYPPX.ROCm.HipSharp.Interop;
using JYPPX.ROCm.HipSharp.Types;

internal static unsafe class Program
{
    private static void Check(HipError result, string api)
    {
        Console.WriteLine($"IPC_API api={api}; code={(int)result}; pid={Environment.ProcessId}");
        if (result != HipError.Success) throw new InvalidOperationException(api + ": " + result);
    }
    private static string Encode<T>(T value) where T : unmanaged => Convert.ToBase64String(new ReadOnlySpan<byte>(&value, sizeof(T)));
    private static T Decode<T>(string text) where T : unmanaged
    {
        byte[] bytes=Convert.FromBase64String(text);
        if(bytes.Length!=sizeof(T))throw new ArgumentException("IPC handle size mismatch.");
        return MemoryMarshal.Read<T>(bytes);
    }
    private static int Main(string[] args)
    {
        using var runtime=new HipRuntime(); runtime.Initialize();
        var raw=new HipRuntimeNativeApi();
        if(sizeof(HipIpcMemHandle)!=64 || sizeof(HipIpcEventHandle)!=64)throw new InvalidOperationException("IPC ABI size mismatch.");
        var data=new byte[4096];
        if(args.Length==3 && args[0]=="child")
        {
            HipIpcMemHandle mh=Decode<HipIpcMemHandle>(args[1]);
            HipIpcEventHandle eh=Decode<HipIpcEventHandle>(args[2]);
            IntPtr memory=IntPtr.Zero, ev=IntPtr.Zero;
            Check(raw.IpcOpenMemHandle((IntPtr)(&memory),mh,1),"hipIpcOpenMemHandle");
            try
            {
                Check(raw.IpcOpenEventHandle((IntPtr)(&ev),eh),"hipIpcOpenEventHandle");
                try { Check(raw.EventSynchronize(ev),"hipEventSynchronize(imported)"); }
                finally { Check(raw.EventDestroy(ev),"hipEventDestroy(imported)"); }
                fixed(byte* host=data)
                {
                    Check(raw.Memcpy((IntPtr)host,memory,(UIntPtr)data.Length,2),"hipMemcpy(imported-to-host)");
                    for(int i=0;i<data.Length;i++)if(data[i]!=(byte)(i*7+11))throw new InvalidOperationException("Imported bytes differ.");
                    for(int i=0;i<data.Length;i++)data[i]^=0xa5;
                    Check(raw.Memcpy(memory,(IntPtr)host,(UIntPtr)data.Length,1),"hipMemcpy(host-to-imported)");
                }
                Check(raw.DeviceSynchronize(),"hipDeviceSynchronize");
            }
            finally { Check(raw.IpcCloseMemHandle(memory),"hipIpcCloseMemHandle"); }
            Console.WriteLine("IPC_CHILD=passed; by-value-handles=64; byteComparison=4096");
            return 0;
        }
        using var owner=runtime.Allocate((ulong)data.Length);
        for(int i=0;i<data.Length;i++)data[i]=(byte)(i*7+11);
        owner.CopyFrom(data);
        HipIpcMemHandle memHandle=default;HipIpcEventHandle eventHandle=default;
        Check(raw.IpcGetMemHandle((IntPtr)(&memHandle),owner.DangerousGetHandle()),"hipIpcGetMemHandle");
        IntPtr sourceEvent=IntPtr.Zero;
        Check(raw.EventCreateWithFlags((IntPtr)(&sourceEvent),6),"hipEventCreateWithFlags(interprocess)");
        try
        {
            Check(raw.EventRecord(sourceEvent,IntPtr.Zero),"hipEventRecord");
            Check(raw.IpcGetEventHandle((IntPtr)(&eventHandle),sourceEvent),"hipIpcGetEventHandle");
            var info=new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};
            if(string.Equals(Path.GetFileNameWithoutExtension(Environment.ProcessPath),"dotnet",StringComparison.OrdinalIgnoreCase))
                info.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
            info.ArgumentList.Add("child");info.ArgumentList.Add(Encode(memHandle));info.ArgumentList.Add(Encode(eventHandle));
            using var child=Process.Start(info)!;
            var stdout=child.StandardOutput.ReadToEndAsync();var stderr=child.StandardError.ReadToEndAsync();
            if(!child.WaitForExit(90000)){child.Kill(true);throw new TimeoutException("IPC child timed out.");}
            Console.Write(stdout.GetAwaiter().GetResult());Console.Error.Write(stderr.GetAwaiter().GetResult());
            if(child.ExitCode!=0)throw new InvalidOperationException("IPC child failed: "+child.ExitCode);
            owner.CopyTo(data);
            for(int i=0;i<data.Length;i++)if(data[i]!=(byte)((byte)(i*7+11)^0xa5))throw new InvalidOperationException("Parent bytes differ after child close.");
        }
        finally { Check(raw.EventDestroy(sourceEvent),"hipEventDestroy(original)"); }
        Console.WriteLine("IPC_PARENT=passed; separateProcess=true; memory=true; event=true; parentOwnerAfterChildClose=true");
        return 0;
    }
}
