using System.Globalization;
using Aw.Context.Core;
using Aw.Context.Core.Records;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Sync;
using Aw.Context.Features.SyncSources;
using Aw.Context.Testing.Engine;
using Aw.Context.Testing.Records;
using Aw.Context.Tests.Map;
using Aw.Kernel;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Context.Tests.Sync;

/// <summary>A named tick pulls only that source's feed, under its own lock, beside any other source's tick (#1597 part A).</summary>
[Trait("Lane", "unit")]
public sealed class IndexSyncNamedTests
{
    private static readonly SourceId _slack = SourceId.From("slack");
    private readonly FakeRecordStore _store = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static RecordItem Item(SourceId source, int n) => new(
        RecordKey.From($"{source.Value}:item/{n.ToString(CultureInfo.InvariantCulture)}"), RecordKind.Issue, $"Title {n.ToString(CultureInfo.InvariantCulture)}", $"Words of {source.Value} item {n.ToString(CultureInfo.InvariantCulture)}", Author: null, Url: null, RecordState.Open, StateRaw: null,
        CreatedAt: null, RecordSamples.Now.AddDays(-1), ClosedAt: null, SourceVersion.From("v1"), Links: [], Repo: null, Audience: Audience(source));

    // why: a github record's audience is its repo, a linear one its team, a slack one its channel
    private static AudienceKey Audience(SourceId source) => AudienceKey.From(source.Value.StartsWith("github", StringComparison.Ordinal) ? "repo:acme/app" : source.Value.StartsWith("slack", StringComparison.Ordinal) ? "slack:C1" : "linear:team-a");

    private static FakeRecordFeed Plain(SourceId source) => new(source, [Item(source, 1), Item(source, 2)]);

    private (RecordFeedSync Feeds, IndexSync Index) Rig(params RecordFeed[] feeds)
    {
        var sync = new RecordFeedSync(new FakeRecordFeedCatalog(feeds), _store, new FakeTimeProvider());
        return (sync, new IndexSync(sync, _store, new FakeScrubberFactory()));
    }

    private async Task<int> Records(SourceId source) => (await _store.Count(source, Ct)).Value.Records;

    [Fact]
    public async Task A_named_tick_pulls_only_that_source()
    {
        var github = new CountingFeed(Plain(RecordSamples.Github));
        var slack = new CountingFeed(Plain(_slack));
        var (_, index) = Rig(github, Plain(RecordSamples.Linear), slack);

        var report = await index.Tick(MapWorld.Human, RecordSamples.Linear, Ct);

        Assert.True(report.IsSuccess, report.IsError ? report.Error.Message : null);
        var row = Assert.Single(report.Value.Sources);
        Assert.Equal(RecordSamples.Linear, row.Source);
        Assert.Equal(2, await Records(RecordSamples.Linear));
        Assert.Equal(0, github.Pulls);
        Assert.Equal(0, slack.Pulls);
        Assert.Equal(0, await Records(RecordSamples.Github));
        Assert.Equal(0, await Records(_slack));
    }

    [Fact]
    public async Task A_named_tick_of_a_source_that_is_not_a_feed_is_SyncSourceUnknown()
    {
        var github = new CountingFeed(Plain(RecordSamples.Github));
        var linear = new CountingFeed(Plain(RecordSamples.Linear));
        var (_, index) = Rig(github, linear);

        var report = await index.Tick(MapWorld.Human, SourceId.From("jira"), Ct);

        Assert.True(report.IsError);
        Assert.Equal(ContextErrors.SyncSourceUnknown.Code, report.Error.Code);
        Assert.Equal(0, github.Pulls);
        Assert.Equal(0, linear.Pulls);
    }

    [Fact]
    public async Task A_named_tick_runs_while_another_source_is_syncing()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var (_, index) = Rig(new HeldFeed(Plain(RecordSamples.Github), release.Task), Plain(_slack));
        Task<Result<SyncReport>>? held = null;
        try
        {
            held = index.Tick(MapWorld.Human, RecordSamples.Github, Ct).AsTask();

            var report = await index.Tick(MapWorld.Human, _slack, Ct);

            Assert.True(report.IsSuccess, report.IsError ? report.Error.Message : null);
            Assert.Equal(_slack, Assert.Single(report.Value.Sources).Source);
            Assert.Equal(2, await Records(_slack));
            Assert.Equal(0, await Records(RecordSamples.Github));   // github's pull is still held
        }
        finally
        {
            _ = release.TrySetResult();   // always let the held pull go, so a failing run never leaves the tick hanging
        }

        if (held is not null)
        {
            _ = await held;
        }
    }

    /// <summary>A feed that counts its pulls and answers from <paramref name="inner"/>.</summary>
    private sealed class CountingFeed(RecordFeed inner) : RecordFeed(inner.Source)
    {
        private int _pulls;

        public int Pulls => Volatile.Read(ref _pulls);

        public override ValueTask<Result<RecordPage>> Pull(RecordPull pull, CancellationToken cancellationToken)
        {
            _ = Interlocked.Increment(ref _pulls);
            return inner.Pull(pull, cancellationToken);
        }
    }

    /// <summary>A feed whose pull waits for <paramref name="gate"/> before answering from <paramref name="inner"/>.</summary>
    private sealed class HeldFeed(RecordFeed inner, Task gate) : RecordFeed(inner.Source)
    {
        public override async ValueTask<Result<RecordPage>> Pull(RecordPull pull, CancellationToken cancellationToken)
        {
            await gate.WaitAsync(cancellationToken);
            return await inner.Pull(pull, cancellationToken);
        }
    }
}
