namespace Aw.Judge.Tests.SetConfig;

// The contract of 'judge config set': prints the new config, keeps hand-edited blocks.
public sealed class SetConfigCliTests : IDisposable
{
    private readonly TempStateDirectory _state = new();

    private string ConfigFile => Path.Combine(_state.Path, "judge", "config.json");

    [Fact, Trait("Lane", "integration")]
    public async Task Config_set_keeps_a_hand_edited_providers_and_prompt_sort_block()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigFile)!);   // why: the path has a directory part by construction
        await File.WriteAllTextAsync(ConfigFile,
            """{"questions":{},"providers":{"agentClis":["cursor-cli"],"jev":false},"promptSort":{"enabled":false,"scaffolds":{"brief":true}}}""",
            TestContext.Current.CancellationToken);
        var cli = new JudgeCli(_state);

        var run = await cli.Run("config", "set", "ask-check", "threshold", "0.6");

        Assert.Equal((0, ""), (run.ExitCode, run.Stderr));
        var json = (await cli.Run("config", "get")).Json();
        Assert.Equal(0.6, json.GetProperty("questions").GetProperty("ask-check").GetProperty("threshold").GetDouble());
        Assert.True(json.GetProperty("questions").GetProperty("ask-check").GetProperty("enabled").GetBoolean());
        Assert.Equal("cursor-cli", json.GetProperty("providers").GetProperty("agentClis")[0].GetString());
        Assert.False(json.GetProperty("providers").GetProperty("jev").GetBoolean());
        Assert.False(json.GetProperty("promptSort").GetProperty("enabled").GetBoolean());
        Assert.True(json.GetProperty("promptSort").GetProperty("scaffolds").GetProperty("brief").GetBoolean());
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Config_set_enabled_prints_and_stores_the_new_config()
    {
        var cli = new JudgeCli(_state);

        var run = await cli.Run("config", "set", "wake-gate", "enabled", "false");

        Assert.Equal((0, ""), (run.ExitCode, run.Stderr));
        Assert.False(run.Json().GetProperty("questions").GetProperty("wake-gate").GetProperty("enabled").GetBoolean());
        CliGolden.Match(run, "SetConfig/Golden/config-set.json");
        Assert.True(File.Exists(ConfigFile));
        Assert.False((await cli.Run("config", "get")).Json().GetProperty("questions").GetProperty("wake-gate").GetProperty("enabled").GetBoolean());
    }

    // Arguments that don't parse are usage errors: exit 64, one 'usage: judge config set …' line, nothing written.
    [Theory, Trait("Lane", "integration")]
    [InlineData("wake-gate", "thresh", "0.5", "unknown field 'thresh'")]
    [InlineData("wake-gate", "enabled", "yes", "enabled takes true or false, not 'yes'")]
    [InlineData("wake-gate", "threshold", "high", "threshold takes a number, not 'high'")]
    [InlineData("wake-gate", "threshold", "NaN", "threshold takes a number, not 'NaN'")]
    public async Task Config_set_with_an_argument_it_cant_read_is_a_usage_error(string question, string field, string value, string problem)
    {
        var run = await new JudgeCli(_state).Run("config", "set", question, field, value);

        Assert.Equal(new CliRun(64, "", $"usage: judge config set <question> enabled true|false | threshold <0-1> ({problem})\n"), run);
        Assert.False(File.Exists(ConfigFile));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Config_set_with_a_missing_argument_is_a_usage_error()
    {
        var run = await new JudgeCli(_state).Run("config", "set", "wake-gate", "enabled");

        Assert.Equal(64, run.ExitCode);
        Assert.StartsWith("usage: judge config set ", run.Stderr, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Config_set_refuses_a_mistyped_question_and_writes_nothing()
    {
        var run = await new JudgeCli(_state).Run("config", "set", "wake-gat", "enabled", "false");

        Assert.Equal(1, run.ExitCode);
        Assert.StartsWith("unknown question: wake-gat (known: ", run.Stderr, StringComparison.Ordinal);
        Assert.Contains("wake-gate", run.Stderr, StringComparison.Ordinal);
        Assert.False(File.Exists(ConfigFile));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Config_set_refuses_a_threshold_outside_0_to_1()
    {
        var run = await new JudgeCli(_state).Run("config", "set", "wake-gate", "threshold", "1.5");

        Assert.Equal(new CliRun(1, "", "threshold must be between 0 and 1, not 1.5\n"), run);
        Assert.False(File.Exists(ConfigFile));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Config_set_refuses_to_overwrite_a_file_that_isnt_a_valid_config()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigFile)!);   // why: the path has a directory part by construction
        await File.WriteAllTextAsync(ConfigFile, "not json", TestContext.Current.CancellationToken);

        var run = await new JudgeCli(_state).Run("config", "set", "wake-gate", "threshold", "0.5");

        Assert.Equal((1, ""), (run.ExitCode, run.Stdout));
        Assert.StartsWith($"invalid config {ConfigFile}: ", run.Stderr, StringComparison.Ordinal);
        Assert.EndsWith("; fix or delete it, then set again\n", run.Stderr, StringComparison.Ordinal);
        Assert.Equal("not json", await File.ReadAllTextAsync(ConfigFile, TestContext.Current.CancellationToken));
    }

    public void Dispose() => _state.Dispose();
}
