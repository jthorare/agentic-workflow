using Aw.Connectors.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Sync;
using Aw.Kernel;
using Aw.Sindri.Host.IndexSources;

namespace Aw.Sindri.Tests.IndexSources;

/// <summary>PR review page spec §5.6: a pull request's reviewers, head and reviewed commits travel from the connector's item onto the record the store keeps.</summary>
public sealed class ConnectorFeedPullFactsTests
{
    private static readonly DateTimeOffset _t = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<RecordItem> Read(ConnectorItem item)
    {
        var feed = new ConnectorFeed(SourceId.From("github"), new OnePage(item), new ProfileRepoMap(["acme/widgets"], [ContextRepoName.From("widgets")]).Place);
        return Assert.Single((await feed.Pull(new RecordPull(After: null, Limit: 50, NotBefore: null), Ct)).Value.Items);
    }

    private static ConnectorItem Pr(ConnectorPullFacts? pull) =>
        new("github:pr/acme/widgets/12", "pr", "Fix save", "Fix save", "octocat", "https://github.com/acme/widgets/pull/12", "open", "open", _t, _t.AddHours(1), ClosedAt: null, "v1", [], "acme/widgets", pull);

    [Fact, Trait("Lane", "unit")]
    public async Task The_requested_reviewers_head_and_each_reviewed_commit_with_its_count_reach_the_record()
    {
        var item = await Read(Pr(new ConnectorPullFacts(["bob"], "headC", [new ConnectorReview("ada", "commitA", 2), new ConnectorReview("cy", "gone", NewCommits: null)])));

        var pull = Assert.IsType<PullFacts>(item.Pull);
        Assert.Equal(["bob"], pull.RequestedReviewers);
        Assert.Equal("headC", pull.HeadSha);
        Assert.Equal([new ReviewedCommit("ada", "commitA", 2), new ReviewedCommit("cy", "gone", NewCommits: null)], pull.Reviews);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_record_with_no_pull_facts_has_none()
    {
        var item = await Read(Pr(pull: null));

        Assert.Null(item.Pull);
    }

    private sealed class OnePage(ConnectorItem item) : IConnector
    {
        public ConnectorDescription Describe() => new("github", "GitHub");

        public ValueTask<Result<ConnectorPage>> Pull(Connectors.Core.SourceCursor? after, int limit, DateTimeOffset? before, CancellationToken cancellationToken) =>
            ValueTask.FromResult<Result<ConnectorPage>>(new ConnectorPage([item], [], new Connectors.Core.SourceCursor("next"), CursorReset: false));

        public ValueTask<Result<IReadOnlyDictionary<string, string>>> Versions(IReadOnlyList<string> keys, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<Unit>> Probe(CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
