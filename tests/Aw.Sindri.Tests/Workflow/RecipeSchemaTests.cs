using System.Buffers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization.Metadata;
using Aw.Kernel.Ids;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Tests.Workflow;

/// <summary>
/// The recipe JSON Schemas (W25) under schemas/recipes/: recipe.schema.json is generated from the long-form records
/// and fails here when a record changes without regenerating it (AW_GOLDEN_UPDATE=1 regenerates it, refused in CI);
/// recipe.editor.schema.json is written by hand for editors, and must list exactly the long form's fields per record.
/// </summary>
public sealed class RecipeSchemaTests
{
    private static readonly Dictionary<Type, string> _patterns = new()
    {
        [typeof(RecipeId)] = "^workflow\\.[a-z][a-z0-9]*(-[a-z0-9]+)*$",
        [typeof(BlockId)] = "^(gate|step|effect|evidence|input)(\\.[a-z][a-z0-9]*(-[a-z0-9]+)*){1,2}$",
        [typeof(FactKey)] = "^(fact:[a-z][A-Za-z0-9]*|evidence:[a-z][a-z0-9]*([.-][a-z0-9]+)*@(base|head))$",
        [typeof(EventKind)] = "^[a-z][a-z0-9]*(-[a-z0-9]+)*\\.[a-z][A-Za-z0-9]*$",
        [typeof(OutcomeName)] = "^[a-z][A-Za-z0-9]*$",
        [typeof(AnswerId)] = "^[a-z][A-Za-z0-9]*(-[a-z0-9]+)*$",
        [typeof(RecipeHash)] = "^sha256:[0-9a-f]{64}$",
        [typeof(RecipeDuration)] = "^[1-9][0-9]{0,5}[mhd]$",
    };

    [Fact, Trait("Lane", "integration")]   // reads (and in update mode writes) schemas/recipes/
    public void The_committed_schema_is_generated_from_the_long_form_records()
    {
        var actual = Render(Generate());
        var path = Path.Combine(RepoRoot(), "schemas", "recipes", "recipe.schema.json");

        if (string.Equals(Environment.GetEnvironmentVariable(Golden.UpdateVariable), "1", StringComparison.Ordinal)
            && !string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.Ordinal))
        {
            File.WriteAllText(path, actual);
            Assert.Fail($"{path} regenerated; review the diff, then run again without {Golden.UpdateVariable}.");
        }

        Assert.True(File.Exists(path), $"{path} is missing; generate it with {Golden.UpdateVariable}=1.");
        Assert.Equal(File.ReadAllText(path), actual);
    }

    [Fact, Trait("Lane", "integration")]   // reads schemas/recipes/
    public void The_editor_overlay_lists_every_long_form_field_of_every_record()
    {
        var overlay = JsonNode.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "schemas", "recipes", "recipe.editor.schema.json")))!;
        var defs = overlay["$defs"]!.AsObject();
        var records = new Dictionary<string, JsonTypeInfo>(StringComparer.Ordinal);
        Collect(typeof(Recipe), records);

        Assert.True(records.Count >= 15, "the walk found the long-form records");
        foreach (var (name, info) in records)
        {
            var fields = defs[name]?["properties"]?.AsObject().Select(p => p.Key).Order(StringComparer.Ordinal).ToList();
            Assert.True(fields is not null, $"recipe.editor.schema.json has no $defs/{name}");
            Assert.Equal(info.Properties.Select(p => p.Name).Order(StringComparer.Ordinal), fields, StringComparer.Ordinal);
        }
    }

    private static void Collect(Type type, Dictionary<string, JsonTypeInfo> records)
    {
        var info = SliceHarness.Json.GetTypeInfo(Nullable.GetUnderlyingType(type) ?? type);
        if (info.Kind == JsonTypeInfoKind.Object && records.TryAdd(info.Type.Name, info))
        {
            foreach (var property in info.Properties)
            {
                Collect(property.PropertyType, records);
            }
        }
        else if (info.Kind is JsonTypeInfoKind.Enumerable or JsonTypeInfoKind.Dictionary)
        {
            Collect(info.ElementType!, records);
        }
    }

    private static JsonObject Generate()
    {
        var schema = JsonSchemaExporter.GetJsonSchemaAsNode(SliceHarness.Json, typeof(Recipe), new JsonSchemaExporterOptions
        {
            TreatNullObliviousAsNonNullable = true,
            TransformSchemaNode = static (context, node) => Transform(context.TypeInfo.Type, node),
        });
        var root = new JsonObject
        {
            ["$schema"] = "https://json-schema.org/draft/2020-12/schema",
            ["$id"] = "https://github.com/jthorare/agentic-workflow/schemas/recipes/recipe.schema.json",
            ["title"] = "Sindri recipe, schema 1 (long form)",
            ["description"] = "Generated from the long-form records by RecipeSchemaTests; the reader normalizes short forms to this. Editors: recipe.editor.schema.json.",
        };
        foreach (var (key, value) in schema.AsObject().ToList())
        {
            root[key] = value?.DeepClone();
        }

        return root;
    }

    // A typed id has a user-code converter, so the exporter can't see its shape ('true'): describe it, and give a list
    // of ids its item schema, which the exporter leaves out when the item schema is 'true'.
    private static JsonNode Transform(Type type, JsonNode node)
    {
        if (IsTypedId(type))
        {
            return IdSchema(type);
        }

        if (Nullable.GetUnderlyingType(type) is { } underlying && IsTypedId(underlying))
        {
            var nullable = IdSchema(underlying);
            nullable["type"] = new JsonArray("string", "null");
            return nullable;
        }

        if (node is JsonObject array && type.IsGenericType && type.GetGenericArguments() is [var item] && IsTypedId(item)
            && typeof(IEnumerable<>).MakeGenericType(item).IsAssignableFrom(type))
        {
            array["items"] = IdSchema(item);
        }

        return node;
    }

    private static bool IsTypedId(Type type) =>
        type.GetInterfaces().Any(static i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ITypedId<,>));

    private static JsonObject IdSchema(Type type) => new() { ["type"] = "string", ["pattern"] = _patterns.GetValueOrDefault(type, "\\S") };

    private static string Render(JsonNode node)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions
        {
            Indented = true,
            IndentSize = 2,
            NewLine = "\n",
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        }))
        {
            node.WriteTo(writer);
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan) + "\n";
    }

    private static string RepoRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AgenticWorkflow.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("No AgenticWorkflow.slnx above the test's base directory.");
    }
}
