using System.Globalization;
using System.Text.Json;
using Aw.Meta.Core.Watch;

namespace Aw.Meta.Core.Github;

/// <summary>Reads the JSON `gh` prints and the one line `git ls-remote` prints. Anything unexpected is "nothing", never an exception.</summary>
public static class GhJson
{
    /// <summary>Reads `gh pr view --json headRefOid,headRefName,isDraft,title,state`.</summary>
    /// <param name="json">The output.</param>
    /// <returns>The pull request, or null when the output is not a JSON object.</returns>
    public static PrDetails? View(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.Object ? Details(document.RootElement) : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Reads `gh pr list --json number,state,isDraft,headRefOid,mergeable`.</summary>
    /// <param name="json">The output.</param>
    /// <returns>The pull requests, or null when the output is not a JSON array.</returns>
    public static IReadOnlyList<PrView>? List(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.Array
                ? [.. document.RootElement.EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.Object && item.TryGetProperty("number", out var number) && number.TryGetInt32(out _))
                    .Select(View)]
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Reads `gh pr list --json number,state,headRefName,headRefOid,mergedAt,closedAt`.</summary>
    /// <param name="json">The output.</param>
    /// <returns>The pull requests, or null when the output is not a JSON array.</returns>
    public static IReadOnlyList<PrBranch>? Branches(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.Array
                ? [.. document.RootElement.EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.Object && item.TryGetProperty("number", out var number) && number.TryGetInt32(out _))
                    .Select(item => new PrBranch(item.GetProperty("number").GetInt32(), State(Text(item, "state")), Text(item, "headRefName"), Text(item, "headRefOid"), Ended(item)))]
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Reads the sha from `git ls-remote origin main`.</summary>
    /// <param name="output">The output: "&lt;sha&gt;&lt;tab&gt;refs/heads/main".</param>
    /// <returns>The sha, or null when the output has none.</returns>
    public static string? RemoteSha(string output)
    {
        ArgumentNullException.ThrowIfNull(output);
        var first = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault()?.Split('\t', ' ')[0];
        return first is { Length: >= 7 } ? first : null;
    }

    /// <summary>Reads `gh pr view --json mergeCommit`.</summary>
    /// <param name="json">The output: {"mergeCommit":{"oid":"..."}}.</param>
    /// <returns>The merge commit's sha, or null when there is none (not merged yet, or unexpected output).</returns>
    public static string? MergeCommit(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var sha = root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("mergeCommit", out var commit)
                && commit.ValueKind == JsonValueKind.Object
                ? Text(commit, "oid")
                : string.Empty;
            return sha.Length >= 7 ? sha : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static PrView View(JsonElement item) => new(
        item.GetProperty("number").GetInt32(),
        State(Text(item, "state")),
        Flag(item, "isDraft"),
        Text(item, "headRefOid"),
        Text(item, "mergeable"));

    private static PrDetails Details(JsonElement item) => new(Text(item, "headRefOid"), Text(item, "headRefName"), Flag(item, "isDraft"), Text(item, "title"), State(Text(item, "state")), Ended(item));

    // mergedAt for a merged pull request, closedAt for a closed one; null while open, when the field is missing, null or not a date. gh prints a never-set time as year 1, which is no time either.
    private static DateTimeOffset? Ended(JsonElement item) => State(Text(item, "state")) switch
    {
        PrState.Merged => Time(item, "mergedAt"),
        PrState.Closed => Time(item, "closedAt"),
        PrState.Open => null,
        _ => null,
    };

    private static DateTimeOffset? Time(JsonElement item, string name) =>
        DateTimeOffset.TryParse(Text(item, name), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var time) && time.Year > 1 ? time : null;

    private static PrState State(string state) => state.ToUpperInvariant() switch
    {
        "MERGED" => PrState.Merged,
        "CLOSED" => PrState.Closed,
        _ => PrState.Open,
    };

    private static string Text(JsonElement item, string name) =>
        item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;

    private static bool Flag(JsonElement item, string name) =>
        item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;
}
