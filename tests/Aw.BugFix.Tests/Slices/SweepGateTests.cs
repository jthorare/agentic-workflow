using Aw.BugFix.Core.Errors;
using Aw.BugFix.Core.Ids;
using Aw.BugFix.Core.Model;
using Aw.BugFix.Features.AdvanceReport;
using Aw.BugFix.Features.JudgeCandidate;
using Aw.BugFix.Features.Resume;

namespace Aw.BugFix.Tests.Slices;

/// <summary>advance report --candidate needs the sweep: a "Where else we checked" table, unless the Prevention decision is an existing guard or deferred.</summary>
public sealed class SweepGateTests : IDisposable
{
    private const string SweepFile = "/state/sweep.md";
    private const string Table = "| Surface | Variant | Status |\n|---|---|---|\n";

    private readonly World _w = new();

    public void Dispose() => _w.Dispose();

    [Fact, Trait("Lane", "unit")]
    public async Task A_resolved_report_without_a_sweep_file_is_refused()
    {
        var c1 = await Resolvable();

        var error = World.Refused(await Report(c1, sweepFile: null), BugFixErrors.SweepRefused);

        Assert.Contains("--sweep", error.Message, StringComparison.Ordinal);
        Assert.Equal(Phase.Evaluate, _w.Now.Phase);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_empty_table_with_a_new_mechanism_is_refused()
    {
        var c1 = await Resolvable();
        _w.Repos.Write(SweepFile, Sweep(rows: "", decision: "new mechanism: analyzer AW0042 bans raw cookie reads"));

        var error = World.Refused(await Report(c1), BugFixErrors.SweepRefused);

        Assert.Contains("Where else we checked", error.Message, StringComparison.Ordinal);
        Assert.Equal(BugFixStatus.Active, _w.Now.Status);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("existing guard: the typed loader")]
    [InlineData("Deferred: https://github.com/acme/api/issues/9")]
    public async Task An_empty_table_passes_when_the_decision_is_an_existing_guard_or_deferred(string decision)
    {
        var c1 = await Resolvable();
        _w.Repos.Write(SweepFile, Sweep(rows: "", decision: decision));

        var result = World.Ok(await Report(c1));

        Assert.Equal(BugFixStatus.Resolved, result.Status);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_table_with_a_status_on_every_row_resolves_and_the_history_keeps_both_the_sweep_file_and_the_run_evidence()
    {
        var c1 = await Resolvable();
        _w.Repos.Write(SweepFile, Sweep(
            rows: "| Orders screen | web | fixed |\n| Invoices screen | web | not affected (reads the typed loader) |\n| Orders screen | mobile | follow-up https://github.com/acme/api/issues/10 |\n",
            decision: "new mechanism: analyzer"));

        var result = World.Ok(await Report(c1));

        Assert.Equal((Phase.Report, BugFixStatus.Resolved), (result.Phase, result.Status));
        var step = _w.Now.History[^1];
        Assert.Contains(SweepFile, step.Command, StringComparison.Ordinal);   // the sweep file is in the step's command ...
        Assert.NotNull(step.Evidence);                                         // ... and the candidate's run evidence stays in Evidence
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("| Orders screen | web | probably fine |")]
    [InlineData("| Orders screen | web | not affected |")]
    [InlineData("| Orders screen | web | follow-up |")]
    [InlineData("| Orders screen | web |")]
    public async Task A_row_without_a_valid_status_is_refused_even_for_an_existing_guard(string row)
    {
        var c1 = await Resolvable();
        _w.Repos.Write(SweepFile, Sweep(rows: row + "\n", decision: "existing guard: the typed loader"));

        var error = World.Refused(await Report(c1), BugFixErrors.SweepRefused);

        Assert.Contains("Orders screen", error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_unresolved_report_needs_no_sweep()
    {
        await _w.Reproduced();

        var result = World.Ok(await _w.Run<AdvanceReportRequest, AdvanceReportResponse>(new AdvanceReportRequest(World.State, Candidate: null, Unresolved: true)));

        Assert.Equal(BugFixStatus.Unresolved, result.Status);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Resume_names_the_sweep_flag_in_the_report_step()
    {
        var c1 = await Resolvable();

        Assert.Equal($"advance report --candidate {c1.Value} --sweep <sweep.md>", World.Ok(await _w.Run<ResumeRequest, ResumeResponse>(new ResumeRequest(World.State))).Next);
    }

    private static string Sweep(string rows, string decision) =>
        $"## Where else we checked\n\n{Table}{rows}\n## Prevention decision\n\n{decision}\n";

    private async Task<CandidateId> Resolvable()
    {
        var c1 = await _w.Candidate();
        _ = World.Ok(await _w.RunAgainst(c1));
        _ = World.Ok(await _w.Run<JudgeCandidateRequest, JudgeCandidateResponse>(new JudgeCandidateRequest(World.State, c1)));
        return c1;
    }

    private Task<Aw.Kernel.Result<AdvanceReportResponse>> Report(CandidateId candidate, string? sweepFile = SweepFile) =>
        _w.Run<AdvanceReportRequest, AdvanceReportResponse>(new AdvanceReportRequest(World.State, candidate, Unresolved: false, sweepFile));
}
