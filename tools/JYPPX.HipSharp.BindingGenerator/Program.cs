using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

if (args.Length == 0)
{
    Console.Error.WriteLine("Usage: generate [--check] | probe-manifest");
    return 2;
}

string repositoryRoot = FindRepositoryRoot();
string manifestPath = Path.Combine(repositoryRoot, "eng", "interop", "interop-manifest.json");
string normalizedPath = Path.Combine(repositoryRoot, "eng", "interop", "normalized-model.json");
string generatedPath = Path.Combine(repositoryRoot, "src", "JYPPX.HipSharp", "Generated", "HipNativeMethods.g.cs");
using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
string candidateNormalized = Canonicalize(manifest.RootElement) + "\n";
string normalized = candidateNormalized;
if (File.Exists(normalizedPath))
{
    string stored = File.ReadAllText(normalizedPath).Replace("\r\n", "\n");
    using JsonDocument storedDocument = JsonDocument.Parse(stored);
    if (!JsonElement.DeepEquals(storedDocument.RootElement, manifest.RootElement))
    {
        Console.Error.WriteLine("Normalized model drift detected.");
        return 1;
    }
    normalized = stored;
}
string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));
string command = args[0].ToLowerInvariant();
bool check = args.Any(value => value is "--check" or "check");

if (command is "probe-manifest" or "probe")
{
    JsonElement root = manifest.RootElement;
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        schemaVersion = root.GetProperty("schemaVersion").GetInt32(),
        generatorVersion = root.GetProperty("generatorVersion").GetString(),
        normalizedManifestSha256 = hash,
        functionCount = root.GetProperty("functions").GetArrayLength(),
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
    if (!File.Exists(normalizedPath) || File.ReadAllText(normalizedPath).Replace("\r\n", "\n") != normalized)
    {
        Console.Error.WriteLine("Normalized model drift detected.");
        return 1;
    }
    string generated = File.ReadAllText(generatedPath);
    if (!generated.Contains("Normalized manifest SHA-256: " + hash, StringComparison.Ordinal))
    {
        Console.Error.WriteLine("Generated interop hash drift detected.");
        return 1;
    }
    Console.WriteLine("Binding generator check passed: " + hash);
    return 0;
}

if (!File.Exists(normalizedPath))
{
    File.WriteAllText(normalizedPath, normalized, new UTF8Encoding(false));
}
Console.WriteLine("Normalized model written: " + hash);
return 0;

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
