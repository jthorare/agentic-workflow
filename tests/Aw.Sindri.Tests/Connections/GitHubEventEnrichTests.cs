using System.Globalization;
using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Connections.Polling;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Connections;

/// <summary>The trimmed PullRequestEvent has no title or author: the GitHub poller fills them from one GET /pulls/{n} per event (#3008).</summary>
[Lane(TestLane.Unit)]
public sealed class GitHubEventEnrichTests
{
    private const string Before = "2026-11-08T09:00:00Z";

    [Fact]
    public async Task A_trimmed_merge_gets_its_title_and_author_from_one_GET_of_the_pull()
    {
        var provider = new FakeProvider();
        provider.AddGitHubEvent(ProviderFixtures.GitHubPrTrimmed("3001", "acme/app", 77, "merged", "abc123", "2026-11-08T10:00:00Z"));
        provider.GitHubPulls.Add(77, ProviderFixtures.GitHubPull("acme/app", 77, "Fix login", "ada"));
        using var h = PollerHarness.For("github", provider);

        var polled = await h.Poller.Poll(h.Connection, EventKind.GitHubPrMerged, Before, TestContext.Current.CancellationToken);

        var e = Assert.Single(polled.Value.Events);
        Assert.Equal("Fix login", e.Fields["title"]);
        Assert.Equal("ada", e.Fields["author"]);
        Assert.Equal("https://github.com/acme/app/pull/77", e.Fields["url"]);
        Assert.Equal("/repos/acme/app/pulls/77", Assert.Single(PullRequests(provider)).Uri.AbsolutePath);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task A_failed_GET_of_the_pull_leaves_the_event_as_the_feed_had_it(HttpStatusCode status)
    {
        var provider = new FakeProvider();
        provider.AddGitHubEvent(ProviderFixtures.GitHubPrTrimmed("3001", "acme/app", 77, "merged", "abc123", "2026-11-08T10:00:00Z"));
        provider.GitHubPulls.Add(77, "{}", status);
        using var h = PollerHarness.For("github", provider);

        var polled = await h.Poller.Poll(h.Connection, EventKind.GitHubPrMerged, Before, TestContext.Current.CancellationToken);

        Assert.True(polled.IsSuccess);
        Assert.False(polled.Value.RateLimited);
        var e = Assert.Single(polled.Value.Events);
        Assert.Equal("77", e.Fields["number"]);
        Assert.False(e.Fields.ContainsKey("title"));
        Assert.False(e.Fields.ContainsKey("author"));
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("\"merged\"")]
    [InlineData("{not json")]
    public async Task A_pull_answer_that_is_not_a_JSON_object_leaves_the_event_as_the_feed_had_it(string body)
    {
        var provider = new FakeProvider();
        provider.AddGitHubEvent(ProviderFixtures.GitHubPrTrimmed("3001", "acme/app", 77, "merged", "abc123", "2026-11-08T10:00:00Z"));
        provider.GitHubPulls.Add(77, body);
        using var h = PollerHarness.For("github", provider);

        var polled = await h.Poller.Poll(h.Connection, EventKind.GitHubPrMerged, Before, TestContext.Current.CancellationToken);

        var e = Assert.Single(polled.Value.Events);
        Assert.False(e.Fields.ContainsKey("title"));
        Assert.False(e.Fields.ContainsKey("author"));
    }

    [Fact]
    public async Task A_burst_of_25_trimmed_merges_enriches_only_the_newest_20()
    {
        var provider = new FakeProvider();
        for (var i = 1; i <= 25; i++)
        {
            var created = DateTimeOffset.Parse("2026-11-08T10:00:00Z", CultureInfo.InvariantCulture).AddSeconds(i).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
            provider.AddGitHubEvent(ProviderFixtures.GitHubPrTrimmed((5000 + i).ToString(CultureInfo.InvariantCulture), "acme/app", i, "merged", "abc123", created));
            provider.GitHubPulls.Add(i, ProviderFixtures.GitHubPull("acme/app", i, "t" + i.ToString(CultureInfo.InvariantCulture), "ada"));
        }

        using var h = PollerHarness.For("github", provider);

        var polled = await h.Poller.Poll(h.Connection, EventKind.GitHubPrMerged, Before, TestContext.Current.CancellationToken);

        var events = polled.Value.Events;
        Assert.Equal(25, events.Count);
        Assert.Equal(20, PullRequests(provider).Count);
        Assert.All(events.Take(5), static e => Assert.False(e.Fields.ContainsKey("title")));   // the oldest 5 go out bare
        Assert.Equal(Enumerable.Range(6, 20).Select(static n => "t" + n.ToString(CultureInfo.InvariantCulture)).ToArray(), events.Skip(5).Select(static e => e.Fields["title"]).ToArray());
    }

    private static List<ProviderRequest> PullRequests(FakeProvider provider) =>
        [.. provider.Requests.Where(static r => r.Uri.AbsolutePath.Contains("/pulls/", StringComparison.Ordinal))];
}
