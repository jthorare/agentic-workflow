#pragma warning disable xUnit1004 // red tests, written ahead of the cutover unit that makes them pass (spec §12 P0)
namespace Aw.Judge.Tests.CheckTraits;

// trait-check.sh branches on trait-check's own exit scheme (hook contract): 0 nothing to report or skipped, 2 findings (stderr
// for edit and stop, JSON lines on stdout for scan), 3 Jev unavailable, 64 usage.
public sealed class TraitCheckCliTests : IDisposable
{
    private readonly TempStateDirectory _state = new();

    [Fact(Skip = RedUntil.TraitCheck), Trait("Lane", "integration")]
    public async Task With_jev_excluded_the_check_exits_3()
    {
        var run = await new JudgeCli(_state).Run("trait-check", "--stop", "--providers", "rules");

        Assert.Equal(3, run.ExitCode);
    }

    [Fact(Skip = RedUntil.TraitCheck), Trait("Lane", "integration")]
    public async Task An_edit_of_a_file_no_trait_covers_exits_0_and_prints_nothing()
    {
        var run = await new JudgeCli(_state).Run("trait-check", "--edit", "--file", "docs/readme.md", "--providers", "rules");

        Assert.Equal(new CliRun(0, "", ""), run);
    }

    [Fact(Skip = RedUntil.TraitCheck), Trait("Lane", "integration")]
    public async Task Findings_exit_2_with_the_message_on_stderr_for_edit_and_json_lines_on_stdout_for_scan()
    {
        var cli = new JudgeCli(_state);

        var edit = await cli.Run("trait-check", "--edit", "--file", "src/app.ts");
        var scan = await cli.Run("trait-check", "--scan", "--path", "src");

        Assert.Equal((2, ""), (edit.ExitCode, edit.Stdout));
        Assert.NotEmpty(edit.Stderr);
        Assert.Equal(2, scan.ExitCode);
        Assert.StartsWith("{", scan.Stdout, StringComparison.Ordinal);
    }

    public void Dispose() => _state.Dispose();
}
