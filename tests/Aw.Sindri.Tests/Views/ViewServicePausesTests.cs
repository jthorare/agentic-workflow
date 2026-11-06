using Aw.Kernel.Ids;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Infrastructure.Views;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Ledger;

namespace Aw.Sindri.Tests.Views;

/// <summary>The paused services as the scheduler reads them from views.db (API P6 unit D): a pause is there until a resume, and it survives a restart.</summary>
[Lane(TestLane.Integration)]
public sealed class ViewServicePausesTests : IDisposable
{
    private static readonly ServiceName _heartbeat = ServiceName.From("heartbeat");
    private static readonly ServiceName _other = ServiceName.From("other");
    private readonly TempHome _home = new();
    private readonly SliceHarness _h = new();
    private long _sequence;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _home.Dispose();

    private string Dir => Path.Combine(_home.Path, "home");

    private async Task Fold<TPayload>(SqliteViews views, string type, TPayload payload)
    {
        _ = (await views.Store.Open(ViewSchema.Version, Token)).Value;
        var applied = (await views.Store.Apply([Recorded.Of(_h.Events, type, payload, ++_sequence)], [new ServicesProjection(_h.Events)], _h.Clock.GetUtcNow(), Token)).Value;
        Assert.Null(applied.Stall);
    }

    private Task Pause(SqliteViews views, ServiceName service) => Fold(views, EventTypes.ServicePaused, new ServicePaused(_h.Ids.New<CommandId>(), service));

    private Task Resume(SqliteViews views, ServiceName service) => Fold(views, EventTypes.ServiceResumed, new ServiceResumed(_h.Ids.New<CommandId>(), service));

    private static async Task<IReadOnlyList<string>> Paused(SqliteViews views) =>
        [.. (await new ViewServicePauses(views.Reader, FirstPass.Progress(TimeProvider.System)).Paused(Token)).Value.Select(static s => s.Value).Order(StringComparer.Ordinal)];

    [Fact]
    public async Task Nothing_is_paused_before_any_pause_and_with_no_views_db()
    {
        using var views = new SqliteViews(Dir);

        Assert.Empty(await Paused(views));
    }

    [Theory]
    [InlineData(true, false, "SND-API-010")]
    [InlineData(false, true, "SND-VIEW-001")]
    public async Task While_the_views_rebuild_or_are_stalled_the_read_is_an_error_not_an_empty_set(bool rebuilding, bool stalled, string code)
    {
        using var views = new SqliteViews(Dir);
        await Pause(views, _heartbeat);
        var status = FirstPass.Progress(_h.Clock);
        status.Publish(status.Current with { Rebuilding = rebuilding, Stalled = stalled, StalledAt = stalled ? 3 : null, StalledType = "service.paused" });

        var read = await new ViewServicePauses(views.Reader, status).Paused(Token);

        Assert.True(read.IsError);
        Expect.Equal(code, read.Error.Code);
    }

    [Fact]
    public async Task Before_the_first_pass_the_read_is_an_error_not_the_stored_rows()
    {
        using var views = new SqliteViews(Dir);
        await Pause(views, _heartbeat);   // views.db holds the last run's pause

        var read = await new ViewServicePauses(views.Reader, new ProjectorProgress(_h.Clock)).Paused(Token);

        Assert.True(read.IsError);
        Expect.Equal("SND-API-010", read.Error.Code);
        Assert.Contains("still loading", read.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_paused_service_is_listed_until_it_is_resumed()
    {
        using var views = new SqliteViews(Dir);
        await Pause(views, _heartbeat);
        await Pause(views, _other);
        Expect.Sequence(["heartbeat", "other"], await Paused(views));

        await Resume(views, _heartbeat);

        Expect.Sequence(["other"], await Paused(views));
    }

    [Fact]
    public async Task Pause_survives_a_restart()
    {
        using (var before = new SqliteViews(Dir))
        {
            await Pause(before, _heartbeat);
        }

        using var after = new SqliteViews(Dir);   // a new process opens the same views.db

        Expect.Sequence(["heartbeat"], await Paused(after));
    }
}
