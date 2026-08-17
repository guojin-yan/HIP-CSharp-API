using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

Dictionary<string, string> options = ParseOptions(args);
string assemblyPath = RequirePath(options, "assembly");
if (options.TryGetValue("semantic", out string? semanticPath))
{
    Assembly semanticAssembly = Assembly.LoadFrom(assemblyPath);
    List<string> semantic = GenerateSemanticSurface(semanticAssembly);
    string semanticOutput = string.Join("\n", new[]
    {
        "# HipSharp assembly semantic snapshot schema 1",
        "# Signatures, constants, locals, exception clauses, and IL; assembly/package provenance metadata excluded.",
    }.Concat(semantic)) + "\n";
    string fullSemanticPath = Path.GetFullPath(semanticPath);
    Directory.CreateDirectory(Path.GetDirectoryName(fullSemanticPath)!);
    File.WriteAllText(fullSemanticPath, semanticOutput, new UTF8Encoding(false));
    Console.WriteLine($"Assembly semantic snapshot written: {semanticAssembly.GetTypes().Length} types, {semantic.Count} records.");
    return 0;
}
string snapshotPath = RequireOption(options, "snapshot");
string categoriesPath = RequirePath(options, "categories");
string mode = options.ContainsKey("write") ? "write" : options.ContainsKey("check") ? "check" : string.Empty;
if (mode.Length == 0)
{
    throw new ArgumentException("Specify --check or --write.");
}

CategoryConfiguration categories = JsonSerializer.Deserialize<CategoryConfiguration>(File.ReadAllText(categoriesPath), new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true,
}) ?? throw new InvalidOperationException("Unable to read the public API category configuration.");

Assembly assembly = Assembly.LoadFrom(assemblyPath);
List<string> surface = GenerateSurface(assembly, categories);
string output = string.Join("\n", new[]
{
    "# HipSharp public API snapshot schema 1",
    "# Generated from exported types and declared public/protected members.",
}.Concat(surface)) + "\n";

if (options.TryGetValue("xml", out string? xmlPath))
{
    VerifyXmlDocumentation(Path.GetFullPath(xmlPath));
}

if (mode == "write")
{
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(snapshotPath))!);
    File.WriteAllText(snapshotPath, output, new UTF8Encoding(false));
}
else
{
    string expected = File.ReadAllText(snapshotPath).Replace("\r\n", "\n", StringComparison.Ordinal);
    if (!string.Equals(expected, output, StringComparison.Ordinal))
    {
        string difference = FirstDifference(expected, output);
        throw new InvalidOperationException("Public API snapshot drift detected. " + difference + " Run the explicit -Update workflow only for a reviewed API change.");
    }
}

Console.WriteLine($"Public API {mode} passed: {assembly.GetExportedTypes().Length} types, {surface.Count(line => !line.StartsWith("T|", StringComparison.Ordinal))} members.");
return 0;

static List<string> GenerateSemanticSurface(Assembly assembly)
{
    var lines = new List<string>();
    const BindingFlags declared = BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    foreach (Type type in assembly.GetTypes().OrderBy(item => item.FullName, StringComparer.Ordinal))
    {
        string typeName = FormatType(type);
        lines.Add($"T|{(int)type.Attributes}|{typeName}|base={FormatOptionalType(type.BaseType)}|interfaces={string.Join(',', type.GetInterfaces().Select(FormatType).OrderBy(value => value, StringComparer.Ordinal))}{FormatGenericConstraints(type.GetGenericArguments())}");
        foreach (FieldInfo field in type.GetFields(declared).OrderBy(FieldIdentity, StringComparer.Ordinal))
        {
            string constant = field.IsLiteral ? "|constant=" + FormatConstant(field.GetRawConstantValue()) : string.Empty;
            lines.Add($"F|{typeName}|{(int)field.Attributes}|{FormatType(field.FieldType)}|{field.Name}{constant}");
        }
        foreach (MethodBase method in type.GetMethods(declared).Cast<MethodBase>().Concat(type.GetConstructors(declared)).OrderBy(MethodIdentity, StringComparer.Ordinal))
        {
            string returnType = method is MethodInfo methodInfo ? FormatType(methodInfo.ReturnType) : "System.Void";
            string parameters = string.Join(',', method.GetParameters().Select(parameter => FormatType(parameter.ParameterType)));
            MethodBody? body = method.GetMethodBody();
            string bodyValue = body is null
                ? "none"
                : $"max={body.MaxStackSize};init={body.InitLocals};locals={string.Join(',', body.LocalVariables.Select(local => FormatType(local.LocalType) + (local.IsPinned ? " pinned" : string.Empty)))};exceptions={string.Join(',', body.ExceptionHandlingClauses.Select(FormatExceptionClause))};il={Convert.ToHexString(body.GetILAsByteArray() ?? Array.Empty<byte>())}";
            int genericArgumentCount = method is MethodInfo genericMethod ? genericMethod.GetGenericArguments().Length : 0;
            lines.Add($"M|{typeName}|{(int)method.Attributes}|{(int)method.MethodImplementationFlags}|{returnType}|{method.Name}|generic={genericArgumentCount}|params={parameters}|{bodyValue}");
        }
    }
    return lines.OrderBy(line => line, StringComparer.Ordinal).ToList();
}

static string FormatOptionalType(Type? type) => type is null ? string.Empty : FormatType(type);

static string FieldIdentity(FieldInfo field) => field.Name + "|" + FormatType(field.FieldType);

static string MethodIdentity(MethodBase method) => method.Name + "|" + string.Join(',', method.GetParameters().Select(parameter => FormatType(parameter.ParameterType)));

static string FormatExceptionClause(ExceptionHandlingClause clause)
{
    int filterOffset = clause.Flags == ExceptionHandlingClauseOptions.Filter ? clause.FilterOffset : -1;
    string catchType = clause.Flags == ExceptionHandlingClauseOptions.Clause ? FormatOptionalType(clause.CatchType) : string.Empty;
    return $"{clause.Flags}:{clause.TryOffset}:{clause.TryLength}:{clause.HandlerOffset}:{clause.HandlerLength}:{filterOffset}:{catchType}";
}

static List<string> GenerateSurface(Assembly assembly, CategoryConfiguration categories)
{
    var lines = new List<string>();
    const BindingFlags declared = BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    foreach (Type type in assembly.GetExportedTypes().OrderBy(item => item.FullName, StringComparer.Ordinal))
    {
        string category = GetCategory(type, categories);
        string typeName = FormatType(type);
        string modifiers = TypeModifiers(type);
        string baseType = type.BaseType is null || type.BaseType == typeof(object) || type.BaseType == typeof(ValueType) || type.BaseType == typeof(Enum)
            ? string.Empty
            : ";base=" + FormatType(type.BaseType);
        // Enum interfaces are supplied by each target framework, not declared by HipSharp.
        string interfaces = type.IsEnum
            ? string.Empty
            : string.Join(",", type.GetInterfaces().Select(FormatType).OrderBy(value => value, StringComparer.Ordinal));
        lines.Add($"T|{category}|{modifiers}|{typeName}{baseType};interfaces={interfaces}{FormatGenericConstraints(type.GetGenericArguments())}");

        foreach (ConstructorInfo constructor in type.GetConstructors(declared).Where(IsVisible))
        {
            lines.Add($"C|{category}|{Visibility(constructor)}|{typeName}({FormatParameters(constructor.GetParameters())})");
        }
        foreach (MethodInfo method in type.GetMethods(declared).Where(IsVisible).Where(method => !IsAccessor(method)))
        {
            string generic = method.IsGenericMethodDefinition ? "<" + string.Join(",", method.GetGenericArguments().Select(argument => argument.Name)) + ">" : string.Empty;
            lines.Add($"M|{category}|{MethodModifiers(method)}|{FormatType(method.ReturnType)} {typeName}.{method.Name}{generic}({FormatParameters(method.GetParameters())}){FormatGenericConstraints(method.GetGenericArguments())}");
        }
        foreach (PropertyInfo property in type.GetProperties(declared).Where(property => property.GetAccessors(true).Any(IsVisible)))
        {
            MethodInfo? getter = property.GetMethod;
            MethodInfo? setter = property.SetMethod;
            string access = $"get:{AccessorVisibility(getter)},set:{AccessorVisibility(setter)}";
            string index = property.GetIndexParameters().Length == 0 ? string.Empty : "[" + FormatParameters(property.GetIndexParameters()) + "]";
            lines.Add($"P|{category}|{access}|{FormatType(property.PropertyType)} {typeName}.{property.Name}{index}");
        }
        foreach (EventInfo eventInfo in type.GetEvents(declared).Where(eventInfo => eventInfo.GetAddMethod(true) is MethodInfo add && IsVisible(add)))
        {
            lines.Add($"E|{category}|{Visibility(eventInfo.GetAddMethod(true)!)}|{FormatType(eventInfo.EventHandlerType!)} {typeName}.{eventInfo.Name}");
        }
        foreach (FieldInfo field in type.GetFields(declared)
                     .Where(field => field.Name != "value__")
                     .Where(field => field.IsPublic || field.IsFamily || field.IsFamilyOrAssembly))
        {
            string value = field.IsLiteral ? "=" + FormatConstant(field.GetRawConstantValue()) : string.Empty;
            lines.Add($"F|{category}|{FieldModifiers(field)}|{FormatType(field.FieldType)} {typeName}.{field.Name}{value}");
        }
    }

    return lines.OrderBy(line => line, StringComparer.Ordinal).ToList();
}

static string GetCategory(Type type, CategoryConfiguration categories)
{
    string name = type.FullName ?? type.Name;
    if (categories.DiagnosticNamespacePrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal))) return "diagnostic";
    if (categories.FormalNamespacePrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal))) return "formal";
    throw new InvalidOperationException("Exported type is not categorized: " + name);
}

static string TypeModifiers(Type type)
{
    string kind = type.IsEnum ? "enum" : type.IsInterface ? "interface" : type.IsValueType ? "struct" : "class";
    var modifiers = new List<string> { type.IsNested ? TypeVisibility(type) : "public" };
    if (type.IsAbstract && type.IsSealed) modifiers.Add("static");
    else
    {
        if (type.IsAbstract) modifiers.Add("abstract");
        if (type.IsSealed) modifiers.Add("sealed");
    }
    if (type.CustomAttributes.Any(attribute => attribute.AttributeType.FullName == "System.Runtime.CompilerServices.IsReadOnlyAttribute")) modifiers.Add("readonly");
    modifiers.Add(kind);
    return string.Join(" ", modifiers);
}

static bool IsVisible(MethodBase method) => method.IsPublic || method.IsFamily || method.IsFamilyOrAssembly;

static string Visibility(MethodBase method) => method.IsPublic ? "public" : method.IsFamily ? "protected" : "protected-internal";

static string TypeVisibility(Type type) => type.IsNestedPublic ? "public" : type.IsNestedFamily ? "protected" : type.IsNestedFamORAssem ? "protected-internal" : "public";

static string MethodModifiers(MethodInfo method)
{
    var values = new List<string> { Visibility(method) };
    if (method.IsStatic) values.Add("static");
    if (method.IsAbstract) values.Add("abstract");
    else if (method.IsVirtual && method.GetBaseDefinition() != method) values.Add("override");
    else if (method.IsVirtual) values.Add("virtual");
    return string.Join(" ", values);
}

static string FieldModifiers(FieldInfo field)
{
    var values = new List<string> { field.IsPublic ? "public" : field.IsFamily ? "protected" : "protected-internal" };
    if (field.IsLiteral) values.Add("const");
    else
    {
        if (field.IsStatic) values.Add("static");
        if (field.IsInitOnly) values.Add("readonly");
    }
    return string.Join(" ", values);
}

static bool IsAccessor(MethodInfo method) => method.IsSpecialName &&
    (method.Name.StartsWith("get_", StringComparison.Ordinal) || method.Name.StartsWith("set_", StringComparison.Ordinal) ||
     method.Name.StartsWith("add_", StringComparison.Ordinal) || method.Name.StartsWith("remove_", StringComparison.Ordinal));

static string AccessorVisibility(MethodInfo? method) => method is null || !IsVisible(method) ? "none" : Visibility(method);

static string FormatParameters(IEnumerable<ParameterInfo> parameters) => string.Join(",", parameters.Select(parameter =>
{
    Type parameterType = parameter.ParameterType;
    string prefix = parameterType.IsByRef ? parameter.IsOut ? "out " : parameter.IsIn ? "in " : "ref " : string.Empty;
    if (parameterType.IsByRef) parameterType = parameterType.GetElementType()!;
    object? defaultValue = parameter.HasDefaultValue ? parameter.DefaultValue : null;
    if (parameterType == typeof(IntPtr) && defaultValue is null)
    {
        defaultValue = IntPtr.Zero;
    }
    string optional = parameter.HasDefaultValue ? "=" + FormatConstant(defaultValue) : string.Empty;
    return prefix + FormatType(parameterType) + " " + parameter.Name + optional;
}));

static string FormatType(Type type)
{
    if (type.IsByRef) return FormatType(type.GetElementType()!) + "&";
    if (type.IsPointer) return FormatType(type.GetElementType()!) + "*";
    if (type.IsArray) return FormatType(type.GetElementType()!) + "[" + new string(',', type.GetArrayRank() - 1) + "]";
    if (type.IsGenericParameter) return type.Name;
    string name = (type.FullName ?? type.Name).Replace('+', '.');
    int marker = name.IndexOf('`');
    if (!type.IsGenericType) return marker >= 0 ? name[..marker] : name;
    if (marker >= 0) name = name[..marker];
    return name + "<" + string.Join(",", type.GetGenericArguments().Select(FormatType)) + ">";
}

static string FormatGenericConstraints(Type[] arguments)
{
    var constraints = new List<string>();
    foreach (Type argument in arguments.Where(argument => argument.IsGenericParameter))
    {
        var values = new List<string>();
        GenericParameterAttributes attributes = argument.GenericParameterAttributes;
        if ((attributes & GenericParameterAttributes.ReferenceTypeConstraint) != 0) values.Add("class");
        if ((attributes & GenericParameterAttributes.NotNullableValueTypeConstraint) != 0) values.Add("struct");
        values.AddRange(argument.GetGenericParameterConstraints().Select(FormatType));
        if ((attributes & GenericParameterAttributes.DefaultConstructorConstraint) != 0 && !values.Contains("struct", StringComparer.Ordinal)) values.Add("new()");
        if (values.Count != 0) constraints.Add(argument.Name + ":" + string.Join("&", values));
    }
    return constraints.Count == 0 ? string.Empty : ";where=" + string.Join(",", constraints);
}

static string FormatConstant(object? value)
{
    if (value is null || value == DBNull.Value || value == Missing.Value) return "null";
    if (value is string text) return JsonSerializer.Serialize(text);
    if (value is char character) return "'" + character.ToString().Replace("'", "\\'", StringComparison.Ordinal) + "'";
    if (value is bool boolean) return boolean ? "true" : "false";
    return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
}

static void VerifyXmlDocumentation(string xmlPath)
{
    XDocument document = XDocument.Load(xmlPath);
    Regex chinese = new("[\\u4e00-\\u9fff]", RegexOptions.CultureInvariant);
    foreach (XElement member in document.Descendants("member"))
    {
        string summary = member.Element("summary")?.Value.Trim() ?? string.Empty;
        if (summary.Length == 0 || !summary.Contains(" / ", StringComparison.Ordinal) || !chinese.IsMatch(summary))
        {
            throw new InvalidOperationException("XML documentation is not bilingual for " + member.Attribute("name")?.Value);
        }
    }
}

static string FirstDifference(string expected, string actual)
{
    string[] left = expected.Split('\n');
    string[] right = actual.Split('\n');
    int count = Math.Min(left.Length, right.Length);
    for (int index = 0; index < count; index++)
    {
        if (!string.Equals(left[index], right[index], StringComparison.Ordinal))
        {
            return $"First difference at line {index + 1}: expected '{left[index]}', actual '{right[index]}'.";
        }
    }
    return $"Line counts differ: expected {left.Length}, actual {right.Length}.";
}

static Dictionary<string, string> ParseOptions(string[] arguments)
{
    var result = new Dictionary<string, string>(StringComparer.Ordinal);
    for (int index = 0; index < arguments.Length; index++)
    {
        string argument = arguments[index];
        if (!argument.StartsWith("--", StringComparison.Ordinal)) throw new ArgumentException("Unexpected argument: " + argument);
        string name = argument[2..];
        if (name is "check" or "write") result[name] = "true";
        else
        {
            if (++index >= arguments.Length) throw new ArgumentException("Missing value for " + argument);
            result[name] = arguments[index];
        }
    }
    return result;
}

static string RequireOption(IReadOnlyDictionary<string, string> options, string name) =>
    options.TryGetValue(name, out string? value) && !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException("Missing --" + name);

static string RequirePath(IReadOnlyDictionary<string, string> options, string name)
{
    string path = Path.GetFullPath(RequireOption(options, name));
    return File.Exists(path) ? path : throw new FileNotFoundException("Input file is missing.", path);
}

internal sealed class CategoryConfiguration
{
    public string[] FormalNamespacePrefixes { get; init; } = Array.Empty<string>();
    public string[] DiagnosticNamespacePrefixes { get; init; } = Array.Empty<string>();
}
