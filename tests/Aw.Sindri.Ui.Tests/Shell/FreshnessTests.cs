using System.Net;
using System.Text;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>
/// U1's freshness (UI spec U10, §4.1, §3.2): the pill from the status's health (E-14) and its asOf, the stalled and
/// rebuilding banners from the services view, the catalog read on connect, and every view re-read on an event.
/// </summary>
[Lane(TestLane.Unit)]
public sealed partial class FreshnessTests : IDisposable
{
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new();
    private readonly FakeTimeProvider _clock = new(A.Noon);
    private readonly CancellationTokenSource _stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    private readonly HealthViewModel _vm;
    private readonly InlineUiContext _ui = new();
    private Task _run = Task.CompletedTask;

    public FreshnessTests()
    {
        _records.Write(A.Record());
        _vm = new HealthViewModel(_records, _server, _clock, new FakeClipboard());
    }

    public void Dispose()
    {
        _stop.Cancel();
        Assert.True(_run.Wait(TimeSpan.FromSeconds(2), CancellationToken.None), "Run ended when cancelled");
        _stop.Dispose();
        _ui.Dispose();
    }

    private async Task Connected(Func<bool>? also = null)
    {
        _run = _vm.Run(_stop.Token);
        for (var i = 0; i < 200 && !(_vm.State == ShellState.Normal && (also?.Invoke() ?? true)); i++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.Equal(ShellState.Normal, _vm.State);
        Assert.True(also?.Invoke() ?? true, "the awaited state was reached within 2 s");
    }

    private static StatusView Status(AsOfView views, HealthView health) => A.Status() with { ApiVersion = 1, Views = views, Health = health };

    // E-14: the pill renders health.word and names each problem as served; "updated Ns ago" counts from projectedAt.
    [Fact]
    public async Task The_pill_says_the_health_word_its_problems_and_how_long_since_the_views_moved()
    {
        _server.Current = Status(new AsOfView(41, 41, A.Noon.AddSeconds(-12), Rebuilding: false, Stalled: false),
            new HealthView("degraded", [new HealthProblemView("SND-SCHED-001", "index failed")]));

        await Connected(() => _vm.Pill.Text == "Degraded · updated 12s ago · index failed");

        Assert.Equal(("Degraded", "updated 12s ago", "index failed"), (_vm.Pill.Word, _vm.Pill.Age, _vm.Pill.Problems));
        Assert.Equal("Degraded · updated 12s ago · index failed", _vm.Pill.Text);
        Assert.Equal((StateGlyph.Down, false), (_vm.Pill.Glyph, _vm.Pill.IsHealthy));
    }

    [Fact]
    public async Task Healthy_is_the_ok_glyph_and_no_problems()
    {
        _server.Current = Status(new AsOfView(41, 41, A.Noon, Rebuilding: false, Stalled: false), new HealthView("healthy", []));

        await Connected(() => _vm.Pill.Text == "Healthy · updated 0s ago");

        Assert.Equal(("Healthy · updated 0s ago", StateGlyph.Ok, true), (_vm.Pill.Text, _vm.Pill.Glyph, _vm.Pill.IsHealthy));
    }

    // Before API P2 the status has no health or views: U0's pill, its age from the last event or ping.
    [Fact]
    public async Task Without_health_the_pill_says_running_as_before()
    {
        _server.ServiceView = new RouteAnswer(HttpStatusCode.NotFound, "{}");

        await Connected(() => _vm.Pill.Text == "Running · updated 0s ago");

        Assert.Equal(("Running · updated 0s ago", false), (_vm.Pill.Text, _vm.Banner.IsShown));
    }

    // §4.1 Stalled: the view's own sentence and Rebuild views, the catalog's row (ready once P2 ships it).
    [Fact]
    public async Task Stalled_views_show_the_views_sentence_and_rebuild_views()
    {
        _server.ServiceView = RouteAnswer.SpecShaped("services", "stalled");

        await Connected(() => _vm.Banner.IsShown);

        Assert.Equal((true, false, "Views are stalled at 42: run `sindri views rebuild`"), (_vm.Banner.IsStalled, _vm.Banner.IsRebuilding, _vm.Banner.Text));
        Assert.Equal(("RebuildViews", true), (_vm.Banner.Rebuild?.Action, _vm.Banner.Rebuild?.CanRun));
    }

    // A catalog that still has RebuildViews pending (P1's, before P2): the banner's control is disabled with its focusable line.
    [Fact]
    public async Task With_rebuild_still_pending_the_banners_control_is_drawn_pending()
    {
        var p2 = RouteAnswer.Api("commands", "catalog");
        _server.Catalog = p2 with { Body = p2.Body.Replace("\"cli\": \"sindri views rebuild\",\n      \"ui\": \"ready\"", "\"cli\": \"sindri views rebuild\",\n      \"ui\": \"pending:P2\"", StringComparison.Ordinal) };
        _server.ServiceView = RouteAnswer.SpecShaped("services", "stalled");

        await Connected(() => _vm.Banner.IsShown);

        Assert.Equal((false, "arrives in P2 · sindri views rebuild"), (_vm.Banner.Rebuild?.CanRun, _vm.Banner.Rebuild?.PendingLine));
    }

    [Fact]
    public async Task Rebuilding_views_show_a_progress_banner_without_rebuild()
    {
        _server.ServiceView = RouteAnswer.SpecShaped("services", "rebuilding");

        await Connected(() => _vm.Banner.IsShown);

        Assert.Equal((false, true, "Rebuilding views (cursor 12 of 42)…", false), (_vm.Banner.IsStalled, _vm.Banner.IsRebuilding, _vm.Banner.Text, _vm.Banner.HasRebuild));
    }

    // §9.1: the catalog is read on connect; Health lists its pending rows by phase (§4.8).
    [Fact]
    public async Task The_catalog_is_read_on_connect_and_its_pending_rows_are_grouped_by_phase()
    {
        await Connected(() => _vm.Catalog.Controls.Count > 0);

        Assert.Equal(1, _server.CatalogCalls);
        var rows = System.Text.Json.JsonDocument.Parse(RouteAnswer.Api("commands", "catalog").Body).RootElement.GetProperty("rows").EnumerateArray()
            .Select(r => r.GetProperty("ui").GetString()!).ToList();   // why: read from the catalog golden, so the next lane's rows don't break the counts
        var pending = rows.Where(u => u.StartsWith("pending:", StringComparison.Ordinal)).Select(u => u["pending:".Length..]).ToList();
        Assert.Equal(rows.Count, _vm.Catalog.Controls.Count);
        Assert.Equal(pending.Count == 1 ? "1 action arrives in later phases" : $"{pending.Count} actions arrive in later phases", _vm.Catalog.PendingSummary);
        Assert.Equal(pending.Distinct(StringComparer.Ordinal).Select(stage => $"arrives in {stage}"), _vm.Catalog.Pending.Select(p => p.Heading), StringComparer.Ordinal);
        Assert.Equal(rows.Count(u => u.StartsWith("cli-only", StringComparison.Ordinal)), _vm.Catalog.CliOnly.Count);
    }

    // §9.2: an event re-reads every view the app shows (the status and the services view); its payload is never applied.
    [Fact]
    public async Task An_event_re_reads_the_status_and_the_services_view()
    {
        await Connected(() => _server.ServicesCalls > 0);
        var (status, services) = (_server.StatusCalls, _server.ServicesCalls);
        _server.ServiceView = RouteAnswer.SpecShaped("services", "stalled");

        _server.Push(A.Event(1, "views.stalled"));
        await Until(() => _vm.Banner.IsShown);

        Assert.True(_vm.Banner.IsStalled);
        Assert.True(_server.StatusCalls > status && _server.ServicesCalls > services);
    }
    // §9.2: a burst re-reads each view at most once per 250 ms. The first event re-reads at once; the events in the next
    // 250 ms collapse into one more re-read when the window ends.
    [Fact]
    public async Task A_burst_of_events_re_reads_each_view_once_per_250_ms()
    {
        await Connected(() => _server.ServicesCalls > 0);
        var (status, services) = (_server.StatusCalls, _server.ServicesCalls);

        _server.Push(A.Event(1, "service.ran"));
        await Until(() => _server.ServicesCalls == services + 1);
        _server.Push(A.Event(2, "service.ran"), A.Event(3, "service.ran"), A.Event(4, "service.ran"));
        await Task.Delay(150, TestContext.Current.CancellationToken);

        Assert.Equal((status + 1, services + 1), (_server.StatusCalls, _server.ServicesCalls));

        _clock.Advance(HealthViewModel.RefreshEvery);
        await Until(() => _server.ServicesCalls == services + 2);
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.Equal((status + 2, services + 2), (_server.StatusCalls, _server.ServicesCalls));
    }

    // F2 (arch-0, corr-0): a rebuild is accepted and the next status read is "views behind" (503 SND-API-011). That is not
    // trouble with Sindri: the shell stays Normal with the banner and the last data, and reads again.
    [Fact]
    public async Task Views_behind_keeps_the_shell_normal_and_the_last_data_and_reads_again()
    {
        _server.ServiceView = RouteAnswer.SpecShaped("services", "stalled");
        await Connected(() => _vm.Banner.IsShown);
        var status = _server.StatusCalls;
        _server.StatusError = new Error("SND-API-011", ErrorKind.Unavailable, "The views have not reached sequence 43 (fix: retry shortly)");

        _server.Push(A.Event(1, "views.rebuild.started"));
        await Until(() => _server.StatusCalls > status);
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.Equal((ShellState.Normal, true), (_vm.State, _vm.Banner.IsShown));
        Assert.Equal("Views are stalled at 42: run `sindri views rebuild`", _vm.Banner.Text);

        _server.StatusError = null;
        _server.Current = Status(new AsOfView(42, 42, A.Noon, Rebuilding: false, Stalled: false), new HealthView("healthy", []));
        _server.ServiceView = RouteAnswer.SpecShaped("services", "fixture");
        await Until(() => !_vm.Banner.IsShown && _vm.Pill.Word == "Healthy", _clock);

        Assert.Equal(("Healthy", ShellState.Normal), (_vm.Pill.Word, _vm.State));
    }

    // F3 (corr-1): after an event the status can still show the projection behind the ledger; with no other event the
    // app keeps reading on the 250 ms throttle until it catches up, so the pill and banner follow.
    [Fact]
    public async Task A_projection_still_behind_the_ledger_is_read_again_until_it_catches_up()
    {
        _server.Current = Status(new AsOfView(10, 42, A.Noon, Rebuilding: false, Stalled: false), new HealthView("healthy", []));
        await Connected(() => _server.ServicesCalls > 0);
        _server.Push(A.Event(1, "service.ran"));
        await Until(() => _server.StatusCalls > 1);

        var before = _server.StatusCalls;
        await Until(() => _server.StatusCalls >= before + 2, _clock);

        _server.Current = Status(new AsOfView(42, 42, A.Noon, Rebuilding: false, Stalled: false), new HealthView("degraded", [new HealthProblemView("SND-SCHED-001", "index failed")]));
        _server.ServiceView = RouteAnswer.SpecShaped("services", "rebuilding");
        await Until(() => _vm.Pill.Word == "Degraded", _clock);
        var settled = _server.StatusCalls;
        _clock.Advance(HealthViewModel.RefreshEvery);
        await Task.Delay(50, TestContext.Current.CancellationToken);
        _clock.Advance(HealthViewModel.RefreshEvery);
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.Equal("Degraded", _vm.Pill.Word);
        Assert.True(_server.StatusCalls <= settled + 1, "caught up: no more polling");
    }

    // F3: a rebuild in progress is polled the same way; a stalled projection is not (it won't move by itself).
    [Fact]
    public async Task A_rebuild_in_progress_is_polled_and_a_stalled_projection_is_not()
    {
        _server.Current = Status(new AsOfView(12, 42, A.Noon, Rebuilding: true, Stalled: false), new HealthView("healthy", []));
        await Connected(() => _server.ServicesCalls > 0);
        _server.Push(A.Event(1, "views.rebuild.progress"));
        await Until(() => _server.StatusCalls > 1);
        var before = _server.StatusCalls;
        await Until(() => _server.StatusCalls >= before + 2, _clock);

        _server.Current = Status(new AsOfView(12, 42, A.Noon, Rebuilding: false, Stalled: true), new HealthView("degraded", []));
        await Until(() => _vm.Pill.Word == "Degraded", _clock);
        var settled = _server.StatusCalls;
        for (var i = 0; i < 3; i++)
        {
            _clock.Advance(HealthViewModel.RefreshEvery);
            await Task.Delay(30, TestContext.Current.CancellationToken);
        }

        Assert.True(_server.StatusCalls <= settled + 1, "stalled: not polled");
    }

    private static async Task Until(Func<bool> condition, FakeTimeProvider clock)
    {
        for (var i = 0; i < 300 && !condition(); i++)
        {
            clock.Advance(HealthViewModel.RefreshEvery);
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.True(condition());
    }

    // F14 (test-2, corr-6): the banner is the last thing Sindri said about the views. A services read that fails for any
    // reason but "no such route" (before P2) leaves it as it was; the status still moves the pill.
    [Fact]
    public async Task A_stalled_banner_stays_when_the_services_read_then_fails()
    {
        _server.ServiceView = RouteAnswer.SpecShaped("services", "stalled");
        await Connected(() => _vm.Banner.IsShown);
        var services = _server.ServicesCalls;
        _server.ServiceView = RouteAnswer.Problem(HttpStatusCode.ServiceUnavailable, "SND-API-009", "Sindri is busy (fix: retry)");

        _server.Push(A.Event(1, "service.ran"));
        await Until(() => _server.ServicesCalls > services);
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.Equal((ShellState.Normal, true, true), (_vm.State, _vm.Banner.IsStalled, _vm.Banner.HasRebuild));
    }

    [Fact]
    public async Task A_stalled_banner_clears_when_the_services_view_says_fresh()
    {
        _server.ServiceView = RouteAnswer.SpecShaped("services", "stalled");
        await Connected(() => _vm.Banner.IsShown);
        _server.ServiceView = RouteAnswer.SpecShaped("services", "fixture");

        _server.Push(A.Event(1, "views.rebuilt"));
        await Until(() => !_vm.Banner.IsShown);

        Assert.False(_vm.Banner.IsShown);
    }

    // The route is gone again (an older Sindri after a downgrade): no banner, not a stale one.
    [Fact]
    public async Task A_services_route_that_is_not_there_hides_the_banner()
    {
        _server.ServiceView = RouteAnswer.SpecShaped("services", "stalled");
        await Connected(() => _vm.Banner.IsShown);
        _server.ServiceView = new RouteAnswer(HttpStatusCode.NotFound, "{}");

        _server.Push(A.Event(1, "service.ran"));
        await Until(() => !_vm.Banner.IsShown);

        Assert.False(_vm.Banner.IsShown);
    }

    // F14: a catalog that failed on connect is read again with the next refresh, so Rebuild views shows once it answers.
    [Fact]
    public async Task A_catalog_that_failed_on_connect_is_read_again_on_the_next_refresh()
    {
        _server.Catalog = RouteAnswer.Problem(HttpStatusCode.ServiceUnavailable, "SND-API-009", "Sindri is busy (fix: retry)");
        _server.ServiceView = RouteAnswer.SpecShaped("services", "stalled");
        await Connected(() => _vm.Banner.IsShown);
        Assert.Equal((0, false), (_vm.Catalog.Controls.Count, _vm.Banner.HasRebuild));
        _server.Catalog = RouteAnswer.Api("commands", "catalog");

        _server.Push(A.Event(1, "service.ran"));
        await Until(() => _vm.Banner.HasRebuild);

        Assert.Equal((57, true, ShellState.Normal), (_vm.Catalog.Controls.Count, _vm.Banner.HasRebuild, _vm.State));
    }

    // F13: the pill words come from the goldens, not from records built by hand.
    [Theory]
    [InlineData("running-healthy", "Healthy · updated 2s ago")]
    [InlineData("running-degraded", "Degraded · updated 2s ago · index failed")]
    [InlineData("running-rebuilding", "Rebuilding · updated 2s ago")]
    [InlineData("running-stalled", "Stalled · updated 30s ago · views stalled at 42")]
    public async Task The_pill_reads_each_spec_shaped_status_golden(string scenario, string text)
    {
        var golden = AwJson.Deserialize<StatusView>(Encoding.UTF8.GetBytes(GoldenFiles.SpecShaped("status", scenario)), SindriApiClient.Json).Value;
        var age = golden.Views!.ProjectedAt - new DateTimeOffset(2026, 1, 1, 0, 1, 0, TimeSpan.Zero);   // the golden's own "updated Ns ago" at its 00:01:00 clock
        _server.Current = golden with { Views = golden.Views with { ProjectedAt = A.Noon + age } };

        await Connected(() => _vm.Pill.Text == text);

        Assert.Equal(text, _vm.Pill.Text);
    }

    // Waits up to 2 s for what the test is about to assert, and fails here, naming nothing else, when it never holds.
    private static async Task Until(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.True(condition(), "the awaited state was reached within 2 s");
    }
}
