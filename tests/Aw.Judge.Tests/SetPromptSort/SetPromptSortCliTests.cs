namespace Aw.Judge.Tests.SetPromptSort;

public sealed class SetPromptSortCliTests : IDisposable
{
    private readonly TempStateDirectory _state = new();

    [Fact, Trait("Lane", "integration")]
    public async Task Config_set_prompt_sort_enabled_writes_the_block_and_prints_the_config()
    {
        var run = await new JudgeCli(_state).Run("config", "set", "prompt-sort", "enabled", "true");

        Assert.Equal(0, run.ExitCode);
        Assert.True(run.Json().GetProperty("promptSort").GetProperty("enabled").GetBoolean());
        Assert.Contains("\"promptSort\"", await File.ReadAllTextAsync(Path.Combine(_state.Path, "judge", "config.json"), TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Prompt_sort_scaffold_switches_one_scaffold_and_prints_the_config()
    {
        var run = await new JudgeCli(_state).Run("prompt-sort", "scaffold", "ui-evidence", "on");

        Assert.Equal(0, run.ExitCode);
        Assert.True(run.Json().GetProperty("promptSort").GetProperty("scaffolds").GetProperty("ui-evidence").GetBoolean());
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_file_it_writes_is_one_config_get_still_reads()
    {
        var cli = new JudgeCli(_state);
        _ = await cli.Run("prompt-sort", "scaffold", "brief", "on");
        _ = await cli.Run("config", "set", "prompt-sort", "enabled", "false");

        var run = await cli.Run("config", "get");

        Assert.Equal(0, run.ExitCode);
        Assert.False(run.Json().GetProperty("promptSort").GetProperty("enabled").GetBoolean());
    }

    public void Dispose() => _state.Dispose();
}
