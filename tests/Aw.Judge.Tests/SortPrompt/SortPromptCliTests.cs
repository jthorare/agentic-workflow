namespace Aw.Judge.Tests.SortPrompt;

// The prompt-sort.sh hook path through the real command line: the hook reads .context and nothing else; by default
// (every scaffold ships off) the sorter records a decision and prints nothing, and it never takes the prompt down.
public sealed class SortPromptCliTests : IDisposable
{
    private readonly TempStateDirectory _state = new();

    [Fact, Trait("Lane", "integration")]
    public async Task The_hook_path_prints_nothing_and_exits_0_while_every_scaffold_is_off()
    {
        var run = await new JudgeCli(_state).RunWithInput("""{"prompt":"add a dark mode toggle","sessionId":"sess-1"}""", "prompt-sort", "--providers", "rules");

        Assert.Equal(new CliRun(0, "", ""), run);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_hook_path_writes_the_session_file_done_gate_reads()
    {
        _ = await new JudgeCli(_state).RunWithInput("""{"prompt":"redesign the settings page layout","sessionId":"sess-1"}""", "prompt-sort", "--providers", "rules");

        Assert.True(File.Exists(Path.Combine(_state.Path, "judge", "sessions", "sess-1.sort.json")));
    }

    public void Dispose() => _state.Dispose();
}
