using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
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
            string app = Path.GetFullPath(AppContext.BaseDirectory);
            string[] expected = manifest.RootElement.GetProperty("files").EnumerateArray()
                .Select(item => item.GetProperty("name").GetString()!).ToArray();
            var modules = Process.GetCurrentProcess().Modules.Cast<ProcessModule>()
                .Select(module => Path.GetFullPath(module.FileName)).Distinct().ToArray();
            string[] packaged = modules.Where(path => expected.Contains(Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)).ToArray();
            foreach (string path in packaged)
            {
                if (!path.StartsWith(app, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("A packaged library was loaded outside the consumer: " + path);
                string hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
                if (!manifest.RootElement.GetProperty("files").EnumerateArray().Any(item => item.GetProperty("sha256").GetString() == hash))
                    throw new InvalidOperationException("Loaded native library hash differs from the package: " + path);
            }
            string[] leaked = modules.Where(path => path.Replace('\\', '/').Contains("/AMD/ROCm/", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/opt/rocm/", StringComparison.OrdinalIgnoreCase)).ToArray();
            if (leaked.Length != 0) throw new InvalidOperationException("Installed SDK library leakage: " + string.Join(", ", leaked));
            if (packaged.Length == 0) throw new InvalidOperationException("No packaged native library was observed.");
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
