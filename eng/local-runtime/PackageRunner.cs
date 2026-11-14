using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;

// Test-only entry point linked alongside an existing sample in an external,
// PackageReference-only consumer. This file is never added to the Core package.
internal static class PackageRunner
{
    private static int Main(string[] args)
    {
        int result;
        try
        {
            MethodInfo entry = typeof(Program).GetMethod("Main", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("The sample has no Main method.");
            result = (int)(entry.Invoke(null, new object[] { args }) ?? 0);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            result = 1;
        }

        try
        {
            using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(Environment.GetEnvironmentVariable("HIPSHARP_LOCAL_RUNTIME_MANIFEST")!));
            var expected = new Dictionary<string, string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
            foreach (JsonElement item in manifest.RootElement.GetProperty("files").EnumerateArray())
                expected.Add(item.GetProperty("name").GetString()!, item.GetProperty("sha256").GetString()!);
            var modules = Process.GetCurrentProcess().Modules.Cast<ProcessModule>()
                .Select(module => Path.GetFullPath(module.FileName)).Distinct().ToArray();
            var packaged = PackageModuleAudit.Verify(AppContext.BaseDirectory, expected, modules);
            string[] leaked = modules.Where(path => path.Replace('\\', '/').Contains("/AMD/ROCm/", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/opt/rocm/", StringComparison.OrdinalIgnoreCase)).ToArray();
            if (leaked.Length != 0) throw new InvalidOperationException("Installed SDK library leakage: " + string.Join(", ", leaked));
            Console.WriteLine("PACKAGE_MODULE_AUDIT=" + JsonSerializer.Serialize(new { passed = true, modules = packaged }));
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("Package module audit failed: " + exception);
            result = 1;
        }
        return result;
    }
}
