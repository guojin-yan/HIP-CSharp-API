using System;
using JYPPX.ROCm.HipSharp;
using JYPPX.ROCm.HipSharp.Types;

try
{
    var runtime = new HipRuntime();
    runtime.Initialize();
    HipRuntimeVersionInfo versions = runtime.GetVersionInfo();
    Console.WriteLine("HIP Runtime: " + versions.RuntimeVersion);
    Console.WriteLine("HIP Driver:  " + versions.DriverVersion);

    foreach (HipDevice device in runtime.GetDevices())
    {
        Console.WriteLine(device);
    }

    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception);
    return 1;
}
