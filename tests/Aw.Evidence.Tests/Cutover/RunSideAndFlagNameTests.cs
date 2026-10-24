using System.Text.RegularExpressions;
using Aw.Evidence.Testing;
using Aw.Evidence.Tests.Cli;
using Aw.Kernel.Testing;

namespace Aw.Evidence.Tests.Cutover;

/// <summary>
/// Cutover gaps G-e, G-g and the version verb (spec §3.3, unit 1.4).
/// G-e (checked in code, 2026-10-23): api-run and db-run already record appBuild (the commit) and at (base or head)
/// per run (ApiSummary, DbSummary, --at); the UI run and parity summaries record appBuild but have no
/// at and no --at flag. A run's summary.json is the per-artifact record of every screenshot beside it, so closing DTC9 for UI
/// evidence is the at field and flag. G-g: one flag name for the data's provenance, --provenance, on every verb; parity keeps
/// accepting --db-provenance for one release. Tests without a skip already pass and stay green.
/// </summary>
[Lane(TestLane.Integration)]
[Collection(ConsoleCapture.Name)]
public sealed partial class RunSideAndFlagNameTests : IDisposable
{

    private readonly CutoverCli _cli = new();

    public void Dispose() => _cli.Dispose();

    private string Script() => _cli.Write("script.json", TestJson.Write(A.Script().WithStep(ScriptBuilder.Goto("/hospitals")).Build()));

    private string Manifest() => _cli.Write("manifest.json", TestJson.Write(A.Manifest().Build()));

    [Theory]
    [InlineData("base")]
    [InlineData("head")]
    public async Task Run_records_the_side_it_was_at_beside_the_commit_it_ran_on(string side)
    {
        var (exit, output, error) = await _cli.Run("run", Script(), _cli.Root.Combine("run-1"), "--app-build", "abc1234", "--at", side);

        Assert.True(exit == 0, error);
        Assert.Contains($"\"at\":\"{side}\"", output, StringComparison.Ordinal);
        Assert.Contains("\"appBuild\":\"abc1234\"", output, StringComparison.Ordinal);
        Assert.Contains($"\"at\":\"{side}\"", await File.ReadAllTextAsync(_cli.Root.Combine("run-1/summary.json"), TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Run_without_at_records_no_side_and_a_value_other_than_base_or_head_is_a_usage_error()
    {
        var (plainExit, plainOut, _) = await _cli.Run("run", Script(), _cli.Root.Combine("run-1"));
        var (badExit, _, badErr) = await _cli.Run("run", Script(), _cli.Root.Combine("run-2"), "--at", "middle");

        Assert.Equal(0, plainExit);
        Assert.DoesNotContain("\"at\":", plainOut, StringComparison.Ordinal);
        Assert.Equal(1, badExit);
        Assert.StartsWith("ui-evidence: usage: ui-evidence run ", badErr, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("base")]
    [InlineData("head")]
    public async Task Parity_takes_at_base_or_head_and_records_it(string side)
    {
        var (_, _, error) = await _cli.Run("parity", Manifest(), _cli.Root.Combine("parity-1"), "--app-build", "abc1234", "--at", side);

        Assert.DoesNotContain("usage: ui-evidence", error, StringComparison.Ordinal);   // that parity.json carries "at" is pinned with the run in unit 1.4
    }

    [Theory]
    [InlineData("seeded")]
    [InlineData("unknown")]
    public async Task Parity_takes_provenance_as_the_one_flag_name(string provenance)
    {
        var (_, _, error) = await _cli.Run("parity", Manifest(), _cli.Root.Combine("parity-1"), "--provenance", provenance);

        Assert.DoesNotContain("usage: ui-evidence", error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("seeded")]
    [InlineData("unknown")]
    public async Task Parity_still_accepts_db_provenance_for_one_release(string provenance)
    {
        var (_, _, error) = await _cli.Run("parity", Manifest(), _cli.Root.Combine("parity-1"), "--db-provenance", provenance);

        Assert.DoesNotContain("usage: ui-evidence", error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Parity_refuses_both_provenance_flags_at_once_and_a_value_that_is_not_seeded_or_unknown()
    {
        var (bothExit, _, bothErr) = await _cli.Run("parity", Manifest(), _cli.Root.Combine("parity-1"), "--provenance", "seeded", "--db-provenance", "unknown");
        var (badExit, _, badErr) = await _cli.Run("parity", Manifest(), _cli.Root.Combine("parity-2"), "--provenance", "maybe");

        Assert.Equal((1, 1), (bothExit, badExit));
        Assert.StartsWith("ui-evidence: usage: ui-evidence parity ", bothErr, StringComparison.Ordinal);
        Assert.StartsWith("ui-evidence: usage: ui-evidence parity ", badErr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_other_verbs_that_take_provenance_use_the_one_flag_name_already()
    {
        var (_, _, planErr) = await _cli.Run("publish-plan", _cli.Root.Combine("none"), "--provenance", "seeded");
        var (_, _, oldNameErr) = await _cli.Run("publish-plan", _cli.Root.Combine("none"), "--db-provenance", "seeded");

        Assert.DoesNotContain("usage: ui-evidence", planErr, StringComparison.Ordinal);   // the missing run is a file error, not a usage error
        Assert.StartsWith("ui-evidence: usage: ui-evidence publish-plan ", oldNameErr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Version_prints_the_build_stamp_on_one_line_and_exits_0_without_a_browser_a_lock_or_a_stack()
    {
        var (exit, output, error) = await _cli.Run("version");

        Assert.Equal((0, ""), (exit, error));
        Assert.Matches(Stamp, output.TrimEnd());
        _ = Assert.Single(output.TrimEnd().Split('\n'));
        Assert.Empty(_cli.Browser.Sessions);
    }

    // 'ui-evidence commit <sha or unknown>, built <UTC ISO 8601 or unknown>', the shape of 'aw-meta version'.
    [GeneratedRegex(@"^ui-evidence commit \S+, built \S+", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Stamp { get; }
}
