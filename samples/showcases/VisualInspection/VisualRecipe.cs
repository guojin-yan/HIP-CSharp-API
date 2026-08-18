using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

internal sealed class VisualRecipe
{
    private static readonly JsonSerializerOptions RecipeJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    [JsonPropertyName("schema_version")]
    public int SchemaVersion { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }

    public List<VisualFixture> Fixtures { get; set; } = new();

    internal static VisualRecipe Load(string inputDirectory)
    {
        string path = Path.Combine(inputDirectory, "visual-fixture-recipe.json");
        VisualRecipe? recipe = JsonSerializer.Deserialize<VisualRecipe>(
            File.ReadAllText(path),
            RecipeJsonOptions);
        if (recipe is null || recipe.Fixtures.Count == 0)
        {
            throw new InvalidDataException("The visual fixture recipe is empty: " + path);
        }

        return recipe;
    }
}

internal sealed class VisualFixture
{
    public string Id { get; set; } = string.Empty;

    public string Image { get; set; } = string.Empty;

    public string Mask { get; set; } = string.Empty;

    [JsonPropertyName("defect_type")]
    public string DefectType { get; set; } = string.Empty;

    [JsonPropertyName("expected_decision")]
    public string ExpectedDecision { get; set; } = string.Empty;

    [JsonPropertyName("defect_pixels")]
    public int DefectPixels { get; set; }
}
