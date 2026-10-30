using System.Net;
using System.Text.Json;
using Avalonia.Controls;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Controls;
using Aw.Sindri.Ui.ViewModels.Run;
using Aw.Sindri.Ui.Views.Run;
using Microsoft.Extensions.Time.Testing;
using Labels = Aw.Sindri.Ui.ViewModels.Labels;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>
/// The run page's key frames (F3 run canvas spec §3, §7) at 1440 x 900, one per run state the spec lists: running, needs you, ended well and failed. Each opens the page on a
/// line run, checks that the header, badge and canvas are there with the state's word, that the header text fits its row and that the page does not scroll sideways, and saves a
/// frame under frames/ as f3-run-*.png.
/// </summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class RunPageFramesTests : FrameHost
{
    private const string Prefix = "f3-";
    private static readonly Uri _api = new("http://127.0.0.1:7410/");

    private static RouteAnswer Body<T>(T value) => new(HttpStatusCode.OK, JsonSerializer.Serialize(value, SindriApiClient.Json));

    // The line stopped at plan with its plan child in the given chip; a run that ended is moved to its end card.
    private static InstanceDetail Line(string planChip, string? end = null, string? outcome = null)
    {
        var line = RunFixtures.Line("plan", new Dictionary<string, string>(StringComparer.Ordinal) { ["plan"] = planChip }, outcome);
        return end is null
            ? line
            : line with
            {
                State = end,
                Since = A.Noon.AddHours(-1),
                States = [.. line.States.Select(c => string.Equals(c.State, end, StringComparison.Ordinal) ? c with { Glyph = StateGlyph.Done, Outcome = outcome } : c)],
            };
    }

    private async Task<(Window Window, RunPageViewModel Page)> Opened(InstanceDetail line, bool failedStep = false)
    {
        Server.InstanceBody = id =>
            string.Equals(id, line.Id, StringComparison.Ordinal) ? Body(line)
            : string.Equals(id, "WFI-plan", StringComparison.Ordinal) && failedStep
                ? Body(RunFixtures.Child("plan", ("produce", StateGlyph.Active, false), ("check", StateGlyph.Pending, false)) with
                {
                    StepRuns = [new StepRun("review.1", "produce", Attempt: 1, MaxAttempts: null, "failed", "agent", A.Noon.AddHours(-4), A.Noon.AddHours(-3), DurationMs: 3_600_000, Outcome: null, Tokens: null, Actor: null)],
                })
            : RunFixtures.Stages.Contains(id["WFI-".Length..], StringComparer.Ordinal) ? Body(RunFixtures.Child(id["WFI-".Length..], ("produce", StateGlyph.Active, false), ("check", StateGlyph.Pending, false))) : null;
        Server.InstanceEventAnswers["WFI-plan"] = new RouteAnswer(HttpStatusCode.OK, GoldenFiles.SpecShaped("workflow-instance-events", "failed-run"));
        var page = new RunPageViewModel(Server, new CatalogViewModel(new FakeClipboard(), new FakeTimeProvider(A.Noon)), new FakeClipboard(), new FakeTimeProvider(A.Noon));
        await page.Open(_api, line.Address, line.Id, Token);
        var window = new Window { Width = 1440, Height = 900, Content = new RunPage { DataContext = page } };
        window.Show();
        Settle(window);
        return (window, page);
    }

    private static void Fits(Window window, RunPageViewModel page, string word)
    {
        Assert.Equal(RunPageState.Shown, page.Phase);
        Assert.Equal(word, page.HeaderWord);   // the page's own word for the state, from the shared labels
        Assert.Equal(page.HeaderWord, Named<TextBlock>(window, "RunBadgeText").Text);
        Assert.True(Visible(window, "RunHeader"));
        Assert.True(Visible(window, "RunCanvas"));
        var title = Named<TextBlock>(window, "RunTitle");
        Assert.Equal(page.Title, title.Text);
        Assert.True(title.Bounds.Width > 0);
        var header = Named<Control>(window, "RunHeader");
        Assert.True(title.Bounds.Width <= header.Bounds.Width + 0.5, "the title runs past the header");
        Assert.True(Named<Control>(window, "RunRoot").Bounds.Width <= window.Width + 0.5, "the page is wider than the window");
    }

    private Task Shown(InstanceDetail line, string name, string word, bool failedStep = false) => OnUi(async () =>
    {
        var (window, page) = await Opened(line, failedStep);
        try
        {
            Fits(window, page, word);
            Frame(window, "run-" + name + "-1440", Prefix);
        }
        finally
        {
            window.Close();
        }
    });

    [Fact]
    public Task A_running_run() => Shown(Line("RUNNING"), "running", Labels.RunWordRunning);

    [Fact]
    public Task A_run_that_needs_you() => Shown(Line("DECISION"), "needs-you", Labels.RunWordWaiting);

    [Fact]
    public Task A_run_that_ended_well() => Shown(Line("DONE", "done", "resolved"), "done", Labels.RunWordDone);

    [Fact]
    public Task A_failed_run() => Shown(Line("FAILED", "stuck", "failed"), "failed", Labels.RunWordFailed, failedStep: true);
}
