using System.Buffers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization.Metadata;
using Aw.Evidence.Core.Data;
using Aw.Evidence.Core.Ids;
using Aw.Kernel.Testing;

namespace Aw.Evidence.Tests.Schemas;

/// <summary>
/// The JSON Schemas for api-run scripts and summaries are generated from the records and committed under schemas/evidence/. A
/// record change that isn't regenerated fails here; regenerate with AW_GOLDEN_UPDATE=1 (refused in CI) and review the diff.
/// </summary>
public sealed class EvidenceSchemaTests
{
    public static TheoryData<string> Files => ["api-script", "api-summary", "db-script", "db-summary"];

    internal static JsonObject Committed(string name) =>
        Assert.IsType<JsonObject>(JsonNode.Parse(File.ReadAllText(PathOf(name))));

    [Theory, Trait("Lane", "integration")]   // reads (and in update mode writes) schemas/evidence/
    [MemberData(nameof(Files))]
    public void The_committed_schema_is_generated_from_the_record(string name)
    {
        var actual = Render(Generate(TypeOf(name), name));
        var path = PathOf(name);

        if (string.Equals(Environment.GetEnvironmentVariable(Golden.UpdateVariable), "1", StringComparison.Ordinal)
            && !string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.Ordinal))
        {
            _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, actual);
            Assert.Fail($"{path} regenerated; review the diff, then run again without {Golden.UpdateVariable}.");
        }

        Assert.True(File.Exists(path), $"{path} is missing; generate it with {Golden.UpdateVariable}=1.");
        Assert.Equal(File.ReadAllText(path), actual);
    }

    private static Type TypeOf(string name) => name switch
    {
        "api-script" => typeof(ApiScript),
        "api-summary" => typeof(ApiSummary),
        "db-script" => typeof(DbScript),
        _ => typeof(DbSummary),
    };

    private static string Title(string name) => name switch
    {
        "api-script" => "ui-evidence api-run script (evidence.api.run/1)",
        "api-summary" => "ui-evidence api-run summary (evidence.api.summary/1)",
        "db-script" => "ui-evidence db-run script (evidence.db.run/1)",
        _ => "ui-evidence db-run summary (evidence.db.summary/1)",
    };

    private static string PathOf(string name) =>
        Path.GetFullPath(Path.Combine(Golden.SourcePath(typeof(EvidenceSchemaTests).Assembly, "Schemas"), "..", "..", "..", "schemas", "evidence", $"{name}.schema.json"));

    private static JsonObject Generate(Type type, string name)
    {
        var schema = JsonSchemaExporter.GetJsonSchemaAsNode(TestJson.Options, type, new JsonSchemaExporterOptions
        {
            TreatNullObliviousAsNonNullable = true,
            TransformSchemaNode = static (context, node) => Limits(context.PropertyInfo, Transform(context.TypeInfo.Type, node)),
        });
        var root = new JsonObject
        {
            ["$schema"] = "https://json-schema.org/draft/2020-12/schema",
            ["$id"] = $"https://github.com/jthorare/agentic-workflow/schemas/evidence/{name}.schema.json",
            ["title"] = Title(name),
        };
        foreach (var (key, value) in schema.AsObject().ToList())
        {
            root[key] = value?.DeepClone();
        }

        return root;
    }

    // A typed id and a header source have user-code converters, so the exporter can't see their shape: describe them.
    private static JsonNode Transform(Type type, JsonNode node)
    {
        if (type == typeof(RunId) || type == typeof(RunInstanceId))
        {
            return new JsonObject { ["type"] = "string", ["pattern"] = "\\S" };
        }

        if (type == typeof(IReadOnlyDictionary<string, HeaderSource>) && node is JsonObject headers)
        {
            headers["additionalProperties"] = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject { ["env"] = new JsonObject { ["type"] = "string", ["pattern"] = "^[A-Z][A-Z0-9_]*$" } },
                ["required"] = new JsonArray("env"),
                ["additionalProperties"] = false,
            };
        }

        return node;
    }

    // What a produced summary always guarantees: the constants, the caps the recorder applies, and no key beyond the record's own.
    private static JsonNode Limits(JsonPropertyInfo? property, JsonNode node)
    {
        if (node is JsonObject { } record && record["properties"] is not null && record["additionalProperties"] is null)
        {
            record["additionalProperties"] = false;
        }

        if (property is not { DeclaringType: var owner } || node is not JsonObject rules)
        {
            return node;
        }

        return (owner.Name, property.Name) switch
        {
            (nameof(ApiSummary), "schema") => new JsonObject { ["const"] = ApiSummary.SchemaName },
            (nameof(ApiSummary), "kind") => new JsonObject { ["const"] = ApiSummary.KindName },
            (nameof(ApiSummary), "provenance") => new JsonObject { ["const"] = ApiSummary.Unseeded },
            (nameof(ApiSummary), "headline") => Capped(rules, 200),
            (nameof(ApiStep), "output") => Capped(rules, 2000),
            (nameof(ApiFailure), "pointer" or "expected" or "actual") => Capped(rules, 200),
            (nameof(DbSummary), "schema") => new JsonObject { ["const"] = DbSummary.SchemaName },
            (nameof(DbSummary), "kind") => new JsonObject { ["const"] = DbSummary.KindName },
            (nameof(DbSummary), "provenance") => new JsonObject { ["enum"] = new JsonArray(DbSummary.Unseeded, DbSummary.Seeded) },
            (nameof(DbSummary), "headline") => Capped(rules, 200),
            (nameof(DbSummary), "engine") => new JsonObject { ["enum"] = new JsonArray("sqlite", "postgres", null) },
            (nameof(DbStep), "output") => Capped(rules, 2000),
            (nameof(DbStep), "target") => Capped(rules, 200),
            (nameof(DbStep), "action") => new JsonObject { ["const"] = "query" },
            _ => node,
        };
    }

    private static JsonObject Capped(JsonObject rules, int limit)
    {
        rules["maxLength"] = limit;
        return rules;
    }

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
}
