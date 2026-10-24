using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Aw.Sindri.Core.Prove;
using Aw.Sindri.Core.Workflow.Blocks;

namespace Aw.Sindri.Features.Ship;

/// <summary>
/// The PR hand-off packet (line spec §3.2 row 7, §7.5): the files <c language="text">step.ship.pack</c> writes, as pure functions of the packets it hands off. pr.json names the base, the
/// head branch and sha, the stack parent, the title and the labels; body.md is the evidence pack as the PR text; green.json records the commands the local GREEN gate ran; the manifest and index chain the proof pack and the review result.
/// </summary>
internal static class HandoffPack
{
    private const int TitleMax = 72;
    private const string DefaultBase = "main";

    /// <summary>The names of the packet's files.</summary>
    internal static IReadOnlyList<string> FileNames { get; } = ["packet.json", "index.json", "pr.json", "body.md", "green.json"];

    /// <summary>The pull request's title: the first line of the phase's goal, cut at a word with an ellipsis when it is over 72 characters.</summary>
    /// <param name="goal">The phase's goal.</param>
    /// <returns>The title.</returns>
    internal static string Title(string goal)
    {
        var line = goal.Split('\n', 2)[0].Trim();
        if (line.Length <= TitleMax)
        {
            return line;
        }

        var cut = line[..(TitleMax - 3)];
        var space = cut.LastIndexOf(' ');
        return (space > 0 ? cut[..space] : cut).TrimEnd() + "...";
    }

    /// <summary>The labels: 'test-change' when the phase may change existing tests.</summary>
    /// <param name="sources">The packets.</param>
    /// <returns>The labels.</returns>
    internal static IReadOnlyList<string> Labels(HandoffSources sources) =>
        TestChanges(sources.Phase).Count > 0 ? ["test-change"] : [];

    /// <summary>pr.json.</summary>
    /// <param name="sources">The packets.</param>
    /// <param name="title">The title, redacted.</param>
    /// <param name="labels">The labels, redacted.</param>
    /// <returns>The file's JSON.</returns>
    internal static JsonObject Pr(HandoffSources sources, string title, IReadOnlyList<string> labels) => new()
    {
        ["schemaVersion"] = 1,
        ["base"] = sources.Inputs.Stack ?? DefaultBranch(sources.Phase),
        ["headBranch"] = Branch(sources),
        ["headSha"] = sources.Head,
        ["stackParent"] = sources.Inputs.Stack,
        ["title"] = title,
        ["labels"] = new JsonArray([.. labels.Select(static l => (JsonNode)JsonValue.Create(l))]),
    };

    /// <summary>green.json.</summary>
    /// <param name="sources">The packets.</param>
    /// <returns>The file's JSON.</returns>
    internal static JsonObject Green(HandoffSources sources) => new()
    {
        ["schemaVersion"] = 1,
        ["head"] = sources.Head,
        ["commands"] = new JsonArray([.. sources.Inputs.Commands.Select(static c => (JsonNode)JsonValue.Create(c))]),
    };

    /// <summary>packet.json.</summary>
    /// <param name="sources">The packets.</param>
    /// <returns>The file's JSON.</returns>
    internal static JsonObject Packet(HandoffSources sources) => new()
    {
        ["schemaVersion"] = 1,
        ["kind"] = "handoff",
        ["id"] = sources.PacketId + "-handoff",
        ["repo"] = sources.Repo,
        ["baseSha"] = sources.BaseSha,
        ["status"] = "draft",
        ["hash"] = null,
        ["links"] = new JsonObject { ["pr"] = "pr.json", ["body"] = "body.md", ["green"] = "green.json", ["index"] = "index.json" },
    };

    /// <summary>index.json: the proof pack and the review result are the parents, and the acceptance extends the proof pack's.</summary>
    /// <param name="sources">The packets.</param>
    /// <returns>The file's JSON.</returns>
    internal static JsonObject Index(HandoffSources sources) => new()
    {
        ["schemaVersion"] = 1,
        ["kind"] = "handoff",
        ["parents"] = new JsonArray(sources.Inputs.ProofHash, sources.Inputs.ReviewHash),
        ["acceptance"] = new JsonObject { ["self"] = null, ["extends"] = ProofPack.Sha256(sources.ProofAcceptanceText) },
    };

    /// <summary>body.md: summary, acceptance table, hidden scenarios, not proven, review, the stack, test changes and a footer. It names scenario items and verdicts, never a scenario's id or text.</summary>
    /// <param name="sources">The packets.</param>
    /// <returns>The text.</returns>
    internal static string Body(HandoffSources sources)
    {
        var body = new StringBuilder();
        _ = body.Append("## Summary\n\n").Append(sources.Goal.Trim()).Append("\n\n## Acceptance\n\n").Append(Acceptance(sources.ProofAcceptance))
            .Append("\n## Hidden scenarios\n\n").Append(Hidden(sources.ProofHoldout))
            .Append("\n## Not proven\n\n").Append(NotProven(sources.NotProven))
            .Append("\n## Review\n\n").Append(Review(sources.Review)).Append('\n');
        if (sources.Inputs.Commands.Count > 0)
        {
            _ = body.Append("\n## Local checks\n\nPassed on the head: ").AppendJoin(", ", sources.Inputs.Commands.Select(static c => $"`{c}`")).Append(".\n");
        }

        if (sources.Inputs.Stack is { } stack)
        {
            _ = body.Append("\n## Stack\n\nStacked on `").Append(stack).Append("`; it merges after that pull request.\n");
        }

        if (TestChanges(sources.Phase) is { Count: > 0 } changes)
        {
            _ = body.Append("\n## Existing tests changed\n\n").AppendJoin('\n', changes.Select(static c => $"- `{c.Path}`: {c.Reason}")).Append('\n');
        }

        return body.Append("\n---\n").Append(Footer(sources)).Append('\n').ToString();
    }

    private static string Footer(HandoffSources sources)
    {
        var where = sources.Inputs.Line is { } line ? $"Line `{line.Name}`, phase `{line.Phase}`, attempt {line.Attempt.ToString(CultureInfo.InvariantCulture)}" : $"Phase `{sources.PacketId}`";
        return $"{where}; phase packet `{sources.Inputs.PacketHash}`, proof `{sources.Inputs.ProofHash}`, review `{sources.Inputs.ReviewHash}`";
    }

    // A line names its branch the way the phase's seed made it; outside a line the packet's id does.
    private static string Branch(HandoffSources sources) =>
        sources.Inputs.Line is { } line ? $"sindri/{line.Name}/{line.Phase}" : $"sindri/{sources.PacketId}";

    private static string DefaultBranch(JsonNode? phase) =>
        string.Equals(StepFacts.Field(phase?["base"], "rule"), "default-branch", StringComparison.Ordinal) && StepFacts.Field(phase?["base"], "ref") is { } name ? name : DefaultBase;

    private static string Acceptance(JsonNode? acceptance)
    {
        var table = new StringBuilder("| Item | Statement | Status | Evidence |\n|---|---|---|---|\n");
        foreach (var item in (acceptance?["items"] as JsonArray ?? []).OfType<JsonObject>())
        {
            var evidence = (item["evidence"] as JsonArray ?? []).OfType<JsonObject>().Select(Reference).ToList();
            _ = table.Append("| ").Append(Cell(item["id"])).Append(" | ").Append(Cell(item["text"])).Append(" | ").Append(Cell(item["status"])).Append(" | ")
                .Append(evidence.Count == 0 ? "none" : string.Join("<br>", evidence)).Append(" |\n");
        }

        return table.ToString();
    }

    private static string Reference(JsonObject evidence)
    {
        var commit = StepFacts.Field(evidence, "commit") ?? string.Empty;
        var sha = StepFacts.Field(evidence, "sha256") ?? string.Empty;
        return $"{StepFacts.Field(evidence, "kind")} `{StepFacts.Field(evidence, "path")}` @ `{commit[..Math.Min(7, commit.Length)]}` (`{sha[..Math.Min(23, sha.Length)]}`)";
    }

    private static string Hidden(JsonNode? holdout)
    {
        var results = (holdout?["results"] as JsonArray ?? []).OfType<JsonObject>().ToList();
        if (results.Count == 0)
        {
            return "No hidden scenarios ran.\n";
        }

        var passed = results.Count(static r => string.Equals(StepFacts.Field(r, "verdict"), "pass", StringComparison.Ordinal));
        var spent = results.Count(static r => r["spent"] is JsonValue v && v.TryGetValue<bool>(out var s) && s);
        var summary = string.Create(CultureInfo.InvariantCulture, $"{results.Count} hidden {(results.Count == 1 ? "scenario" : "scenarios")} ran: {passed} passed, {results.Count - passed} failed, {spent} spent.\n\n");
        return summary + string.Join('\n', results.Select(static r => $"- {StepFacts.Field(r, "item") ?? "(no item)"}: {StepFacts.Field(r, "verdict")}")) + "\n";
    }

    // The pack writes either one line, 'nothing skipped', or a heading and a list.
    private static string NotProven(string text)
    {
        var lines = text.Split('\n').Where(static l => !l.StartsWith('#')).Select(static l => l.TrimEnd()).Where(static l => l.Length > 0).ToList();
        return lines.Count == 0 || lines.TrueForAll(static l => l.StartsWith("nothing skipped", StringComparison.OrdinalIgnoreCase)) ? "Nothing skipped.\n" : string.Join('\n', lines) + "\n";
    }

    private static string Review(JsonNode? review)
    {
        var counts = review?["counts"];
        var rounds = Count(review, "round");
        return string.Create(CultureInfo.InvariantCulture, $"{StepFacts.Field(review, "verdict")} after {Plural(rounds, "round")}: {Count(counts, "blocking")} blocking, {Count(counts, "issue")} issue, {Plural(Count(counts, "suggestions"), "suggestion")}, {Plural(Count(counts, "nits"), "nit")}.\n");
    }

    private static string Plural(int count, string noun) => string.Create(CultureInfo.InvariantCulture, $"{count} {noun}{(count == 1 ? string.Empty : "s")}");

    private static int Count(JsonNode? node, string key) => node?[key] is JsonValue value && value.TryGetValue<int>(out var count) ? count : 0;

    private static string Cell(JsonNode? value) => ((value as JsonValue)?.TryGetValue<string>(out var text) == true ? text : string.Empty).Replace("|", "\\|", StringComparison.Ordinal).Replace('\n', ' ');

    private static List<(string Path, string Reason)> TestChanges(JsonNode? phase) =>
        [.. (phase?["testChange"] as JsonArray ?? []).OfType<JsonObject>().Select(static c => (Path: StepFacts.Field(c, "path") ?? string.Empty, Reason: StepFacts.Field(c, "reason") ?? string.Empty)).Where(static c => c.Path.Length > 0)];
}
