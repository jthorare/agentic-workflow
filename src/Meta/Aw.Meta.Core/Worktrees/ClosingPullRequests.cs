using System.Text.Json;

namespace Aw.Meta.Core.Worktrees;

/// <summary>Reads which merged pull requests closed a GitHub issue, from the GraphQL answer `gh api graphql` prints (gh 2.45 has no `issue view --json` field for it).</summary>
internal static class ClosingPullRequests
{
    /// <summary>The query: the issue's state and its closing pull requests, closed ones included. gh fills the owner and repo from the repo in the directory.</summary>
    internal const string Query = "query($owner:String!,$name:String!,$number:Int!){repository(owner:$owner,name:$name){issue(number:$number){state closedByPullRequestsReferences(first:10,includeClosedPrs:true){nodes{number state}}}}}";

    /// <summary>The merged pull requests that closed the issue, when the issue is CLOSED; empty when it is open, closed without a merged one, or the answer can't be read (unknown is never finished).</summary>
    /// <param name="json">The output: {"data":{"repository":{"issue":{"state":..,"closedByPullRequestsReferences":{"nodes":[..]}}}}}.</param>
    /// <returns>The merged pull request numbers.</returns>
    internal static IReadOnlyList<int> Merged(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (ClosedIssue(document.RootElement) is not { } issue || Child(issue, "closedByPullRequestsReferences") is not { } closers
                || !closers.TryGetProperty("nodes", out var nodes) || nodes.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            return [.. nodes.EnumerateArray()
                .Where(static node => node.ValueKind == JsonValueKind.Object && Is(node, "state", "MERGED"))
                .Select(static node => node.TryGetProperty("number", out var number) && number.ValueKind == JsonValueKind.Number && number.TryGetInt32(out var pr) ? pr : 0)
                .Where(static pr => pr > 0)];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    // data.repository.issue when its state is CLOSED; null otherwise.
    private static JsonElement? ClosedIssue(JsonElement root) =>
        Child(root, "data") is { } data && Child(data, "repository") is { } repository && Child(repository, "issue") is { } issue && Is(issue, "state", "CLOSED") ? issue : null;

    private static bool Is(JsonElement item, string name, string expected) =>
        item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && string.Equals(value.GetString(), expected, StringComparison.Ordinal);

    private static JsonElement? Child(JsonElement item, string name) =>
        item.ValueKind == JsonValueKind.Object && item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Object ? value : null;
}
