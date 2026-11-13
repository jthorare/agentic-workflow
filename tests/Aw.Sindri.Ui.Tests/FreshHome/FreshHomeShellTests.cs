using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.FreshHome;

/// <summary>On a home with no profile the shell shows setup states on the pill, the Todo card and the From elsewhere line, not failures (issue 3113, part of 3019).</summary>
[Lane(TestLane.Unit)]
public sealed class FreshHomeShellTests
{
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog"), NeedsYouView = RouteAnswer.SpecShaped("needs-you", "extra-nothing") };
    private readonly FakeTimeProvider _clock = new(DateTimeOffset.Parse("2026-01-01T09:00:00Z", System.Globalization.CultureInfo.InvariantCulture));

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<ShellViewModel> FreshHome()
    {
        _server.Current = A.Status(profile: new ProfileView(Live: null, Problem: "No profile found", Source: null, ProblemKind: "missing"));
        _server.TodoNoteAnswer = new RouteAnswer(HttpStatusCode.OK, TodoNoteSetupSample.NoteIn("can't read", lastError: "no vault"));
        _server.ReviewsAnswer = RouteAnswer.Problem(HttpStatusCode.InternalServerError, "SND-X-500", "down");
        _server.LinearIssuesAnswer = RouteAnswer.Problem(HttpStatusCode.InternalServerError, "SND-X-500", "down");
        _records.Write(A.Record());
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        _ = shell.Run(Token);
        await ShellWait.Until(shell, () => shell.State == ShellState.Normal && !shell.Todo.IsLoading, Token);
        return shell;
    }

    [Fact]
    public Task The_top_pill_does_not_say_healthy_beside_a_missing_profile() => UiThread.Run(async () =>
    {
        var shell = await FreshHome();

        Assert.Equal((false, true), (shell.Health.Pill.IsHealthy, shell.Health.Pill.Text.Contains("Not set up", StringComparison.Ordinal)));
    }, Token);

    [Fact]
    public Task The_todo_card_shows_no_note_line_when_no_profile_names_a_vault() => UiThread.Run(async () =>
    {
        var shell = await FreshHome();

        Assert.Equal((false, string.Empty, string.Empty), (shell.Todo.HasSync, shell.Todo.SyncLive, shell.Todo.SyncQuiet));
    }, Token);

    [Fact]
    public Task The_elsewhere_line_offers_to_connect_instead_of_retrying_failed_reads() => UiThread.Run(async () =>
    {
        var shell = await FreshHome();

        await ShellWait.Until(shell, () => !shell.Elsewhere.IsLoading, Token);

        Assert.Equal(("Connect GitHub · Connect Linear", false), (shell.Elsewhere.Text, shell.Elsewhere.Parts.Any(p => p.CanRetry)));
    }, Token);

    [Fact]
    public Task The_no_profile_line_on_the_health_page_opens_setup() => UiThread.Run(async () =>
    {
        var shell = await FreshHome();

        shell.Health.RequestSetup();

        Assert.Equal(PageId.Settings, shell.CurrentPage);
    }, Token);
}
