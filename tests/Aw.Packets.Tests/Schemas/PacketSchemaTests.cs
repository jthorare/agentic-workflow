using System.Buffers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Packets.App;
using Aw.Packets.Core.Ids;
using Aw.Packets.Core.Kinds;
using Aw.Packets.Core.Kinds.Line;
using Aw.Packets.Core.Kinds.Scoping;
using Aw.Packets.Core.Model;
using Aw.Packets.Core.Values;

namespace Aw.Packets.Tests.Schemas;

/// <summary>
/// The JSON Schemas for editors (addendum §17.4: typed records with a generated JSON Schema) are generated from the
/// records and committed under schemas/packets/. A record change that isn't regenerated fails here; regenerate with
/// AW_GOLDEN_UPDATE=1 (refused in CI) and review the diff.
/// </summary>
public sealed class PacketSchemaTests
{
    private const string LensItemPattern = "^L-(code|product|related|history)-[1-9][0-9]*$";

    private static readonly JsonSerializerOptions _json = AwJson.CreateOptions(PacketsJsonContext.Default);

    public static TheoryData<string> Files =>
    [
        "packet", "decisions", "out-of-scope", "tasks", "freeze-record", "lenses",
        "design-packet", "plan-packet", "phase-packet", "proof-packet", "review-packet", "handoff-packet",
        "index", "acceptance", "phase", "phase-holdout", "pull-request", "sizes",
    ];

    [Theory, Trait("Lane", "integration")]   // reads (and in update mode writes) schemas/packets/
    [MemberData(nameof(Files))]
    public void The_committed_schema_is_generated_from_the_record(string name) =>
        Verify(
            name,
            RepoRoot(),
            update: string.Equals(Environment.GetEnvironmentVariable(Golden.UpdateVariable), "1", StringComparison.Ordinal),
            inCi: string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.Ordinal));

    /// <summary>Checks one schema file under <paramref name="repoRoot"/>; in update mode writes it and fails only when its content changed (Golden.Match).</summary>
    internal static void Verify(string name, string repoRoot, bool update, bool inCi) =>
        Golden.Match(Render(Generate(TypeOf(name), name)), repoRoot, $"schemas/packets/{name}.schema.json", Normalization.None, update, inCi);

    internal static string RepoRoot() =>
        Path.GetFullPath(Path.Combine(Golden.SourcePath(typeof(PacketSchemaTests).Assembly, "Schemas"), "..", "..", ".."));

    private static Type TypeOf(string name) =>
        name switch
        {
            "packet" => typeof(PacketManifest),
            "decisions" => typeof(DecisionLog),
            "out-of-scope" => typeof(OutOfScope),
            "tasks" => typeof(TaskPlan),
            "lenses" => typeof(LensesFile),
            "design-packet" => typeof(LinePacketManifest<DesignLinks>),
            "plan-packet" => typeof(LinePacketManifest<PlanLinks>),
            "phase-packet" => typeof(LinePacketManifest<PhaseLinks>),
            "proof-packet" => typeof(LinePacketManifest<ProofLinks>),
            "review-packet" => typeof(LinePacketManifest<ReviewLinks>),
            "handoff-packet" => typeof(LinePacketManifest<HandoffLinks>),
            "index" => typeof(PacketIndex),
            "acceptance" => typeof(AcceptanceFile),
            "phase" => typeof(PhaseFile),
            "phase-holdout" => typeof(PhaseHoldout),
            "pull-request" => typeof(PullRequestFile),
            "sizes" => typeof(SizesFile),
            _ => typeof(FreezeRecord),
        };

    private static JsonObject Generate(Type type, string name)
    {
        var schema = JsonSchemaExporter.GetJsonSchemaAsNode(_json, type, new JsonSchemaExporterOptions
        {
            TreatNullObliviousAsNonNullable = true,
            TransformSchemaNode = static (context, node) => Ranges(context.PropertyInfo, Transform(context.TypeInfo.Type, node)),
        });
        var root = new JsonObject
        {
            ["$schema"] = "https://json-schema.org/draft/2020-12/schema",
            ["$id"] = $"https://github.com/jthorare/agentic-workflow/schemas/packets/{name}.schema.json",
            ["title"] = $"Packet {name} file (schemaVersion 1)",
        };
        foreach (var (key, value) in schema.AsObject().ToList())
        {
            root[key] = value?.DeepClone();
        }

        LinePatch(root, name);
        return root;
    }

    // The line kinds' files: schemaVersion is 1, a packet.json says its own kind, an index.json one of the line kinds,
    // and a phase packet's id is '<line>-<phase>[-a<n>]' (PKT-PHS-007), which the exporter can't see.
    private static void LinePatch(JsonObject root, string name)
    {
        if (new[] { "packet", "decisions", "out-of-scope", "tasks", "freeze-record", "lenses" }.Contains(name, StringComparer.Ordinal)
            || root["properties"] is not JsonObject properties)
        {
            return;
        }

        if (properties["schemaVersion"] is JsonObject version)
        {
            version["const"] = 1;
        }

        if (name.EndsWith("-packet", StringComparison.Ordinal) && properties["kind"] is JsonObject packetKind)
        {
            packetKind["const"] = name[..^"-packet".Length];
            if (name == "phase-packet" && properties["id"] is JsonObject id)
            {
                id["pattern"] = "^[a-z0-9]+(?:-[a-z0-9]+)+?(?:-a[1-9][0-9]*)?$";
            }
        }

        if (name == "index" && properties["kind"] is JsonObject indexKind)
        {
            indexKind["enum"] = new JsonArray(PacketKinds.All.Where(static k => k.IsLine).Select(static k => (JsonNode)JsonValue.Create(k.Name)).ToArray());
        }
    }

    // lenses.json's ranges and id pattern (PKT-SCP-004), which the exporter can't see because ScopingChecks states them in code.
    private static JsonNode Ranges(System.Text.Json.Serialization.Metadata.JsonPropertyInfo? property, JsonNode node)
    {
        if (property is null || node is not JsonObject schema)
        {
            return node;
        }

        var key = property.DeclaringType.Name + "." + property.Name;
        if (_lensRanges.TryGetValue(key, out var apply))
        {
            apply(schema);
        }

        return node;
    }

    private static readonly Dictionary<string, Action<JsonObject>> _lensRanges = new(StringComparer.Ordinal)
    {
        ["LensEntry.depth"] = static s => { s["minimum"] = 0; s["maximum"] = ScopingChecks.MaxReach; },
        ["LensEntry.hops"] = static s => s["uniqueItems"] = true,
        ["LensEntry.mapVersion"] = static s => s["minLength"] = 1,
        ["LensEntry.foundCount"] = static s => s["minimum"] = 0,
        ["LensEntry.truncated"] = static s => s["uniqueItems"] = true,
        ["LensesFile.schemaVersion"] = static s => s["const"] = 1,
        ["LensNode.lensItem"] = static s => s["pattern"] = LensItemPattern,
        ["LensFound.lensItem"] = static s => s["pattern"] = LensItemPattern,
    };

    // A typed id has a user-code converter, so the exporter can't see its shape ('true'): describe it, and give a list
    // of ids its item schema, which the exporter leaves out when the item schema is 'true'.
    private static JsonNode Transform(Type type, JsonNode node)
    {
        if (IsTypedId(type))
        {
            var id = IdSchema(type);
            if (type == typeof(RepoSlug))
            {
                id["description"] = "owner/name; aw-packets also refuses Windows device names (con, prn, aux, nul, com1-9, lpt1-9) on either side";
            }

            return id;
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

    private static JsonObject IdSchema(Type type) => new()
    {
        ["type"] = "string",
        ["pattern"] = type == typeof(PacketId) ? "^[a-z0-9][a-z0-9-]{0,63}$"
            : type == typeof(RepoSlug) ? "^[A-Za-z0-9._-]*[A-Za-z0-9_-]/[A-Za-z0-9._-]*[A-Za-z0-9_-]$"
            : type == typeof(CommitSha) ? "^(?:[0-9a-f]{40}|[0-9a-f]{64})$"
            : type == typeof(PacketHash) ? "^sha256:[0-9a-f]{64}$"
            : "\\S",
    };

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
