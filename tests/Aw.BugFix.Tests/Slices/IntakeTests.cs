using Aw.BugFix.Core.Errors;
using Aw.BugFix.Core.Model;
using Aw.BugFix.Features.AdvanceInvestigate;
using Aw.BugFix.Testing;

namespace Aw.BugFix.Tests.Slices;

public sealed class IntakeTests : IDisposable
{
    private readonly World _w = new();

    public void Dispose() => _w.Dispose();

    [Fact, Trait("Lane", "unit")]
    public async Task Init_writes_intake_state_for_a_valid_ticket()
    {
        var result = World.Ok(await _w.Init());

        Assert.Equal("/state/eng-1/state.json", result.State);
        var state = _w.Now;
        Assert.Equal((Phase.Intake, BugFixStatus.Active, 0, 1), (state.Phase, state.Status, state.Attempt, state.SchemaVersion));
        Assert.Equal("ENG-1", state.Ticket.Reference);
        Assert.Equal("init", Assert.Single(state.History).Command);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Init_refuses_while_an_unfinished_bug_fix_exists()
    {
        _ = World.Ok(await _w.Init());

        var error = World.Refused(await _w.Init(), BugFixErrors.WrongPhase);

        Assert.Contains("resume it instead", error.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("{")]
    [InlineData("""{"source":"linear","id":null,"title":"t","brief":" ","expected":"e","actual":"a"}""")]
    [InlineData("""{"source":"linear","id":null,"title":"t","brief":"b","expected":"","actual":"a"}""")]
    [InlineData("""{"source":"jira","id":null,"title":"t","brief":"b","expected":"e","actual":"a"}""")]
    [InlineData("""{"source":"text","title":"t","brief":"b","expected":"e"}""")]
    public async Task Init_rejects_a_ticket_that_is_not_a_complete_ticket(string ticket)
    {
        _w.Repos.Write(World.TicketFile, ticket);

        _ = World.Refused(await _w.Init(), BugFixErrors.Invalid);
        Assert.Equal(0, _w.Store.Saves);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Advance_investigate_snapshots_the_root_cause_and_hypotheses()
    {
        _ = World.Ok(await _w.Init());

        var result = World.Ok(await Investigate());

        Assert.Equal((Phase.Investigate, 2), (result.Phase, result.Hypotheses));
        var snapshot = _w.Now.Investigation!; // why: the step just succeeded, which sets it.
        Assert.Equal("The session cookie is dropped on redirect.", snapshot.RootCause);
        Assert.Equal(["src/app.ts", "src/b.ts"], snapshot.Hypotheses[0].Files);
        Assert.Equal(HypothesisResult.Confirmed, snapshot.Hypotheses[0].Result);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("unfixed", "confirmed", "a cause", "expected \"diagnosed\"")]
    [InlineData("diagnosed", "confirmed", null, "no ## Root Cause")]
    public async Task Advance_investigate_refuses_a_handoff_that_does_not_support_a_fix(string status, string result, string? cause, string reason)
    {
        _ = World.Ok(await _w.Init());
        _w.Repos.Write(World.HandoffFile, A.Handoff(status, [result], cause));

        var error = World.Refused(await Investigate(), BugFixErrors.HandoffRefused);

        Assert.Contains(reason, error.Message, StringComparison.Ordinal);
        Assert.Equal(Phase.Intake, _w.Now.Phase);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_handoff_without_a_confirmed_hypothesis_is_taken_when_two_are_open_and_never_asks_a_human()
    {
        _ = World.Ok(await _w.Init());
        _w.Repos.Write(World.HandoffFile, A.Handoff(results: ["untested", "untested"]));
        var result = World.Ok(await Investigate());

        Assert.Equal(Phase.Investigate, result.Phase);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_handoff_with_one_unconfirmed_hypothesis_is_refused_without_asking_a_human()
    {
        _ = World.Ok(await _w.Init());
        _w.Repos.Write(World.HandoffFile, A.Handoff(results: ["untested"]));

        var message = World.Refused(await Investigate(), BugFixErrors.HandoffRefused).Message;

        Assert.Contains("no hypothesis is confirmed", message, StringComparison.Ordinal);
        Assert.DoesNotContain("ask the user", message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Editing_the_handoff_after_advance_investigate_changes_nothing()
    {
        await _w.Investigated();
        _w.Repos.Write(World.HandoffFile, A.Handoff(rootCause: "something else"));

        Assert.Equal("The session cookie is dropped on redirect.", _w.Now.Investigation?.RootCause);
        _ = World.Refused(await Investigate(), BugFixErrors.WrongPhase);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_step_without_state_says_to_run_init_first()
    {
        var error = World.Refused(await Investigate(), BugFixErrors.StateMissing);

        Assert.Contains("run bugfix-state init first", error.Message, StringComparison.Ordinal);
    }

    private Task<Kernel.Result<AdvanceInvestigateResponse>> Investigate() =>
        _w.Run<AdvanceInvestigateRequest, AdvanceInvestigateResponse>(new AdvanceInvestigateRequest(World.State, World.HandoffFile));
}
