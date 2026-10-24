using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Validation;

namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary>
/// The YAML recipe reader (spec §3.1): YamlDotNet's parser only (AOT-safe), the YAML 1.2 core schema, the refused
/// features as RCP-SCHEMA-003, short forms normalized to the long form, every unknown field, missing field and wrong
/// type reported at its node (RCP-SCHEMA-001/002, RCP-DUR-001), then typed binding with unmapped members disallowed
/// and the hash over the long form. The file's text is all it reads; the caller does the I/O.
/// </summary>
internal sealed class YamlRecipeReader(JsonSerializerOptions options) : IRecipeReader
{
    /// <summary>The largest recipe file read, in UTF-8 bytes (4 MiB).</summary>
    internal const int MaxBytes = 4 * 1024 * 1024;

    public RecipeReading Read(string text, string file)
    {
        ArgumentNullException.ThrowIfNull(text);
        var findings = new ReaderFindings(file);
        var size = Encoding.UTF8.GetByteCount(text);
        if (size > MaxBytes)
        {
            findings.At("RCP-SCHEMA-003", string.Empty, SourcePosition.Start,
                string.Create(CultureInfo.InvariantCulture, $"the file is {size} bytes, over the 4 MiB limit"), "split the recipe, or move long texts out of it");
            return new RecipeReading(Document: null, findings.All);
        }

        var root = YamlTree.Read(text, findings);
        if (root is JsonObject recipe)
        {
            RecipeNormalizer.Normalize(recipe, findings);
            new RecipeShape(options, findings).Check(recipe, typeof(Recipe), string.Empty, nullable: false);
        }
        else if (!findings.Any)
        {
            findings.Add("RCP-SCHEMA-002", string.Empty, "the recipe isn't a mapping",
                "write the recipe as a YAML mapping with schema, id, version, initial, states and transitions");
        }

        if (!findings.Any)
        {
            return Bind((JsonObject)root!, file, findings);   // why: no finding means the root was a mapping
        }

        return new RecipeReading(Document: null, [.. findings.All, .. SchemaPastUnknownFields(root, file, findings)]);
    }

    /// <summary>
    /// The schema rules' findings on a recipe whose only reader findings are unknown fields (spec §3.1: every schema error
    /// is reported, not just the first): the unknown fields are left out, the rest binds as written, and the schema rules
    /// run on it; their findings already reported are dropped. After any other reader finding (a wrong type, a missing
    /// field, refused YAML) the schema rules wait, since the recipe without that node would make them report on
    /// something the author didn't write. Such a recipe is never hashed or validated further.
    /// </summary>
    private IEnumerable<RecipeFinding> SchemaPastUnknownFields(JsonNode? root, string file, ReaderFindings findings)
    {
        if (root is not JsonObject || findings.Unknown.Count == 0 || findings.All.Count != findings.Unknown.Count)
        {
            return [];
        }

        foreach (var (parent, key) in findings.Unknown)
        {
            _ = parent.Remove(key);
        }

        Recipe? recipe;
        try
        {
            recipe = (Recipe?)JsonSerializer.Deserialize(root, options.GetTypeInfo(typeof(Recipe)));
        }
        catch (JsonException)
        {
            return [];
        }

        var reported = findings.All.Select(static f => (f.Rule, f.Path)).ToHashSet();
        var document = new RecipeDocument(recipe!, RecipeHashing.Of(recipe!, options), new RecipeSource(file, findings.Positions));   // why: the root is a mapping, never JSON null
        return [.. RecipeValidator.CheckSchema(document).Where(f => !findings.IsRefused(f.Path) && !reported.Contains((f.Rule, f.Path)))];
    }

    private RecipeReading Bind(JsonObject root, string file, ReaderFindings findings)
    {
        Recipe? recipe;
        try
        {
            recipe = (Recipe?)JsonSerializer.Deserialize(root, options.GetTypeInfo(typeof(Recipe)));
        }
        catch (JsonException e)
        {
            // The shape check runs first, so this is a backstop: report it at the node binding names.
            var path = (e.Path ?? "$").TrimStart('$').TrimStart('.');
            findings.Add("RCP-SCHEMA-002", path, $"the recipe doesn't bind: {e.Message}", "fix the value at that place");
            return new RecipeReading(Document: null, findings.All);
        }

        var source = new RecipeSource(file, findings.Positions);
        return new RecipeReading(new RecipeDocument(recipe!, RecipeHashing.Of(recipe!, options), source), []);   // why: the root is a mapping, never JSON null
    }
}
