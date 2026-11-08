using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Sandbox;

namespace Aw.Sindri.Ui.Tests.Sandbox;

/// <summary>Check again, one test per states.json checkAgain row (sandbox spec §8 States; accessibility row Check again).</summary>
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
    public async Task A_check_that_did_not_run_is_shown_as_a_caption_and_an_empty_list_shows_none()
    {
        _bed.Server.SandboxCheckAnswer = SandboxTestBed.CheckBody(["No sandbox tool."], [], notChecked: [SandboxTestBed.HooksNotChecked]);
        await Check.Check(Token);
        Assert.Equal((true, SandboxTestBed.HooksNotChecked), (Check.HasNotChecked, SandboxTestBed.J(Check.NotChecked)));

        _bed.Server.SandboxCheckAnswer = SandboxTestBed.CheckBody(["No sandbox tool."], []);
        await Check.Check(Token);
        Assert.Equal((false, 0), (Check.HasNotChecked, Check.NotChecked.Count));
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

        Assert.Equal("False/Still blocked: 1 thing to fix | False/Check again | True/Check again | True/Still blocked: 1 thing to fix", SandboxTestBed.J(seen.Select(x => x.Live + "/" + x.Text)));
        Assert.All(seen, s => Assert.True(s.Text.Length > 0));
    }

    [Fact]
    public async Task All_clear_with_nothing_parked_just_says_ready()
    {
        _bed.Server.SandboxCheckAnswer = SandboxTestBed.CheckBody([], []);

        await Check.Check(Token);

        Assert.Equal(Labels.SandboxReadyNothingWaiting, Check.Result);
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
