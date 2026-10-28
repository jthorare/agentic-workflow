using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Sandbox;

namespace Aw.Sindri.Ui.Tests.Sandbox;

/// <summary>Check again and the resume list, one test per states.json checkAgain row (sandbox spec §8 States; accessibility rows Check again and Resume list).</summary>
[Lane(TestLane.Unit)]
public sealed class SandboxCheckTests
{
    private readonly SandboxTestBed _bed = new();

    private SandboxCheckViewModel Check => _bed.Check;

    private static CancellationToken Token => SandboxTestBed.Token;

    [Fact]
    public void Idle_is_the_label_and_a_resting_live_region()
    {
        Assert.Equal((SandboxTestBed.Text("checkAgain", "idle"), SandboxCheckPhase.Idle, false, false), (Check.Label, Check.Phase, Check.IsChecking, Check.HasResult));
        Assert.Equal((SandboxTestBed.Text("checkAgain", "idle"), false), (Check.Live.Text, Check.Live.IsLive));
    }

    [Fact]
    public async Task In_flight_keeps_the_label_announces_sending_after_300_ms_and_a_second_click_does_nothing()
    {
        _bed.Server.SandboxCheckGate = new TaskCompletionSource();
        var row = SandboxTestBed.Row("checkAgain", "in-flight");

        var first = Check.Check(Token);
        await Check.Check(Token);

        Assert.Equal((SandboxTestBed.Text("checkAgain", "in-flight"), true, 1), (Check.Label, Check.IsChecking, _bed.Server.SandboxCheckCalls));
        Assert.True((bool)row["spinner"]!);
        Assert.Equal("nothing", (string)row["secondClickDoes"]!);
        _bed.Clock.Advance(TimeSpan.FromMilliseconds((int)row["announceAfterMs"]! - 1));
        Assert.False(Check.Live.IsLive);
        _bed.Clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal((SandboxTestBed.Announce("checkAgain", "in-flight"), true), (Check.Live.Text, Check.Live.IsLive));

        _bed.Server.SandboxCheckGate.SetResult();
        await first;
        Assert.False(Check.IsChecking);
    }

    [Fact]
    public async Task Still_failing_lists_every_blocker_not_the_first_and_says_how_many()
    {
        _bed.Server.SandboxCheckAnswer = SandboxTestBed.CheckBody(["No sandbox tool.", "Not signed in."], []);

        await Check.Check(Token);

        Assert.Equal(SandboxCheckPhase.StillFailing, Check.Phase);
        Assert.Equal("No sandbox tool. | Not signed in.", SandboxTestBed.J(Check.Blockers.Select(b => b.Text)));
        Assert.Equal("fix 0 | fix 1", SandboxTestBed.J(Check.Blockers.Select(b => b.Command)));
        Assert.Equal(SandboxTestBed.Announce("checkAgain", "still-failing").Replace("<n>", "2", StringComparison.Ordinal), Check.Result);
        Assert.Equal(Check.Result, Check.Live.Text);
    }

    [Fact]
    public async Task A_repeated_still_failing_is_a_change_the_region_rests_between()
    {
        _bed.Server.SandboxCheckAnswer = SandboxTestBed.CheckBody(["No sandbox tool."], []);
        List<(bool Live, string Text)> seen = [];
        Check.Live.PropertyChanged += (_, _) => seen.Add((Check.Live.IsLive, Check.Live.Text));

        await Check.Check(Token);
        seen.Clear();
        await Check.Check(Token);

        Assert.Equal("False/Check again | False/Check again | True/Check again | True/Still blocked: 1 thing to fix", SandboxTestBed.J(seen.Select(x => x.Live + "/" + x.Text)));
        Assert.All(seen, s => Assert.True(s.Text.Length > 0));
    }

    [Fact]
    public async Task All_clear_lists_the_steps_before_it_acts_and_sends_nothing()
    {
        _bed.Server.SandboxCheckAnswer = SandboxTestBed.CheckBody([], ["Try the engine", "Fix the login timeout"]);

        await Check.Check(Token);

        Assert.Equal(SandboxCheckPhase.AllClear, Check.Phase);
        Assert.Equal(SandboxTestBed.Text("checkAgain", "all-clear").Replace("<n>", "2", StringComparison.Ordinal), Check.Result);
        Assert.Equal("Try the engine | Fix the login timeout", SandboxTestBed.J(Check.ResumeRows.Select(r => r.Title)));
        Assert.True(Check.ShowsResume);
        Assert.Empty(_bed.Server.CommandCalls);
        var buttons = ((System.Text.Json.Nodes.JsonArray)SandboxTestBed.Row("checkAgain", "all-clear")["buttons"]!).Select(b => (string)b!);
        Assert.Equal(SandboxTestBed.J(buttons), SandboxTestBed.J([Labels.SandboxResume, Labels.SandboxNotNow]));
    }

    [Fact]
    public async Task All_clear_with_nothing_parked_just_says_ready()
    {
        _bed.Server.SandboxCheckAnswer = SandboxTestBed.CheckBody([], []);

        await Check.Check(Token);

        Assert.Equal((Labels.SandboxReadyNothingWaiting, false), (Check.Result, Check.ShowsResume));
    }

    [Fact]
    public async Task Resume_answers_each_instance_as_resume_in_order_announces_resuming_then_says_resumed()
    {
        _bed.Server.SandboxCheckAnswer = SandboxTestBed.CheckBody([], ["Try the engine", "Fix the login timeout"]);
        await Check.Check(Token);
        var resolved = 0;
        Check.Resolved += (_, _) => resolved++;
        var spoken = new List<string>();
        Check.Live.PropertyChanged += (_, _) => spoken.Add(Check.Live.Text);

        await Check.Resume(Token);

        Assert.Equal(SandboxTestBed.J([SandboxTestBed.Instance(0), SandboxTestBed.Instance(1)]), SandboxTestBed.J(_bed.Server.CommandCalls.Select(c => c.Instance)));
        Assert.All(_bed.Server.CommandCalls, c => Assert.Equal((CommandKind.Answer, AnswerVariant.Resume), (c.Body.Kind, c.Body.Answer)));
        Assert.Contains(spoken, said => string.Equals(said, SandboxTestBed.Announce("checkAgain", "all-clear").Replace("<n>", "2", StringComparison.Ordinal), StringComparison.Ordinal));
        Assert.Equal((SandboxCheckPhase.Resumed, SandboxTestBed.Text("checkAgain", "resumed").Replace("<n>", "2", StringComparison.Ordinal), 1), (Check.Phase, Check.Result, resolved));
        Assert.False(Check.ShowsResume);
    }

    [Fact]
    public async Task A_step_that_moved_on_makes_it_partial_and_the_rest_stay_parked()
    {
        _bed.Server.SandboxCheckAnswer = SandboxTestBed.CheckBody([], ["Try the engine", "Fix the login timeout"]);
        _ = _bed.Server.MovedOnInstances.Add(SandboxTestBed.Instance(1));
        await Check.Check(Token);

        await Check.Resume(Token);

        var text = SandboxTestBed.Text("checkAgain", "resumed-partial").Replace("<n>", "1", StringComparison.Ordinal).Replace("<m>", "2", StringComparison.Ordinal).Replace("<item>", "Fix the login timeout", StringComparison.Ordinal);
        Assert.Equal((SandboxCheckPhase.ResumedPartial, text), (Check.Phase, Check.Result));
        Assert.Equal(SandboxTestBed.Announce("checkAgain", "resumed-partial").Replace("<n>", "1", StringComparison.Ordinal).Replace("<m>", "2", StringComparison.Ordinal), Check.Live.Text);
    }

    [Fact]
    public async Task Not_now_drops_the_list_sends_nothing_and_asks_for_focus_back_on_check_again()
    {
        _bed.Server.SandboxCheckAnswer = SandboxTestBed.CheckBody([], ["Try the engine"]);
        await Check.Check(Token);
        var focus = 0;
        Check.FocusCheckRequested += (_, _) => focus++;

        Check.NotNow();

        Assert.Equal((false, 1, SandboxCheckPhase.Idle), (Check.ShowsResume, focus, Check.Phase));
        Assert.Empty(_bed.Server.CommandCalls);
    }

    [Fact]
    public async Task A_check_that_cannot_run_says_so_in_words()
    {
        _bed.Server.GoDown();

        await Check.Check(Token);

        Assert.Equal((Labels.SandboxCheckFailed, SandboxCheckPhase.Idle), (Check.Result, Check.Phase));
    }

    [Fact]
    public async Task Checked_shows_just_now_then_the_age()
    {
        _bed.Server.SandboxCheckAnswer = SandboxTestBed.CheckBody(["No sandbox tool."], []);
        await Check.Check(Token);
        Assert.Equal(SandboxTestBed.Text("parkCard", "checked-fresh"), Check.CheckedText);

        _bed.Clock.Advance(TimeSpan.FromMinutes(12));
        Check.Seen(Check.CheckedAt);

        Assert.Equal(SandboxTestBed.Text("parkCard", "checked-stale").Replace("<age>", "12m", StringComparison.Ordinal), Check.CheckedText);
    }

    [Fact]
    public void Never_checked_shows_no_checked_line()
    {
        Check.Seen(checkedAt: null);

        Assert.Equal((string.Empty, false), (Check.CheckedText, Check.HasChecked));
    }

    [Fact]
    public async Task Copy_puts_the_blockers_command_on_the_clipboard()
    {
        _bed.Server.SandboxCheckAnswer = SandboxTestBed.CheckBody(["No sandbox tool."], []);
        await Check.Check(Token);

        await Check.Copy(Check.Blockers[0], Token);

        Assert.Equal("fix 0", _bed.Clipboard.Text);
    }
}
