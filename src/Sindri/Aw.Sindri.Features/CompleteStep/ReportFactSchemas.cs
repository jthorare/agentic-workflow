using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.CompleteStep;

/// <summary>
/// The facts a scoping agent writes whose shape a schema closes (#1112): fact:scopeMap, fact:acceptance, fact:openQuestions and fact:questions (the facts an agent Step writes that have a schema; SCP-SCHEMA-001 checks the first two one Step later).
/// Checked when the agent reports, so a field its schema does not allow is fixed by the agent that wrote it, in the same run, not by a new drafter after 'check'.
/// </summary>
internal static partial class ReportFactSchemas
{
    private static readonly Dictionary<string, JsonNode> _documents = new(StringComparer.Ordinal);
    private static readonly Lock _gate = new();
    private static readonly Dictionary<string, string> _schemas = new(StringComparer.Ordinal)
    {
        ["fact:scopeMap"] = "scope-map",
        ["fact:acceptance"] = "acceptance",
        ["fact:openQuestions"] = "open-questions",
        ["fact:questions"] = "questions",
    };

    /// <summary>Whether <paramref name="fact"/> has a schema checked at report time.</summary>
    /// <param name="fact">The fact.</param>
    /// <returns>True for scopeMap, acceptance, openQuestions and questions.</returns>
    internal static bool Covers(FactKey fact) => _schemas.ContainsKey(fact.Value);

    /// <summary>Where each covered fact's schema is, for the Step's instructions.</summary>
    /// <param name="fact">A covered fact.</param>
    /// <returns>The repo-relative schema path.</returns>
    internal static string SchemaPath(string fact) => $"schemas/scoping/{_schemas[fact]}.json";

    /// <summary>Why <paramref name="content"/> breaks the fact's schema, one entry per place, naming the fact, the JSON path and the schema file; never the fact's text. Empty when it holds or is not JSON (check reports that).</summary>
    /// <param name="fact">A covered fact.</param>
    /// <param name="content">What the agent wrote.</param>
    /// <returns>The refusals.</returns>
    internal static IReadOnlyList<string> Violations(FactKey fact, ReadOnlySpan<byte> content)
    {
        JsonNode? tree;
        try
        {
            tree = JsonNode.Parse(Encoding.UTF8.GetString(content));
        }
        catch (JsonException)   // boundary: the content is what an agent wrote
        {
            return [];
        }

        var path = SchemaPath(fact.Value);
        return [.. Errors(_schemas[fact.Value], tree)
            .Where(static e => !e.Path.EndsWith("/trust", StringComparison.Ordinal))   // same filter as SCP-SCHEMA-001: code stamps trust
            .Select(e => $"{fact.Value}{e.Path}: {Unquoted(e.Message)} (fix: make {fact.Value} match {path})")];
    }

    private static IReadOnlyList<(string Path, string Message)> Errors(string schema, JsonNode? tree)
    {
        var document = Document(schema);
        return new JsonSchemaLite(document).Validate(tree, (document["$defs"] as JsonObject)?["fact"] ?? document);
    }

    private static JsonNode Document(string name)
    {
        lock (_gate)
        {
            if (!_documents.TryGetValue(name, out var document))
            {
                using var stream = typeof(ReportFactSchemas).Assembly.GetManifestResourceStream($"scoping-schemas/{name}.json")
                    ?? throw new InvalidOperationException($"The scoping schema '{name}' is not embedded (schemas/scoping/{name}.json).");
                using var reader = new StreamReader(stream, Encoding.UTF8);
                document = JsonNode.Parse(reader.ReadToEnd()) ?? new JsonObject();
                _documents[name] = document;
            }

            return document;
        }
    }

    /// <summary>A pattern error quotes the offending text; drop the quote.</summary>
    private static string Unquoted(string message) => Quoted.Replace(message, "value ");

    [GeneratedRegex(@"^'.*' (?=does not match)", RegexOptions.Singleline | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Quoted { get; }
}
