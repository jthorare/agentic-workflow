using System.Globalization;
using System.Text.Json.Nodes;
using Aw.Context.Core.Linear;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sync;
using Aw.Context.Infrastructure.Linear;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Context.Tests.Linear;

/// <summary>An issue's GitHub pull request attachments as links to the PR records, under the url rules, and the issue's own key that PRs naming it resolve through (projects page spec 2026-10-29 §4.5).</summary>
[Trait("Lane", "unit")]
public sealed class LinearFeedPrLinkTests
{
    private static readonly DateTimeOffset _at = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static JsonObject WithAttachments(string id, params string[] urls)
    {
        var node = FakeLinearIssues.Node(id, _at);
        node["attachments"] = new JsonObject { ["nodes"] = new JsonArray([.. urls.Select(static (u, i) => (JsonNode)new JsonObject { ["id"] = "att" + i.ToString(CultureInfo.InvariantCulture), ["url"] = u })]) };
        return node;
    }

    private static async Task<RecordItem> One(JsonObject node, params string[] githubRepos)
    {
        var fake = new FakeLinearIssues([node]);
        var settings = LinearSettings.Create("http://127.0.0.1:1/graphql", "env:LINEAR_FIXTURE_KEY", ["SPL"]).Value;
        var parts = new LinearSourceParts(new HttpClient(fake), static _ => "fixture-key", new FakeTimeProvider(_at), new CapturingLogger<LinearClient>());
        var page = await new LinearSources(parts).CreateFeed(settings, githubRepos).Pull(new RecordPull(After: null, 10, NotBefore: null), Ct);
        return Assert.Single(page.Value.Items);
    }

    private static List<string> PrLinks(RecordItem item) =>
        [.. item.Links.Where(static l => l.Kind == RecordLinkKind.Relates && l.ToRef.StartsWith("github:", StringComparison.Ordinal)).Select(static l => l.ToRef)];

    [Fact]
    public async Task A_pull_request_attachment_of_a_listed_repo_links_to_the_pr_record_in_the_profiles_spelling()
    {
        var item = await One(WithAttachments("a1", "https://github.com/ACME/web/pull/42/files", "https://github.com/acme/api/pull/7?tab=files#top"), "acme/Web", "acme/api");

        Assert.Equal(["github:pr/acme/Web/42", "github:pr/acme/api/7"], PrLinks(item));
    }

    [Theory]
    [InlineData("http://github.com/acme/web/pull/1")]
    [InlineData("https://github.com:8443/acme/web/pull/1")]
    [InlineData("https://someone@github.com/acme/web/pull/1")]
    [InlineData("https://www.github.com/acme/web/pull/1")]
    [InlineData("https://github.example.com/acme/web/pull/1")]
    [InlineData("https://github.com/acme/web/issues/1")]
    [InlineData("https://github.com/acme/other/pull/1")]
    [InlineData("https://github.com/acme/web/pull/x1")]
    [InlineData("https://github.com/acme/web/pull/")]
    [InlineData("https://github.com/acme/web/pull/0")]
    [InlineData("https://linear.app/acme/issue/SPL-1")]
    [InlineData("not a url")]
    public async Task An_attachment_that_is_not_a_pull_request_of_a_listed_repo_is_dropped(string url)
    {
        var item = await One(WithAttachments("a1", url), "acme/web");

        Assert.Empty(PrLinks(item));
    }

    [Fact]
    public async Task With_no_github_repos_listed_no_attachment_links()
    {
        var item = await One(WithAttachments("a1", "https://github.com/acme/web/pull/1"));

        Assert.Empty(PrLinks(item));
    }

    [Fact]
    public async Task The_same_pull_request_attached_twice_links_once()
    {
        var item = await One(WithAttachments("a1", "https://github.com/acme/web/pull/1", "https://github.com/acme/web/pull/1/commits"), "acme/web");

        Assert.Equal(["github:pr/acme/web/1"], PrLinks(item));
    }

    [Fact]
    public async Task An_issue_carries_its_own_key_for_prs_that_name_it()
    {
        var item = await One(FakeLinearIssues.Node("a1", _at));

        Assert.Contains(item.Links, static l => l.ToRef == "linear:issue-key/SPL-a1" && l.Kind == RecordLinkKind.KnownAs && l.Title is null);
    }
}
