using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

if (args.Length == 0)
{
    Console.Error.WriteLine("Usage: generate [--check] | extract-headers --header-root <path> [--check] | probe-manifest");
    return 2;
}

string repositoryRoot = FindRepositoryRoot();
string manifestPath = Path.Combine(repositoryRoot, "eng", "interop", "interop-manifest.json");
string normalizedPath = Path.Combine(repositoryRoot, "eng", "interop", "normalized-model.json");
string generatedPath = Path.Combine(repositoryRoot, "src", "JYPPX.ROCm.HipSharp", "Generated", "HipNativeMethods.g.cs");
string completeModelPath = Path.Combine(repositoryRoot, "eng", "interop", "complete-api-model.json");
string completeRuntimePath = Path.Combine(repositoryRoot, "src", "JYPPX.ROCm.HipSharp", "Generated", "HipRuntimeNativeApi.g.cs");
string completeRtcPath = Path.Combine(repositoryRoot, "src", "JYPPX.ROCm.HipSharp", "Generated", "HipRtcNativeApi.g.cs");
string command = args[0].ToLowerInvariant();
bool check = args.Any(value => value is "--check" or "check");

if (command is "extract-headers" or "extract")
{
    string? headerRoot = GetOption(args, "--header-root");
    if (string.IsNullOrWhiteSpace(headerRoot))
    {
        Console.Error.WriteLine("extract-headers requires --header-root <path>.");
        return 2;
    }

    CompleteApiModel extracted = CompleteApiGenerator.Extract(headerRoot, "rocm-7.2.1");
    string extractedJson = CompleteApiGenerator.Serialize(extracted);
    if (check)
    {
        if (!File.Exists(completeModelPath) ||
            !JsonEquivalent(File.ReadAllText(completeModelPath), extractedJson))
        {
            Console.Error.WriteLine("Complete API model drift detected against the supplied official headers.");
            return 1;
        }

        if (!GeneratedFileMatches(completeRuntimePath, CompleteApiGenerator.RenderRuntime(extracted)) ||
            !GeneratedFileMatches(completeRtcPath, CompleteApiGenerator.RenderRtc(extracted)))
        {
            Console.Error.WriteLine("Complete API generated source drift detected.");
            return 1;
        }

        Console.WriteLine($"Official header coverage passed: Runtime={extracted.RuntimeFunctions.Count}, HIPRTC={extracted.RtcFunctions.Count}.");
        return 0;
    }

    File.WriteAllText(completeModelPath, extractedJson, new UTF8Encoding(false));
    WriteCompleteGeneratedFiles(extracted, completeRuntimePath, completeRtcPath);
    Console.WriteLine($"Complete API model written: Runtime={extracted.RuntimeFunctions.Count}, HIPRTC={extracted.RtcFunctions.Count}.");
    return 0;
}

if (!File.Exists(completeModelPath))
{
    Console.Error.WriteLine("Complete API model is missing. Run extract-headers with an explicit official header root.");
    return 1;
}

CompleteApiModel completeModel = CompleteApiGenerator.Deserialize(File.ReadAllText(completeModelPath));
using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
string candidateNormalized = Canonicalize(manifest.RootElement) + "\n";
string normalized = candidateNormalized;
bool normalizedDrift = false;
if (File.Exists(normalizedPath))
{
    string stored = File.ReadAllText(normalizedPath).Replace("\r\n", "\n");
    using JsonDocument storedDocument = JsonDocument.Parse(stored);
    if (!JsonElement.DeepEquals(storedDocument.RootElement, manifest.RootElement))
    {
        normalizedDrift = true;
    }
    else
    {
        normalized = stored;
    }
}
string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));
if (command is "probe-manifest" or "probe")
{
    JsonElement root = manifest.RootElement;
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        schemaVersion = root.GetProperty("schemaVersion").GetInt32(),
        generatorVersion = root.GetProperty("generatorVersion").GetString(),
        normalizedManifestSha256 = hash,
        functionCount = root.GetProperty("functions").GetArrayLength(),
        completeRuntimeFunctionCount = completeModel.RuntimeFunctions.Count,
        completeRtcFunctionCount = completeModel.RtcFunctions.Count,
        libraries = root.GetProperty("libraries").EnumerateArray().Select(value => value.GetString()).ToArray(),
    }, new JsonSerializerOptions { WriteIndented = true }));
    return 0;
}

if (command != "generate")
{
    Console.Error.WriteLine("Unknown command: " + args[0]);
    return 2;
}

if (check)
{
    if (normalizedDrift || !File.Exists(normalizedPath) || File.ReadAllText(normalizedPath).Replace("\r\n", "\n") != normalized)
    {
        Console.Error.WriteLine("Normalized model drift detected.");
        return 1;
    }
    string generated = File.ReadAllText(generatedPath).Replace("\r\n", "\n");
    string expectedGenerated = RenderDeclarations(manifest.RootElement, hash);
    if (!string.Equals(generated, expectedGenerated, StringComparison.Ordinal))
    {
        Console.Error.WriteLine("Generated interop source drift detected.");
        return 1;
    }
    if (!GeneratedFileMatches(completeRuntimePath, CompleteApiGenerator.RenderRuntime(completeModel)) ||
        !GeneratedFileMatches(completeRtcPath, CompleteApiGenerator.RenderRtc(completeModel)))
    {
        Console.Error.WriteLine("Complete API generated source drift detected.");
        return 1;
    }
    Console.WriteLine("Binding generator check passed: " + hash);
    return 0;
}

File.WriteAllText(normalizedPath, candidateNormalized, new UTF8Encoding(false));
File.WriteAllText(generatedPath, RenderDeclarations(manifest.RootElement, hash), new UTF8Encoding(false));
WriteCompleteGeneratedFiles(completeModel, completeRuntimePath, completeRtcPath);
Console.WriteLine("Normalized model written: " + hash);
return 0;

static string? GetOption(string[] arguments, string option)
{
    int index = Array.FindIndex(arguments, value => string.Equals(value, option, StringComparison.OrdinalIgnoreCase));
    return index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] : null;
}

static bool JsonEquivalent(string first, string second)
{
    using JsonDocument firstDocument = JsonDocument.Parse(first);
    using JsonDocument secondDocument = JsonDocument.Parse(second);
    return JsonElement.DeepEquals(firstDocument.RootElement, secondDocument.RootElement);
}

static bool GeneratedFileMatches(string path, string expected) =>
    File.Exists(path) && string.Equals(File.ReadAllText(path).Replace("\r\n", "\n"), expected, StringComparison.Ordinal);

static void WriteCompleteGeneratedFiles(CompleteApiModel model, string runtimePath, string rtcPath)
{
    File.WriteAllText(runtimePath, CompleteApiGenerator.RenderRuntime(model), new UTF8Encoding(false));
    File.WriteAllText(rtcPath, CompleteApiGenerator.RenderRtc(model), new UTF8Encoding(false));
}

static string RenderDeclarations(JsonElement root, string hash)
{
    var builder = new StringBuilder();
    builder.AppendLine("// <auto-generated />");
    builder.AppendLine("// Source: eng/interop/interop-manifest.json");
    builder.AppendLine("// Generator version: " + root.GetProperty("generatorVersion").GetString());
    builder.AppendLine("// Normalized manifest SHA-256: " + hash);
    builder.AppendLine();
    builder.AppendLine("using System;");
    builder.AppendLine("using System.Runtime.CompilerServices;");
    builder.AppendLine("using System.Runtime.InteropServices;");
    builder.AppendLine("using JYPPX.ROCm.HipSharp.Graphs;");
    builder.AppendLine("using JYPPX.ROCm.HipSharp.Memory;");
    builder.AppendLine("using JYPPX.ROCm.HipSharp.Rtc;");
    builder.AppendLine("using JYPPX.ROCm.HipSharp.Types;");
    builder.AppendLine();
    builder.AppendLine("namespace JYPPX.ROCm.HipSharp.Generated;");
    builder.AppendLine();
    builder.AppendLine("/// <summary>");
    builder.AppendLine("/// 提供由规范化 manifest 生成的 HIP C ABI 声明 / Provides HIP C ABI declarations generated from the normalized manifest.");
    builder.AppendLine("/// </summary>");
    builder.AppendLine("internal static partial class HipNativeMethods");
    builder.AppendLine("{");

    foreach (JsonElement function in root.GetProperty("functions").EnumerateArray())
    {
        string library = function.GetProperty("library").GetString()!;
        string importName = library == "amdhip64" ? "RuntimeImportName" : "RtcImportName";
        string managedName = function.GetProperty("managedName").GetString()!;
        string entryPoint = function.GetProperty("entryPoint").GetString()!;
        string returnType = function.GetProperty("returnType").GetString()!;
        string parameters = string.Join(", ", function.GetProperty("parameters").EnumerateArray().Select(parameter =>
            parameter.GetProperty("declaration").GetString()));

        builder.AppendLine("    /// <summary>");
        builder.AppendLine("    /// " + function.GetProperty("summaryZh").GetString() + " / " + function.GetProperty("summaryEn").GetString() + ".");
        builder.AppendLine("    /// </summary>");
        foreach (JsonElement parameter in function.GetProperty("parameters").EnumerateArray())
        {
            builder.AppendLine("    /// <param name=\"" + parameter.GetProperty("name").GetString() + "\">方向：" +
                parameter.GetProperty("direction").GetString() + "，所有权：" + parameter.GetProperty("ownership").GetString() +
                " / Direction: " + parameter.GetProperty("direction").GetString() + "; ownership: " +
                parameter.GetProperty("ownership").GetString() + ".</param>");
        }
        builder.AppendLine("    /// <returns>原生返回值 / Native return value.</returns>");
        builder.AppendLine("#if NET7_0_OR_GREATER");
        builder.AppendLine("    [LibraryImport(HipNativeLibraryNames." + importName + ", EntryPoint = \"" + entryPoint + "\")]");
        builder.AppendLine("    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]");
        builder.AppendLine("    internal static partial " + returnType + " " + managedName + "(" + parameters + ");");
        builder.AppendLine("#else");
        builder.AppendLine("    [DllImport(HipNativeLibraryNames." + importName + ", EntryPoint = \"" + entryPoint + "\", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]");
        builder.AppendLine("    internal static extern " + returnType + " " + managedName + "(" + parameters + ");");
        builder.AppendLine("#endif");
        builder.AppendLine();
    }

    builder.AppendLine("}");
    return builder.ToString().Replace("\r\n", "\n");
}

static string FindRepositoryRoot()
{
    DirectoryInfo? current = new(AppContext.BaseDirectory);
    while (current is not null && !File.Exists(Path.Combine(current.FullName, "HipSharp.sln")))
    {
        current = current.Parent;
    }
    return current?.FullName ?? throw new InvalidOperationException("Could not locate HipSharp repository root.");
}

static string Canonicalize(JsonElement element)
{
    using var stream = new MemoryStream();
    using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
    {
        WriteCanonical(writer, element);
    }
    return Encoding.UTF8.GetString(stream.ToArray());
}

static void WriteCanonical(Utf8JsonWriter writer, JsonElement element)
{
    switch (element.ValueKind)
    {
        case JsonValueKind.Object:
            writer.WriteStartObject();
            foreach (JsonProperty property in element.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal))
            {
                writer.WritePropertyName(property.Name);
                WriteCanonical(writer, property.Value);
            }
            writer.WriteEndObject();
            break;
        case JsonValueKind.Array:
            writer.WriteStartArray();
            foreach (JsonElement item in element.EnumerateArray())
            {
                WriteCanonical(writer, item);
            }
            writer.WriteEndArray();
            break;
        default:
            element.WriteTo(writer);
            break;
    }
}
