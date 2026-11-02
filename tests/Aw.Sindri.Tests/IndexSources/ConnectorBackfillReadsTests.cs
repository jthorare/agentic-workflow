using System.Globalization;
using Aw.Connectors.Core.GitHub;
using Aw.Connectors.Core.Slack;
using Aw.Connectors.Features;
using Aw.Connectors.Testing;
using Aw.Connectors.Testing.GitHub;
using Aw.Connectors.Testing.Slack;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Sync;
using Aw.Sindri.Host.IndexSources;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.IndexSources;

/// <summary>Backfill pulls everything older than <see cref="RecordPull.Before"/> (#1519): the feed drops newer records after the connector read them, so the GitHub and Slack readers must not be asked for a record at or after the bound at all.</summary>
[Trait("Lane", "unit")]
public sealed class ConnectorBackfillReadsTests
{
    private const string Channel = "C0001GEN";
    private const string Repo = "acme/widgets";
    private static readonly DateTimeOffset _now = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider _clock = new(_now);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static RecordPull Backfill(SourceCursor? after) => new(after, 100, NotBefore: _now.AddDays(-180), Before: _now.AddDays(-30));

    private ServiceProvider Services() => new ServiceCollection().AddSingleton<TimeProvider>(_clock).AddConnectorsFeatures().BuildServiceProvider();

    private ConnectorFeed GitHubFeed(FakeGitHubReader reader)
    {
        using var services = Services();
        var settings = GitHubSettings.Create([Repo], token: null, host: null, allowPrivateHost: false, commentCap: null).Value;
        var connector = services.GetRequiredService<IGitHubConnectorFactory>().Create(settings, reader, TimeSpan.FromDays(180));
        return new ConnectorFeed(SourceId.From("github"), connector, new ProfileRepoMap([Repo], [ContextRepoName.From("widgets")]).Place);
    }

    private ConnectorFeed SlackFeed(FakeSlackReader reader)
    {
        using var services = Services();
        var settings = SlackSettings.Create([new SlackChannelRef(Channel, "general")], token: null, "example").Value;
        var connector = services.GetRequiredService<ISlackConnectorFactory>().Create(settings, reader, TimeSpan.FromDays(180));
        return new ConnectorFeed(SourceId.From("slack"), connector, new SlackPlacement([Channel]).Place);
    }

    private static FakeSlackReader SlackWithTwoThreads(out string oldRoot, out string newRoot)
    {
        oldRoot = SlackTs.Format(_now.AddDays(-40));
        newRoot = SlackTs.Format(_now.AddDays(-5));
        return new FakeSlackReader(Channel)
            .Add(Channel, A.SlackMessage(oldRoot).WithText("Old rollout plan"))
            .Add(Channel, A.SlackMessage(SlackTs.Format(_now.AddDays(-40).AddMinutes(3))).InThread(oldRoot).WithText("Agreed"))
            .Add(Channel, A.SlackMessage(newRoot).WithText("New rollout plan"))
            .Add(Channel, A.SlackMessage(SlackTs.Format(_now.AddDays(-5).AddMinutes(3))).InThread(newRoot).WithText("Agreed too"));
    }

    [Fact]
    public async Task A_backfill_pull_reads_no_GitHub_pull_request_updated_at_or_after_Before()
    {
        var reader = new FakeGitHubReader()
            .Add(Repo, A.GitHubIssue(1).AsPullRequest().UpdatedAt(_now.AddDays(-40)))
            .Add(Repo, A.GitHubIssue(2).AsPullRequest().UpdatedAt(_now.AddDays(-5)));

        var page = (await GitHubFeed(reader).Pull(Backfill(after: null), Ct)).Value;

        Assert.Equal(["github:pr/acme/widgets/1"], page.Items.Select(static i => i.Key.Value), StringComparer.Ordinal);
        Assert.Contains("pull acme/widgets#1", reader.Reads, StringComparer.Ordinal);
        Assert.DoesNotContain(reader.Reads, static read => read.EndsWith("#2", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_backfill_pull_reads_no_Slack_thread_rooted_at_or_after_Before()
    {
        var reader = SlackWithTwoThreads(out var oldRoot, out var newRoot);

        _ = await SlackFeed(reader).Pull(Backfill(after: null), Ct);

        Assert.Contains($"replies {Channel} {oldRoot}", reader.Reads, StringComparer.Ordinal);
        Assert.DoesNotContain($"replies {Channel} {newRoot}", reader.Reads, StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_bounded_slack_backfill_ends()
    {
        var feed = SlackFeed(SlackWithTwoThreads(out _, out _));
        var page = (await feed.Pull(Backfill(after: null), Ct)).Value;

        var ended = false;
        for (var pull = 1; pull < 3 && !ended; pull++)
        {
            page = (await feed.Pull(Backfill(page.Next), Ct)).Value;
            ended = page.Items.Count == 0 && page.Removed.Count == 0;
        }

        Assert.True(ended, string.Create(CultureInfo.InvariantCulture, $"still returned {page.Items.Count} items and {page.Removed.Count} removed after 3 pulls"));
    }
}
