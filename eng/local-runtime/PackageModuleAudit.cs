#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

// Shared by the net10 package runner and net48 smoke; never shipped in Core.
internal static class PackageModuleAudit
{
    internal static List<string> Verify(string applicationDirectory, IDictionary<string, string> expected,
        IEnumerable<string> loadedModules)
    {
        bool windows = Path.DirectorySeparatorChar == '\\';
        StringComparison comparison = windows ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        StringComparer comparer = windows ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        string app = Path.GetFullPath(applicationDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var byName = new Dictionary<string, string>(comparer);
        foreach (KeyValuePair<string, string> item in expected)
        {
            string? value = item.Value;
            if (string.IsNullOrWhiteSpace(item.Key) || Path.GetFileName(item.Key) != item.Key ||
                value == null || !Regex.IsMatch(value, "^[0-9a-fA-F]{64}$"))
                throw new InvalidOperationException("Invalid native module manifest entry: " + item.Key);
            byName.Add(item.Key, value);
        }
        var observed = new List<string>();
        var seen = new HashSet<string>(comparer);
        foreach (string module in loadedModules)
        {
            string path = Path.GetFullPath(module);
            string name = Path.GetFileName(path);
            if (!byName.TryGetValue(name, out string? hash))
            {
                // Catch another version of a HIP closure library even if its name
                // is absent from the package allowlist. OS/driver prerequisites
                // (including WSL DXCore) are not package libraries.
                if (IsHipClosureLibrary(name))
                    throw new InvalidOperationException("Unlisted HIP closure module: " + path);
                continue;
            }
            if (!path.StartsWith(app, comparison))
                throw new InvalidOperationException("Native library outside application: " + path);
            for (string? current = path; current != null && current.StartsWith(app, comparison);
                 current = Path.GetDirectoryName(current))
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidOperationException("Native library uses a link outside the audited file layout: " + current);
            }
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
#if NET5_0_OR_GREATER
                string actual = Convert.ToHexString(sha.ComputeHash(stream));
#else
                string actual = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
#endif
                if (!string.Equals(actual, hash, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Native library filename/hash mismatch: " + name);
            }
            if (seen.Add(path)) observed.Add(path);
        }
        if (observed.Count == 0) throw new InvalidOperationException("No packaged native library was observed.");
        return observed;
    }

    private static bool IsHipClosureLibrary(string name) => Regex.IsMatch(name,
        @"^(?:lib)?(?:amdhip|hiprtc|amd_comgr|hsa-runtime|rocdxg|rocm_kpack|rocprofiler-register|rocm_sysdeps)[\w.\-]*\.(?:dll|so(?:\.[\w.\-]+)?)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
}
