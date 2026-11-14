namespace Aw.Sindri.Infrastructure.Connections;

/// <summary>The rest actions each preset offers, one small fixed list per preset (a new action is a code change and a review, never a connection setting).</summary>
internal static class RestActions
{
    private static readonly Dictionary<string, IReadOnlyList<RestAction>> _byPreset = new(StringComparer.Ordinal)
    {
        ["github"] =
        [
            new("create_issue", "Open an issue: owner, repo, title, body.", HttpMethod.Post, "api.github.com", "/repos/{owner}/{repo}/issues"),
            new("get_issue", "Read one issue: owner, repo, number.", HttpMethod.Get, "api.github.com", "/repos/{owner}/{repo}/issues/{number}"),
            new("list_issues", "List a repo's issues: owner, repo, optional state.", HttpMethod.Get, "api.github.com", "/repos/{owner}/{repo}/issues"),
            new("list_runs", "List a repo's workflow runs: owner, repo, optional branch, optional per_page.", HttpMethod.Get, "api.github.com", "/repos/{owner}/{repo}/actions/runs"),
            new("add_comment", "Comment on an issue or pull request: owner, repo, number, body.", HttpMethod.Post, "api.github.com", "/repos/{owner}/{repo}/issues/{number}/comments"),
        ],
        ["slack"] =
        [
            new("post_message", "Post a message: channel, text.", HttpMethod.Post, "slack.com", "/api/chat.postMessage"),
        ],
    };

    /// <summary>Gets a preset's actions.</summary>
    /// <param name="preset">The preset key, or null for a hand-made connection.</param>
    /// <returns>The actions; empty when the preset has none.</returns>
    public static IReadOnlyList<RestAction> For(string? preset) => preset is not null && _byPreset.TryGetValue(preset, out var actions) ? actions : [];
}
