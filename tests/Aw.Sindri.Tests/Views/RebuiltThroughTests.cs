using System.Globalization;
using Aw.Kernel.Events;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Host.Server;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Server;

namespace Aw.Sindri.Tests.Views;

/// <summary>#2054: the projector reads the newest 'views.rebuilt' from the ledger and compares it with the sequence its views.db has already covered.</summary>
public sealed class RebuiltThroughTests
{
    private readonly SliceHarness _h = new();
    private readonly InMemoryViewStore _memory = new(SliceHarness.Json);
    private readonly FlakyViewStore _views;
    private readonly ToggleProjection _projection = new();

    public RebuiltThroughTests()
    {
        _views = new FlakyViewStore(_memory);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public async Task A_store_from_before_the_rebuild_record_rebuilds_once_and_then_not_again()
    {
        await Append(ToggleProjection.Put, 3);
        await Append(EventTypes.ViewsRebuilt, 1);
        await OpenWithoutRecord();
        var first = new ListLogger<Projector>();

        Assert.Equal(ProjectorPass.CaughtUp, await Projector(out _, first).CatchUp(Token));

        Assert.Contains(first.Lines, l => l.Contains("Views rebuilding", StringComparison.Ordinal));
        Assert.Equal(4L, (await _memory.State(Token)).Value?.RebuiltThrough);
        Assert.Equal(3, _projection.Calls);   // reset, then replayed from cursor 0

        var second = new ListLogger<Projector>();
        _ = await Projector(out _, second).CatchUp(Token);   // the next start

        Assert.DoesNotContain(second.Lines, l => l.Contains("Views rebuilding", StringComparison.Ordinal));
        Assert.Equal(3, _projection.Calls);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_store_from_before_the_rebuild_record_with_no_rebuild_in_the_ledger_records_zero_and_does_not_rebuild_again()
    {
        await Append(ToggleProjection.Put, 3);
        await OpenWithoutRecord();

        _ = await Projector(out _).CatchUp(Token);
        Assert.Equal(0L, (await _memory.State(Token)).Value?.RebuiltThrough);
        Assert.Equal(3, _projection.Calls);

        var second = new ListLogger<Projector>();
        _ = await Projector(out _, second).CatchUp(Token);

        Assert.DoesNotContain(second.Lines, l => l.Contains("Views rebuilding", StringComparison.Ordinal));
        Assert.Equal(3, _projection.Calls);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_rebuild_that_ran_is_not_repeated_after_a_restart()
    {
        await Append(ToggleProjection.Put, 3);
        var first = Projector(out var progress);
        _ = await first.CatchUp(Token);
        await Append(EventTypes.ViewsRebuilt, 1);
        progress.RequestRebuild();
        _ = await first.CatchUp(Token);
        Assert.Equal(6, _projection.Calls);   // 3 + the 3 replayed by the rebuild

        _ = await Projector(out _).CatchUp(Token);   // serve restarts with a fresh in-memory request flag

        Assert.Equal(6, _projection.Calls);
        Assert.Equal(4L, (await _memory.State(Token)).Value?.RebuiltThrough);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_request_whose_reset_failed_rebuilds_after_a_restart()
    {
        await Append(ToggleProjection.Put, 3);
        var first = Projector(out var progress);
        _ = await first.CatchUp(Token);
        await Append(EventTypes.ViewsRebuilt, 1);
        progress.RequestRebuild();
        _views.FailResets = 1;
        Assert.Equal(ProjectorPass.Failed, await first.CatchUp(Token));
        Assert.Equal(3, _projection.Calls);

        var pass = await Projector(out _).CatchUp(Token);

        Assert.Equal(ProjectorPass.CaughtUp, pass);
        Assert.Equal(6, _projection.Calls);
        Assert.Equal(4L, (await _memory.State(Token)).Value?.RebuiltThrough);
    }

    private async Task OpenWithoutRecord()
    {
        _ = await _views.Open(ViewSchema.Version, Token);   // a views.db from before the column: it exists (not fresh), same schema, no rebuilt_through
        _ = await _views.Open(ViewSchema.Version, Token);
    }

    private Projector Projector(out ProjectorProgress progress, ListLogger<Projector>? log = null)
    {
        progress = new ProjectorProgress(_h.Clock);
        return new Projector(_h.Store, _views, [_projection], progress, _h.Callers, _h.Clock, log ?? new ListLogger<Projector>());
    }

    private async Task Append(string type, int count)
    {
        var grant = (await _h.Store.Read(Token)).Value is null ? await _h.HoldLease() : _h.Held.Current!;   // why: Read found the held lease
        for (var i = 0; i < count; i++)
        {
            var key = string.Create(CultureInfo.InvariantCulture, $"k{_h.Store.Events.Count + 1:D4}");
            var context = EventContext.Create("host:sindri", "local", 0, _h.Ids.NewId()).Value;
            _ = await _h.Store.Append(new LedgerEntry(context.Envelope(type, """{"n":1}""", _h.Clock, _h.Ids), key, Item: null), grant, Token);
        }
    }
}
