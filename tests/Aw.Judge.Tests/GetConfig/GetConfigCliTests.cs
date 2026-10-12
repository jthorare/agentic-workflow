namespace Aw.Judge.Tests.GetConfig;

// The contract of 'judge config get': the effective config as JSON, exit 0.
public sealed class GetConfigCliTests : IDisposable
{
    private readonly TempStateDirectory _state = new();

    [Fact, Trait("Lane", "integration")]
    public async Task Config_get_prints_the_defaults_without_a_config_file()
    {
        var run = await new JudgeCli(_state).Run("config", "get");

        Assert.Equal((0, ""), (run.ExitCode, run.Stderr));
        var json = run.Json();
        var wakeGate = json.GetProperty("questions").GetProperty("wake-gate");
        Assert.True(wakeGate.GetProperty("enabled").GetBoolean());
        Assert.Equal(0.7, wakeGate.GetProperty("threshold").GetDouble());
        Assert.False(json.TryGetProperty("providers", out _));
        Assert.False(json.TryGetProperty("promptSort", out _));
        CliGolden.Match(run, "GetConfig/Golden/config-get-defaults.json");
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Config_get_prints_the_file_s_questions_and_blocks()
    {
        Directory.CreateDirectory(Path.Combine(_state.Path, "judge"));
        await File.WriteAllTextAsync(
            Path.Combine(_state.Path, "judge", "config.json"),
            """{"questions":{"ask-check":{"enabled":false,"threshold":0.6,"providers":["jev"]}},"providers":{"agentClis":["codex-cli"],"jev":false},"promptSort":{"budgetMs":800}}""",
            TestContext.Current.CancellationToken);

        var run = await new JudgeCli(_state).Run("config", "get");
        var json = run.Json();

        CliGolden.Match(run, "GetConfig/Golden/config-get-file.json");
        var askCheck = json.GetProperty("questions").GetProperty("ask-check");
        Assert.False(askCheck.GetProperty("enabled").GetBoolean());
        Assert.Equal("jev", askCheck.GetProperty("providers")[0].GetString());
        Assert.Equal("codex-cli", json.GetProperty("providers").GetProperty("agentClis")[0].GetString());
        Assert.False(json.GetProperty("providers").GetProperty("jev").GetBoolean());
        Assert.Equal(800, json.GetProperty("promptSort").GetProperty("budgetMs").GetDouble());
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData("not json")]
    [InlineData("""{"questions":{"wake-gate":{"enabled":"yes","threshold":0.5}}}""")]
    public async Task Config_get_prints_the_defaults_when_the_file_isnt_a_valid_config(string text)
    {
        Directory.CreateDirectory(Path.Combine(_state.Path, "judge"));
        await File.WriteAllTextAsync(Path.Combine(_state.Path, "judge", "config.json"), text, TestContext.Current.CancellationToken);

        var run = await new JudgeCli(_state).Run("config", "get");

        Assert.Equal(0, run.ExitCode);
        var wakeGate = run.Json().GetProperty("questions").GetProperty("wake-gate");
        Assert.True(wakeGate.GetProperty("enabled").GetBoolean());
        Assert.Equal(0.7, wakeGate.GetProperty("threshold").GetDouble());
    }

    public void Dispose() => _state.Dispose();
}
