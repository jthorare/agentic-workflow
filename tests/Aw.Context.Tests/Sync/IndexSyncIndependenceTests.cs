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

/// <summary>One slow or busy source never stalls the others: each source's feed ticks on its own (#1597).</summary>
[Trait("Lane", "unit")]
public sealed class IndexSyncIndependenceTests
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
    public async Task Linear_and_Slack_commit_while_GitHub_pull_is_held()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var (_, index) = Rig(new HeldFeed(Plain(RecordSamples.Github), release.Task), Plain(RecordSamples.Linear), Plain(_slack));
        Task<Result<SyncReport>>? tick = null;
        var othersCommitted = false;
        try
        {
            tick = index.Tick(MapWorld.Human, Ct).AsTask();
            for (var waited = 0; waited < 250 && !othersCommitted; waited++)
            {
                othersCommitted = await Records(RecordSamples.Linear) == 2 && await Records(_slack) == 2;
                if (!othersCommitted)
                {
                    await Task.Delay(20, Ct);
                }
            }

            Assert.Equal(0, await Records(RecordSamples.Github));   // github's pull is still held
        }
        finally
        {
            _ = release.TrySetResult();   // always let the held pull go, so a failing run never leaves the tick hanging
        }

        var report = await tick;

        Assert.True(othersCommitted, "linear and slack never committed while the GitHub pull was held");
        Assert.True(report.IsSuccess, report.IsError ? report.Error.Message : null);
        Assert.Equal(2, await Records(RecordSamples.Github));
    }

    [Fact]
    public async Task A_source_already_syncing_is_its_own_row_and_the_others_still_tick()
    {
        var (feeds, index) = Rig(Plain(RecordSamples.Github), Plain(RecordSamples.Linear), Plain(_slack));
        Result<SyncReport>? report = null;

        var held = await feeds.Locked(RecordSamples.Linear, async () =>
        {
            report = await index.Tick(MapWorld.Human, Ct);
            return new SourceSync(RecordSamples.Linear, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, Code: null);
        });

        Assert.True(held.IsSuccess);
        Assert.NotNull(report);
        Assert.True(report.IsSuccess, report.IsError ? report.Error.Message : null);
        var rows = report.Value.Sources.ToDictionary(r => r.Source.Value, StringComparer.Ordinal);
        Assert.Equal(ContextErrors.SyncRunning.Code, rows["linear"].Code);
        Assert.Null(rows["github"].Code);
        Assert.Null(rows["slack"].Code);
        Assert.Equal(2, await Records(RecordSamples.Github));
        Assert.Equal(2, await Records(_slack));
        Assert.Equal(0, await Records(RecordSamples.Linear));
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
