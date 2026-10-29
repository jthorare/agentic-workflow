using System.Net;
using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Controls;
using Aw.Sindri.Ui.ViewModels.Run;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Run;

/// <summary>A step that ended on a limit says which one, after how long, and the branch that kept its work (states.json 'limitEnding').</summary>
[Lane(TestLane.Unit)]
public sealed class RunPageLimitEndingTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private readonly FakeSindriApi _server = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static RouteAnswer Body<T>(T value) => new(HttpStatusCode.OK, JsonSerializer.Serialize(value, SindriApiClient.Json));

    private async Task<RunPageViewModel> Failed(string events)
    {
        var line = RunFixtures.Line("plan", new Dictionary<string, string>(StringComparer.Ordinal) { ["plan"] = "FAILED" });
        line = line with
        {
            State = "stuck",
            Since = A.Noon.AddHours(-1),
            States = [.. line.States.Select(c => string.Equals(c.State, "stuck", StringComparison.Ordinal) ? c with { Glyph = StateGlyph.Done, Outcome = "failed" } : c)],
        };
        var child = RunFixtures.Child("plan", ("produce", StateGlyph.Active, false), ("check", StateGlyph.Pending, false));
        var run = new StepRun("review.1", "produce", Attempt: 1, MaxAttempts: null, "failed", "agent", A.Noon.AddHours(-4), A.Noon.AddHours(-3), DurationMs: 3_600_000, Outcome: null, Tokens: null, Actor: null);
        _server.InstanceBody = id => string.Equals(id, line.Id, StringComparison.Ordinal) ? Body(line)
            : string.Equals(id, "WFI-plan", StringComparison.Ordinal) ? Body(child with { StepRuns = [run] })
            : RunFixtures.Stages.Contains(id["WFI-".Length..], StringComparer.Ordinal) ? Body(RunFixtures.Child(id["WFI-".Length..], ("produce", StateGlyph.Done, false))) : null;
        _server.InstanceEventAnswers["WFI-plan"] = new RouteAnswer(HttpStatusCode.OK, events);
        var page = new RunPageViewModel(_server, new CatalogViewModel(new FakeClipboard(), new FakeTimeProvider(A.Noon)), new FakeClipboard(), new FakeTimeProvider(A.Noon));
        await page.Open(_api, line.Address, line.Id, Token);
        return page;
    }

    [Fact]
    public async Task A_memory_limit_with_a_kept_branch_says_all_of_it()
    {
        var page = await Failed(GoldenFiles.SpecShaped("workflow-instance-events", "sandbox-limit-memory-branch"));

        Assert.Equal("This Step ran out of memory (12GiB) after 21 min. Its work so far is kept on branch sindri/itm-0f-review-1.", page.FailureText);
    }

    [Fact]
    public async Task A_wall_limit_with_no_branch_says_time_only()
    {
        var page = await Failed(GoldenFiles.SpecShaped("workflow-instance-events", "sandbox-limit-wall-no-branch"));

        Assert.Equal("This Step ran out of time after 40 min.", page.FailureText);
    }

    [Theory]
    [InlineData(59, "59 s")]
    [InlineData(60, "1 min")]
    [InlineData(720, "12 min")]
    [InlineData(3900, "1 h 5 min")]
    [InlineData(7200, "2 h")]
    public void The_time_reads_in_words(long seconds, string expected) =>
        Assert.Equal("This Step ran out of time after " + expected + ".", ViewModels.Labels.LimitEnding("wall", "40m", seconds.ToString(System.Globalization.CultureInfo.InvariantCulture), branch: null));
}
