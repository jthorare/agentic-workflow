using System.Collections.Frozen;
using System.Text.Json.Nodes;

namespace Aw.Sindri.Core.Connections.Polling;

/// <summary>
/// The payload schema and sample of each pollable event kind that declares one (the GitHub kinds): the one place validate, the block catalog and the poll read them from.
/// A kind without an entry (Linear, Slack, Google) keeps the shared field set of the Connection event block.
/// </summary>
public static class EventKindSchemas
{
    private const string Str = "string";
    private const string Num = "number";
    private const string Bool = "boolean";
    private const string Arr = "array";

    // why: every event of every kind carries these
    private static readonly (string Name, string Type)[] _envelope = [("deliveryId", Str), ("occurredAt", Str), ("connection", Str), ("event", Str)];

    // why: the one base every pull request kind shares; a kind adds its own fields after it
    private static readonly (string Name, string Type)[] _prBase = [("repo", Str), ("number", Num), ("title", Str), ("author", Str), ("url", Str), ("headSha", Str), ("baseRef", Str)];

    private static readonly (string Name, string Type)[] _draft = [("draft", Bool)];
    private static readonly (string Name, string Type)[] _issueBase = [("repo", Str), ("number", Num), ("title", Str), ("url", Str), ("author", Str)];

    // why: sample values are by field name, so a field has the same example in every kind
    private static readonly FrozenDictionary<string, Func<JsonNode>> _examples = new Dictionary<string, Func<JsonNode>>(StringComparer.Ordinal)
    {
        ["deliveryId"] = static () => "github:41872905113",
        ["occurredAt"] = static () => "2026-11-12T09:30:00Z",
        ["connection"] = static () => "github:work",
        ["repo"] = static () => "acme/api",
        ["number"] = static () => 128,
        ["title"] = static () => "Retry the export on timeout",
        ["author"] = static () => "octocat",
        ["url"] = static () => "https://github.com/acme/api/pull/128",
        ["headSha"] = static () => "9fceb02d0ae598e95dc970b74767f19372d61af8",
        ["baseRef"] = static () => "main",
        ["draft"] = static () => false,
        ["reviewer"] = static () => "hubot",
        ["state"] = static () => "approved",
        ["commenter"] = static () => "hubot",
        ["body"] = static () => "Can we cap the retries at 3?",
        ["threadId"] = static () => "PRRT_kwDOAbCdEf4Aa1",
        ["conclusion"] = static () => "red",
        ["failing"] = static () => new JsonArray("build", "unit-tests"),
        ["label"] = static () => "bug",
        ["ref"] = static () => "refs/heads/main",
        ["pusher"] = static () => "octocat",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    private static readonly EventKindSchema[] _all =
    [
        Pr("github.pr.opened", _draft),
        Pr("github.pr.updated", _draft),
        Pr("github.pr.ready_for_review", _draft),
        Pr("github.pr.closed", _draft),
        Pr("github.pr.merged", _draft),
        Pr("github.pr.review_requested", [("reviewer", Str)]),
        Pr("github.pr.review_submitted", [("reviewer", Str), ("state", Str)]),
        Pr("github.pr.comment", [("commenter", Str), ("body", Str), ("threadId", Str)]),
        Pr("github.pr.checks", [("conclusion", Str), ("failing", Arr)]),
        Pr("github.pr.conflict", []),
        Build("github.issue.opened", _issueBase),
        Build("github.issue.labeled", [.. _issueBase, ("label", Str)]),
        Build("github.push", [("repo", Str), ("ref", Str), ("headSha", Str), ("pusher", Str)]),
    ];

    private static readonly FrozenDictionary<string, EventKindSchema> _byKind = _all.ToFrozenDictionary(static s => s.Kind, StringComparer.Ordinal);

    /// <summary>Gets the schemas, one per kind, in catalog order.</summary>
    public static IReadOnlyList<EventKindSchema> All => _all;

    /// <summary>Looks up one kind's schema.</summary>
    /// <param name="kind">The catalog id ('github.push').</param>
    /// <returns>The schema, or null when the kind declares none.</returns>
    public static EventKindSchema? Find(string kind) => _byKind.TryGetValue(kind, out var schema) ? schema : null;

    private static EventKindSchema Pr(string kind, (string Name, string Type)[] extras) => Build(kind, [.. _prBase, .. extras]);

    private static EventKindSchema Build(string kind, (string Name, string Type)[] own)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        var sample = new JsonObject();
        foreach (var (name, type) in _envelope.Concat(own))
        {
            fields[name] = type;
            sample[name] = string.Equals(name, "event", StringComparison.Ordinal) ? kind : _examples[name]();
        }

        return new EventKindSchema(kind, fields, sample);
    }
}
