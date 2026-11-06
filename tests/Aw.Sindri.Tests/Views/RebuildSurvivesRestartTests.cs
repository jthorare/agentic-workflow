using System.Globalization;
using Aw.Kernel.Events;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Host.Server;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Ledger;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Ledger;
using Aw.Sindri.Tests.Server;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Views;

/// <summary>#2054: an accepted 'views rebuild' lives only in <see cref="ProjectorProgress"/>'s memory, so a restart before the reset commits loses it.</summary>
public sealed class RebuildSurvivesRestartTests : IDisposable
{
    private readonly TempHome _home = new();
    private readonly FakeTimeProvider _clock = new(KnownInstants.Epoch);
    private readonly SequentialIdGenerator _ids = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _home.Dispose();

    /// <summary>
    /// The observable is the fold count: a rebuild replays the three 'fixture.put' events through the projection, a pass over views that are
    /// already at the head folds none ('views.rebuilt' itself is not handled by it).
    /// </summary>
    [Fact, Trait("Lane", "integration")]
    public async Task An_accepted_rebuild_runs_after_a_restart_before_its_reset()
    {
        // serve #1 projects three events, then accepts a rebuild: RebuildViewsService appends views.rebuilt and calls RequestRebuild
        var stores = SqliteStores.Create(_home.Path, _clock, new FakeProcessProbe());
        var grant = (await stores.Lease.Acquire(new FakeProcessProbe().Self(), Token)).Value;
        await Append(stores, grant, ToggleProjection.Put, """{"n":1}""", 3);
        var first = Projector(new ToggleProjection(), out var firstProgress);
        Assert.Equal(ProjectorPass.CaughtUp, await first.CatchUp(Token));

        await Append(stores, grant, EventTypes.ViewsRebuilt, """{"commandId":"cmd_1","cursorBefore":3,"eventsReplayed":3}""", 1);
        firstProgress.RequestRebuild();
        first.Dispose();   // serve dies before the next pass's Reset commits

        // serve #2 starts over the same ledger and views.db with a fresh in-memory progress
        var projection = new ToggleProjection();
        var second = Projector(projection, out _);
        Assert.Equal(ProjectorPass.CaughtUp, await second.CatchUp(Token));

        Assert.Equal(3, projection.Calls);   // the accepted rebuild replayed the events; today the request is gone and nothing is folded
    }

    private Projector Projector(ToggleProjection projection, out ProjectorProgress progress)
    {
        var views = new SqliteViews(_home.Path);
        var ledger = new LedgerDatabase(new SindriSettings(_home.Path, Path.Combine(_home.Path, "profile.yaml")), _clock);
        progress = new ProjectorProgress(_clock);
        return new Projector(new SqliteLedgerReader(ledger), views.Store, [projection], progress, new AsyncLocalCallerContextAccessor(_ids), _clock,
            new ListLogger<Projector>());
    }

    private async Task Append(SindriStores stores, LeaseGrant grant, string type, string payload, int count)
    {
        var head = (await stores.Ledger.Head(Token)).Value;
        for (var i = 1; i <= count; i++)
        {
            var context = EventContext.Create("host:sindri", "local", 0, _ids.NewId()).Value;
            _ = await stores.Ledger.Append(new LedgerEntry(context.Envelope(type, payload, _clock, _ids), string.Create(CultureInfo.InvariantCulture, $"k{head + i:D4}"), Item: null), grant, Token);
        }
    }
}
