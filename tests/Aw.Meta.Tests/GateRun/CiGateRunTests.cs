using Aw.Meta.Core.Commands;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Gate;
using Aw.Meta.Features.GateRun;
using Aw.Meta.Testing;
using Aw.Meta.Tests.Reserve;

namespace Aw.Meta.Tests.GateRun;

public sealed class CiGateRunTests
{
    private const string View = "gh pr view 190 --json headRefOid,baseRefName,reviewDecision,mergeStateStatus";
    private const string Checks = "gh pr checks 190 --required --json name,state,bucket";
    private const string Pass = """{"name":"build","state":"SUCCESS","bucket":"pass"}""";
    private const string Second = """{"name":"types","state":"SUCCESS","bucket":"pass"}""";
    private readonly MetaWorld _world = new();

    private GateRunService Service() => new(
        _world.Config,
        new GateWorkspace(_world.Repo, _world.Trees, _world.Paths, _world.LogFiles),
        new GateStepRunner(_world.Runner, _world.Paths),
        _world.Gates,
        Worlds.Reservation(_world),
        _world.Clock,
        _world.Repo);

    private async Task Script(string checks, string review = "APPROVED", string merge = "CLEAN", int checksExit = 0, string checksError = "")
    {
        await _world.Ready();
        var team = (await _world.Config.Load(CancellationToken.None)).Value;
        _ = await _world.Config.Save(team with { Gate = GateConfig.Empty with { Mode = "ci" } }, CancellationToken.None);
        _ = _world.Runner
            .When(View, $$"""{"headRefOid":"headsha","baseRefName":"main","reviewDecision":"{{review}}","mergeStateStatus":"{{merge}}"}""")
            .When(Checks, _ => new CommandResult(checksExit, checks, checksError, TimedOut: false));
    }

    private async Task<GateRecord> Run()
    {
        var result = await Service().Handle(new GateRunRequest(190, []), CancellationToken.None);
        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        var stored = (await _world.Gates.Latest(190, CancellationToken.None)).Value;
        Assert.Equal(stored, result.Value.Gate);
        return result.Value.Gate;
    }

    private static string[] Failed(GateRecord gate) => [.. gate.Steps.Where(s => s.Verdict != GateVerdict.Pass).Select(s => s.Name)];

    [Fact, Trait("Lane", "unit")]
    public async Task Every_required_check_green_approved_and_clean_records_a_pass_at_the_head()
    {
        await Script($"[{Pass},{Second}]");

        var gate = await Run();

        Assert.Equal((GateVerdict.Pass, "headsha", string.Empty), (gate.Verdict, gate.Head, gate.Main));
        Assert.Equal(["ci"], gate.Scopes, StringComparer.Ordinal);
        Assert.Equal(["build", "types", "review", "merge-state"], gate.Steps.Select(s => s.Name), StringComparer.Ordinal);
        Assert.Empty(Failed(gate));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_failing_required_check_fails_the_gate_and_names_the_check_and_its_state()
    {
        await Script($$"""[{{Pass}},{"name":"types","state":"FAILURE","bucket":"fail"}]""");

        var gate = await Run();

        Assert.Equal(GateVerdict.Fail, gate.Verdict);
        Assert.Equal(["types"], Failed(gate));
        Assert.Contains("FAILURE", gate.Steps.Single(s => s.Name == "types").Log, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_pending_required_check_fails_the_gate_as_pending()
    {
        await Script($$"""[{{Pass}},{"name":"types","state":"IN_PROGRESS","bucket":"pending"}]""");

        var gate = await Run();

        Assert.Equal(GateVerdict.Fail, gate.Verdict);
        Assert.Equal(["types"], Failed(gate));
        Assert.Contains("pending", gate.Steps.Single(s => s.Name == "types").Log, StringComparison.OrdinalIgnoreCase);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("SKIPPED", "skipping", true)]
    [InlineData("NEUTRAL", "skipping", true)]
    [InlineData("CANCELLED", "cancel", false)]
    public async Task A_skipped_required_check_passes_as_on_github_and_a_cancelled_one_fails(string state, string bucket, bool passes)
    {
        await Script($$"""[{{Pass}},{"name":"types","state":"{{state}}","bucket":"{{bucket}}"}]""");

        var gate = await Run();

        Assert.Equal(passes ? GateVerdict.Pass : GateVerdict.Fail, gate.Verdict);
        string[] failed = passes ? [] : ["types"];
        Assert.Equal(failed, Failed(gate));
        Assert.Contains(state, gate.Steps.Single(s => s.Name == "types").Log, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Every_required_check_skipped_with_a_clean_merge_state_passes()
    {
        await Script("""[{"name":"build","state":"SKIPPED","bucket":"skipping"},{"name":"types","state":"NEUTRAL","bucket":"skipping"}]""");

        var gate = await Run();

        Assert.Equal(GateVerdict.Pass, gate.Verdict);
        Assert.Empty(Failed(gate));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_review_that_is_not_approved_fails_the_gate()
    {
        await Script($"[{Pass}]", review: "CHANGES_REQUESTED");

        var gate = await Run();

        Assert.Equal(GateVerdict.Fail, gate.Verdict);
        Assert.Equal(["review"], Failed(gate));
        Assert.Contains("CHANGES_REQUESTED", gate.Steps.Single(s => s.Name == "review").Log, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_merge_state_that_is_not_clean_fails_the_gate()
    {
        await Script($"[{Pass}]", merge: "BLOCKED");

        var gate = await Run();

        Assert.Equal(GateVerdict.Fail, gate.Verdict);
        Assert.Equal(["merge-state"], Failed(gate));
        Assert.Contains("BLOCKED", gate.Steps.Single(s => s.Name == "merge-state").Log, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("[]", 0, "")]
    [InlineData("", 1, "no required checks reported on the 'main' branch")]
    public async Task A_base_with_no_required_checks_is_refused_and_nothing_is_recorded(string checks, int exit, string error)
    {
        await Script(checks, checksExit: exit, checksError: error);

        var result = await Service().Handle(new GateRunRequest(190, []), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("AW-META-024", result.Error.Code);
        Assert.Contains("no required checks on main", result.Error.Message, StringComparison.Ordinal);
        Assert.Null((await _world.Gates.Latest(190, CancellationToken.None)).Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Checks_that_are_pending_or_failed_exit_non_zero_in_gh_and_are_still_read_from_its_output()
    {
        await Script($$"""[{{Pass}},{"name":"types","state":"IN_PROGRESS","bucket":"pending"}]""", checksExit: 8);

        var gate = await Run();

        Assert.Equal(GateVerdict.Fail, gate.Verdict);
        Assert.Equal(["types"], Failed(gate));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_ci_gate_makes_no_gate_tree_runs_no_step_and_takes_no_heavy_reservation()
    {
        await Script($"[{Pass}]");

        _ = await Run();

        Assert.DoesNotContain(_world.Runner.Lines, l => l.Contains("worktree", StringComparison.Ordinal) || l.Contains("git merge", StringComparison.Ordinal) || l.Contains("/bin/heavy", StringComparison.Ordinal));
        Assert.All(_world.Runner.Requests, r => Assert.Null(r.LogFile));
        Assert.Null(_world.Heavy.Record);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_gh_without_json_for_checks_is_read_from_its_plain_output()
    {
        await Script(string.Empty, checksExit: 1, checksError: "unknown flag: --json");
        _ = _world.Runner.When("gh pr checks 190 --required", _ => new CommandResult(8, "build\tpass\t5s\thttps://x/1\ntypes\tpending\t0\thttps://x/2\n", string.Empty, TimedOut: false));

        var gate = await Run();

        Assert.Equal(GateVerdict.Fail, gate.Verdict);
        Assert.Equal(["types"], Failed(gate));
        Assert.Equal(["build", "types", "review", "merge-state"], gate.Steps.Select(s => s.Name), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_gh_error_with_no_output_is_an_error_not_a_base_with_no_required_checks()
    {
        await Script(string.Empty, checksExit: 1, checksError: "HTTP 401: Bad credentials (https://api.github.com/graphql)");

        var result = await Service().Handle(new GateRunRequest(190, []), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.False(string.Equals("AW-META-024", result.Error.Code, StringComparison.Ordinal));
        Assert.Null((await _world.Gates.Latest(190, CancellationToken.None)).Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_plain_output_row_with_no_bucket_fails_the_whole_read_and_records_nothing()
    {
        await Script(string.Empty, checksExit: 1, checksError: "unknown flag: --json");
        _ = _world.Runner.When("gh pr checks 190 --required", _ => new CommandResult(0, "build\tpass\t5s\thttps://x/1\ntypes\n", string.Empty, TimedOut: false));

        var result = await Service().Handle(new GateRunRequest(190, []), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.False(string.Equals("AW-META-024", result.Error.Code, StringComparison.Ordinal));
        Assert.Null((await _world.Gates.Latest(190, CancellationToken.None)).Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_ci_gate_neither_fetches_nor_reads_origin_main_so_a_missing_origin_main_cannot_block_it()
    {
        await Script($"[{Pass}]");

        _ = await Run();

        Assert.DoesNotContain(_world.Runner.Lines, l => l.Contains("origin/main", StringComparison.Ordinal) || l.Contains("git fetch", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Scopes_given_in_ci_mode_are_refused_and_nothing_is_read_or_recorded()
    {
        await Script($"[{Pass}]");

        var result = await Service().Handle(new GateRunRequest(190, ["repo"]), CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Equal("AW-META-001", result.Error.Code);
        Assert.Contains("scopes don't apply", result.Error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(_world.Runner.Lines, l => l.StartsWith("gh ", StringComparison.Ordinal));
        Assert.Null((await _world.Gates.Latest(190, CancellationToken.None)).Value);
    }
}
