using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

internal static class CompleteApiGenerator
{
    internal const int ExpectedRuntimeFunctionCount = 459;
    internal const int ExpectedRtcFunctionCount = 18;

    private static readonly Regex RuntimeFunctionRegex = new(
        @"(?ms)^(?<return>hipError_t|const\s+char\s*\*|int)\s+(?<name>hip[A-Z][A-Za-z0-9_]*)\s*\((?<parameters>.*?)\)\s*;",
        RegexOptions.CultureInvariant);

    private static readonly Regex RtcFunctionRegex = new(
        @"(?ms)^(?<return>hiprtcResult|const\s+char\s*\*)\s+(?<name>hiprtc[A-Za-z0-9_]*)\s*\((?<parameters>.*?)\)\s*;",
        RegexOptions.CultureInvariant);

    private static readonly HashSet<string> PointerAliasTypes = new(StringComparer.Ordinal)
    {
        "hipArray_const_t",
        "hipArray_t",
        "hipCtx_t",
        "hipDeviceptr_t",
        "hipEvent_t",
        "hipExternalMemory_t",
        "hipExternalSemaphore_t",
        "hipFunction_t",
        "hipGraph_t",
        "hipGraphExec_t",
        "hipGraphicsResource_t",
        "hipGraphNode_t",
        "hipHostFn_t",
        "hipKernel_t",
        "hipLibrary_t",
        "hipLinkState_t",
        "hipMemGenericAllocationHandle_t",
        "hipMemPool_t",
        "hipMipmappedArray_const_t",
        "hipMipmappedArray_t",
        "hipModule_t",
        "hipStreamCallback_t",
        "hipStream_t",
        "hipUserObject_t",
        "hiprtcLinkState",
        "hiprtcProgram",
    };

    private static readonly HashSet<string> CSharpKeywords = new(StringComparer.Ordinal)
    {
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
        "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
        "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for", "foreach",
        "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock", "long", "namespace",
        "new", "null", "object", "operator", "out", "override", "params", "private", "protected", "public",
        "readonly", "ref", "return", "sbyte", "sealed", "short", "sizeof", "stackalloc", "static", "string",
        "struct", "switch", "this", "throw", "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe",
        "ushort", "using", "virtual", "void", "volatile", "while",
    };

    private static readonly char[] ParameterNameSeparators = { '_' };

    internal static CompleteApiModel Extract(string headerRoot, string rocmTag)
    {
        string fullRoot = Path.GetFullPath(headerRoot);
        string runtimePath = ResolveHeader(fullRoot, "hip/hip_runtime_api.h");
        string rtcPath = ResolveHeader(fullRoot, "hip/hiprtc.h");
        List<NativeFunction> runtimeFunctions = ParseFunctions(runtimePath, "amdhip64", RuntimeFunctionRegex, "hip");
        List<NativeFunction> rtcFunctions = ParseFunctions(rtcPath, "hiprtc", RtcFunctionRegex, "hiprtc");

        if (runtimeFunctions.Count != ExpectedRuntimeFunctionCount)
        {
            throw new InvalidDataException(
                $"Expected {ExpectedRuntimeFunctionCount} public HIP Runtime C functions, but extracted {runtimeFunctions.Count}.");
        }

        if (rtcFunctions.Count != ExpectedRtcFunctionCount)
        {
            throw new InvalidDataException(
                $"Expected {ExpectedRtcFunctionCount} public HIPRTC C functions, but extracted {rtcFunctions.Count}.");
        }

        return new CompleteApiModel(
            1,
            rocmTag,
            new[]
            {
                CreateHeader("hip/hip_runtime_api.h", "amdhip64", runtimePath),
                CreateHeader("hip/hiprtc.h", "hiprtc", rtcPath),
            },
            runtimeFunctions,
            rtcFunctions);
    }

    internal static string Serialize(CompleteApiModel model) =>
        JsonSerializer.Serialize(model, JsonOptions()).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";

    internal static CompleteApiModel Deserialize(string json) =>
        JsonSerializer.Deserialize<CompleteApiModel>(json, JsonOptions())
        ?? throw new InvalidDataException("The complete API model is empty.");

    internal static string RenderRuntime(CompleteApiModel model) =>
        Render(model, model.RuntimeFunctions, "HipRuntimeNativeApi", "RuntimeImportName", "HIP Runtime");

    internal static string RenderRtc(CompleteApiModel model) =>
        Render(model, model.RtcFunctions, "HipRtcNativeApi", "RtcImportName", "HIPRTC");

    private static JsonSerializerOptions JsonOptions() => new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private static CompleteApiHeader CreateHeader(string path, string library, string physicalPath) =>
        new(path, library, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(physicalPath))));

    private static string ResolveHeader(string root, string relativePath)
    {
        string path = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        string rootWithSeparator = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Header path escapes the supplied header root: " + relativePath);
        }

        return File.Exists(path) ? path : throw new FileNotFoundException("Official HIP header is missing.", path);
    }

    private static List<NativeFunction> ParseFunctions(
        string path,
        string library,
        Regex functionRegex,
        string managedPrefix)
    {
        string source = File.ReadAllText(path);
        source = Regex.Replace(source, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline | RegexOptions.CultureInvariant);
        source = Regex.Replace(source, @"//.*$", string.Empty, RegexOptions.Multiline | RegexOptions.CultureInvariant);

        var candidates = new List<(string ReturnType, string EntryPoint, string Parameters)>();
        foreach (Match match in functionRegex.Matches(source))
        {
            string parameters = CollapseWhitespace(match.Groups["parameters"].Value);
            if (parameters.Contains('{') || parameters.Contains('}') || parameters.Contains("const T&", StringComparison.Ordinal))
            {
                continue;
            }

            candidates.Add((
                CollapseWhitespace(match.Groups["return"].Value),
                match.Groups["name"].Value,
                parameters));
        }

        var functions = new List<NativeFunction>();
        foreach (IGrouping<string, (string ReturnType, string EntryPoint, string Parameters)> group in
                 candidates.GroupBy(candidate => candidate.EntryPoint, StringComparer.Ordinal))
        {
            (string nativeReturnType, string entryPoint, string parameterText) = group
                .OrderBy(candidate => candidate.Parameters.Contains('=') ? 1 : 0)
                .ThenBy(candidate => candidate.Parameters.Contains('&') ? 1 : 0)
                .ThenBy(candidate => candidate.Parameters.Length)
                .First();

            List<NativeParameter> parameters = ParseParameters(parameterText);
            string managedName = ToManagedMethodName(entryPoint.Substring(managedPrefix.Length));
            functions.Add(new NativeFunction(
                managedName,
                entryPoint,
                library,
                nativeReturnType,
                MapReturnType(nativeReturnType),
                parameters));
        }

        return functions.OrderBy(function => function.EntryPoint, StringComparer.Ordinal).ToList();
    }

    private static List<NativeParameter> ParseParameters(string parameterText)
    {
        if (string.IsNullOrWhiteSpace(parameterText) || parameterText == "void")
        {
            return new List<NativeParameter>();
        }

        var result = new List<NativeParameter>();
        foreach (string originalParameter in SplitParameters(parameterText))
        {
            string declaration = Regex.Replace(
                originalParameter,
                @"\s*__dparm\s*\([^)]*\)",
                string.Empty,
                RegexOptions.CultureInvariant).Trim();
            declaration = Regex.Replace(declaration, @"\s*=\s*.*$", string.Empty, RegexOptions.CultureInvariant).Trim();
            Match match = Regex.Match(
                declaration,
                @"^(?<type>.+?)(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*(?<array>\[[^\]]*\])?$",
                RegexOptions.CultureInvariant);
            if (!match.Success)
            {
                throw new InvalidDataException("Could not parse native parameter declaration: " + declaration);
            }

            string nativeName = match.Groups["name"].Value;
            string nativeType = CollapseWhitespace(match.Groups["type"].Value.Trim());
            if (match.Groups["array"].Success)
            {
                nativeType += match.Groups["array"].Value;
            }

            string managedName = ToManagedParameterName(nativeName);
            result.Add(new NativeParameter(
                managedName,
                nativeName,
                nativeType,
                MapParameterType(nativeType)));
        }

        return result;
    }

    private static IEnumerable<string> SplitParameters(string value)
    {
        int start = 0;
        int depth = 0;
        for (int index = 0; index < value.Length; index++)
        {
            switch (value[index])
            {
                case '(':
                case '[':
                case '{':
                    depth++;
                    break;
                case ')':
                case ']':
                case '}':
                    depth--;
                    break;
                case ',' when depth == 0:
                    yield return value.Substring(start, index - start).Trim();
                    start = index + 1;
                    break;
            }
        }

        yield return value.Substring(start).Trim();
    }

    private static string MapReturnType(string nativeType) => nativeType switch
    {
        "hipError_t" => "HipError",
        "hiprtcResult" => "HipRtcResult",
        "const char*" => "IntPtr",
        "int" => "int",
        _ => throw new InvalidDataException("Unsupported native return type: " + nativeType),
    };

    private static string MapParameterType(string nativeType)
    {
        if (nativeType.Contains('*') || nativeType.Contains('&') || nativeType.Contains('['))
        {
            return "IntPtr";
        }

        string type = nativeType;
        type = Regex.Replace(type, @"\b(const|volatile|struct|enum)\b", string.Empty, RegexOptions.CultureInvariant);
        type = CollapseWhitespace(type);

        if (PointerAliasTypes.Contains(type))
        {
            return "IntPtr";
        }

        return type switch
        {
            "char" or "signed char" or "int8_t" => "sbyte",
            "unsigned char" or "uint8_t" => "byte",
            "short" or "short int" or "int16_t" => "short",
            "unsigned short" or "unsigned short int" or "uint16_t" => "ushort",
            "int" or "signed" or "signed int" or "int32_t" => "int",
            "unsigned" or "unsigned int" or "uint32_t" => "uint",
            "long long" or "long long int" or "int64_t" => "long",
            "unsigned long long" or "unsigned long long int" or "uint64_t" => "ulong",
            "size_t" => "UIntPtr",
            "float" => "float",
            "double" => "double",
            "hipDevice_t" => "int",
            "hipError_t" => "HipError",
            "hiprtcResult" => "HipRtcResult",
            "dim3" => "HipDim3",
            "hipExtent" => "HipExtent",
            "hipPitchedPtr" => "HipPitchedPtr",
            "hipMemLocation" => "HipMemLocation",
            "hipIpcMemHandle_t" => "HipIpcMemHandle",
            "hipIpcEventHandle_t" => "HipIpcEventHandle",
            "hipTextureObject_t" or "hipSurfaceObject_t" => "ulong",
            _ when type.StartsWith("hip", StringComparison.Ordinal) => "int",
            _ => throw new InvalidDataException("Unsupported native parameter type: " + nativeType),
        };
    }

    private static string ToManagedParameterName(string nativeName)
    {
        string[] parts = nativeName.Split(ParameterNameSeparators, StringSplitOptions.RemoveEmptyEntries);
        string name = parts.Length == 0
            ? nativeName
            : parts[0] + string.Concat(parts.Skip(1).Select(part => char.ToUpperInvariant(part[0]) + part.Substring(1)));
        return CSharpKeywords.Contains(name) ? "@" + name : name;
    }

    private static string ToManagedMethodName(string nativeName)
    {
        string[] parts = nativeName.Split(ParameterNameSeparators, StringSplitOptions.RemoveEmptyEntries);
        return string.Concat(parts.Select(part => char.ToUpperInvariant(part[0]) + part.Substring(1)));
    }

    private static string Render(
        CompleteApiModel model,
        IReadOnlyList<NativeFunction> functions,
        string className,
        string importName,
        string displayLibrary)
    {
        string modelJson = Serialize(model);
        string modelHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(modelJson)));
        var builder = new StringBuilder();
        builder.AppendLine("// <auto-generated />");
        builder.AppendLine("// Source: eng/interop/complete-api-model.json");
        builder.AppendLine("// ROCm tag: " + model.RocmTag);
        builder.AppendLine("// Complete API model SHA-256: " + modelHash);
        builder.AppendLine();
        builder.AppendLine("using System;");
        builder.AppendLine("using System.Runtime.CompilerServices;");
        builder.AppendLine("using System.Runtime.InteropServices;");
        builder.AppendLine("using JYPPX.HipSharp.Rtc;");
        builder.AppendLine("using JYPPX.HipSharp.Types;");
        builder.AppendLine();
        builder.AppendLine("namespace JYPPX.HipSharp.Interop;");
        builder.AppendLine();
        builder.AppendLine("public sealed partial class " + className);
        builder.AppendLine("{");

        foreach (NativeFunction function in functions)
        {
            string declarations = string.Join(", ", function.Parameters.Select(parameter => parameter.ManagedType + " " + parameter.ManagedName));
            string arguments = string.Join(", ", function.Parameters.Select(parameter => parameter.ManagedName));
            string nativeParameters = string.Join(", ", function.Parameters.Select(parameter => parameter.NativeType + " " + parameter.NativeName));
            builder.AppendLine("    /// <summary>调用原生 <c>" + function.EntryPoint + "</c> / Calls native <c>" + function.EntryPoint + "</c>.</summary>");
            builder.AppendLine("    /// <remarks>原生签名 / Native signature: <c>" + XmlEscape(function.NativeReturnType + " " + function.EntryPoint + "(" + nativeParameters + ")") + "</c>.</remarks>");
            foreach (NativeParameter parameter in function.Parameters)
            {
                builder.AppendLine("    /// <param name=\"" + parameter.ManagedName.TrimStart('@') + "\">原生参数 <c>" +
                    XmlEscape(parameter.NativeType + " " + parameter.NativeName) + "</c> / Native parameter.</param>");
            }
            builder.AppendLine("    /// <returns>原生返回值 / Native return value.</returns>");
            builder.AppendLine("    public " + function.ManagedReturnType + " " + function.ManagedName + "(" + declarations + ") =>");
            builder.AppendLine("        NativeMethods." + function.ManagedName + "(" + arguments + ");");
            builder.AppendLine();
        }

        builder.AppendLine("    private static partial class NativeMethods");
        builder.AppendLine("    {");
        foreach (NativeFunction function in functions)
        {
            string declarations = string.Join(", ", function.Parameters.Select(parameter => parameter.ManagedType + " " + parameter.ManagedName));
            builder.AppendLine("#if NET7_0_OR_GREATER");
            builder.AppendLine("        [LibraryImport(HipNativeLibraryNames." + importName + ", EntryPoint = \"" + function.EntryPoint + "\")]");
            builder.AppendLine("        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]");
            builder.AppendLine("        internal static partial " + function.ManagedReturnType + " " + function.ManagedName + "(" + declarations + ");");
            builder.AppendLine("#else");
            builder.AppendLine("        [DllImport(HipNativeLibraryNames." + importName + ", EntryPoint = \"" + function.EntryPoint + "\", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]");
            builder.AppendLine("        internal static extern " + function.ManagedReturnType + " " + function.ManagedName + "(" + declarations + ");");
            builder.AppendLine("#endif");
            builder.AppendLine();
        }
        builder.AppendLine("    }");
        builder.AppendLine("}");
        return builder.ToString().Replace("\r\n", "\n");
    }

    private static string CollapseWhitespace(string value) =>
        Regex.Replace(value, @"\s+", " ", RegexOptions.CultureInvariant).Trim();

    private static string XmlEscape(string value) => value
        .Replace("&", "&amp;", StringComparison.Ordinal)
        .Replace("<", "&lt;", StringComparison.Ordinal)
        .Replace(">", "&gt;", StringComparison.Ordinal);
}

internal sealed record CompleteApiModel(
    int SchemaVersion,
    string RocmTag,
    IReadOnlyList<CompleteApiHeader> Headers,
    IReadOnlyList<NativeFunction> RuntimeFunctions,
    IReadOnlyList<NativeFunction> RtcFunctions);

internal sealed record CompleteApiHeader(string Path, string Library, string Sha256);

internal sealed record NativeFunction(
    string ManagedName,
    string EntryPoint,
    string Library,
    string NativeReturnType,
    string ManagedReturnType,
    IReadOnlyList<NativeParameter> Parameters);

internal sealed record NativeParameter(
    string ManagedName,
    string NativeName,
    string NativeType,
    string ManagedType);
