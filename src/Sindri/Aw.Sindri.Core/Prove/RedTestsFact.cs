using System.Text.Json.Nodes;

namespace Aw.Sindri.Core.Prove;

/// <summary>
/// 'fact:redTests' as one reader sees it (line spec A.7, §7.2): an object keyed by item id, each value a list of test ids (<c language="text">["T1"]</c>) or
/// <c language="text">{tests?: [id | {id, path}], appOnly?: {signal}, commit?, output?}</c>. Red-check, before/after and PRV-RED-001 all parse it here.
/// </summary>
/// <param name="Entries">One entry per key, in the fact's order.</param>
/// <param name="Problem">Why the fact as a whole is not an object; null otherwise.</param>
public sealed record RedTestsFact(IReadOnlyList<RedEntry> Entries, string? Problem)
{
    /// <summary>The shape, as the agent and every refusal state it.</summary>
    public const string Shape = "{ \"<acceptance item id>\": { \"tests\": [\"<test id>\" | { \"id\": \"<test id>\", \"path\": \"<test file>\" }], \"appOnly\": { \"signal\": \"<signal id>\" }, \"commit\": \"<red commit sha>\", \"output\": \"<failing run, trimmed>\" } } (one key per acceptance item and nothing else at the top level; the value may also be just a list of test ids)";

    /// <summary>Parses <paramref name="fact"/>; null and <c language="text">{}</c> hold no entry.</summary>
    /// <param name="fact">The fact.</param>
    /// <returns>The parsed fact.</returns>
    public static RedTestsFact Parse(JsonNode? fact) => fact switch
    {
        null => new RedTestsFact([], Problem: null),
        JsonObject obj => new RedTestsFact([.. obj.Select(static e => Entry(e.Key, e.Value))], Problem: null),
        _ => new RedTestsFact([], $"fact:redTests must be an object keyed by item id, not {(fact is JsonArray ? "a list" : "a value")}: {Shape}"),
    };

    /// <summary>The red commit the entries name and why they can't be used as one, if they can't: none named, or two different ones.</summary>
    /// <returns>The commit, or the problem.</returns>
    public (string? Commit, string? Problem) RedCommit()
    {
        var named = Entries.Select(static e => (e.Item, e.Commit)).Where(static e => e.Commit is not null).Select(static e => (e.Item, Commit: e.Commit ?? string.Empty)).ToList();
        return named.Select(static n => n.Commit).Distinct(StringComparer.Ordinal).Count() switch
        {
            0 => (null, $"no item names the red commit the tests are run on; put \"commit\" on each item: {Shape}"),
            1 => (named[0].Commit, null),
            _ => (null, "the items name different red commits (" + string.Join(", ", named.Select(static n => $"{n.Item}: {n.Commit}")) + "); every item takes the one red commit"),
        };
    }

    private static RedEntry Entry(string item, JsonNode? value) => value switch
    {
        JsonArray list => Tested(item, list, appOnly: false, commit: null),
        JsonObject body => Body(item, body),
        _ => Wrong(item, "is neither a list of test ids nor an object"),
    };

    private static RedEntry Body(string item, JsonObject body)
    {
        var commit = body["commit"];
        if (commit is not null && !(commit is JsonValue c && c.TryGetValue<string>(out _)))
        {
            return Wrong(item, "has a \"commit\" that is not a string");
        }

        var appOnly = body["appOnly"];
        if (appOnly is not (null or JsonObject))
        {
            return Wrong(item, "has an \"appOnly\" that is not an object");
        }

        var text = (string?)commit;
        var commitName = string.IsNullOrWhiteSpace(text) ? null : text;
        return body["tests"] switch
        {
            null => new RedEntry(item, [], appOnly is not null, commitName, Problem: null),
            JsonArray list => Tested(item, list, appOnly is not null, commitName),
            _ => Wrong(item, "has \"tests\" that is not a list"),
        };
    }

    private static RedEntry Tested(string item, JsonArray list, bool appOnly, string? commit)
    {
        var tests = new List<RedTest>();
        foreach (var t in list)
        {
            if (Test(t) is not { } test)
            {
                return Wrong(item, "lists a test that is neither an id nor { id, path }");
            }

            tests.Add(test);
        }

        return new RedEntry(item, tests, appOnly, commit, Problem: null);
    }

    private static RedTest? Test(JsonNode? node) => node switch
    {
        JsonValue v when v.TryGetValue<string>(out var id) && !string.IsNullOrWhiteSpace(id) => new RedTest(id, Path: null),
        JsonObject o when o["id"] is JsonValue i && i.TryGetValue<string>(out var named) && !string.IsNullOrWhiteSpace(named) => new RedTest(named, o["path"] is JsonValue p && p.TryGetValue<string>(out var path) ? path : null),
        _ => null,
    };

    private static RedEntry Wrong(string item, string what) =>
        new(item, [], AppOnly: false, Commit: null, $"fact:redTests: \"{item}\" {what}; expected {Shape}");
}
