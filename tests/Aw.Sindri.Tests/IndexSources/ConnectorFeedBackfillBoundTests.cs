using System.Globalization;
using Aw.Connectors.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Sync;
using Aw.Kernel;
using Aw.Sindri.Host.IndexSources;

namespace Aw.Sindri.Tests.IndexSources;

/// <summary>The connectors take no time bound, so the feed applies Backfill's upper bound itself (#1512): a record updated at or after <see cref="RecordPull.Before"/> is dropped.</summary>
[Trait("Lane", "unit")]
public sealed class ConnectorFeedBackfillBoundTests
{
    private static readonly DateTimeOffset _t = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ConnectorItem Item(int number, DateTimeOffset updated) =>
        new(string.Create(CultureInfo.InvariantCulture, $"github:pr/acme/widgets/{number}"), "pr", "Fix save", "Fix save\n\nbody", "octocat", string.Create(CultureInfo.InvariantCulture, $"https://github.com/acme/widgets/pull/{number}"), "merged", "merged", _t, updated, ClosedAt: null, "v1", [], "acme/widgets");

    [Fact]
    public async Task A_record_updated_at_or_after_Before_is_not_returned()
    {
        var connector = new Stub(Item(1, _t.AddDays(-10)), Item(2, _t.AddDays(-30)), Item(3, _t.AddDays(-40)));
        var feed = new ConnectorFeed(SourceId.From("github"), connector, new ProfileRepoMap(["acme/widgets"], [ContextRepoName.From("widgets")]).Place);

        var page = (await feed.Pull(new RecordPull(After: null, 50, NotBefore: _t.AddDays(-180), Before: _t.AddDays(-30)), Ct)).Value;

        Assert.Equal(["github:pr/acme/widgets/3"], page.Items.Select(static i => i.Key.Value), StringComparer.Ordinal);
    }

    private sealed class Stub(params ConnectorItem[] items) : IConnector
    {
        public ConnectorDescription Describe() => new("github", "GitHub");

        public ValueTask<Result<ConnectorPage>> Pull(Connectors.Core.SourceCursor? after, int limit, DateTimeOffset? before, CancellationToken cancellationToken) =>
            ValueTask.FromResult<Result<ConnectorPage>>(new ConnectorPage(items, [], new Connectors.Core.SourceCursor("next-cursor"), CursorReset: false));

        public ValueTask<Result<IReadOnlyDictionary<string, string>>> Versions(IReadOnlyList<string> keys, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<Result<Unit>> Probe(CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
