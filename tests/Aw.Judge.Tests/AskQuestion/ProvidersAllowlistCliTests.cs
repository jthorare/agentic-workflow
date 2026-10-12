using Aw.Judge.Testing;
using static Aw.Judge.Tests.AskQuestion.AskQuestionHarness;

namespace Aw.Judge.Tests.AskQuestion;

// The providers allowlist: '--providers a,b' anywhere in argv, else AW_JUDGE_PROVIDERS; rules is always kept. It only
// limits questions, so only a question reads the list: one the judge can't read is a usage error there, and the other
// commands (health, the brief commands the hooks call) ignore it.
public sealed class ProvidersAllowlistCliTests : IDisposable
{
    private const string Progress = """{"text":"progress on the parser, half done","senderKind":"teammate"}""";

    private readonly TempStateDirectory _state = new();

    private JudgeCli Judge(ScriptedProvider provider, string? variable = null) => new(_state)
    {
        Providers = [provider],
        Environment = variable is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(StringComparer.Ordinal) { ["AW_JUDGE_PROVIDERS"] = variable },
    };

    [Theory, Trait("Lane", "integration")]
    [InlineData("--providers", "rules", "wake-gate")]
    [InlineData("wake-gate", "--providers", "rules")]
    public async Task The_flag_anywhere_in_argv_limits_the_chain(params string[] args)
    {
        var claude = Cli().Decides("batch", 0.9);

        var run = await Judge(claude).RunWithInput(Progress, args);

        Assert.Equal((2, "no-provider-decided"), (run.ExitCode, run.Json().GetProperty("reasonCode").GetString()));
        Assert.Empty(claude.Calls);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task AW_JUDGE_PROVIDERS_limits_the_chain_when_the_flag_is_absent()
    {
        var claude = Cli().Decides("batch", 0.9);

        var run = await Judge(claude, variable: "rules").RunWithInput(Progress, "wake-gate");

        Assert.Equal(2, run.ExitCode);
        Assert.Empty(claude.Calls);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_flag_wins_over_AW_JUDGE_PROVIDERS()
    {
        var run = await Judge(Cli().Decides("batch", 0.9), variable: "rules").RunWithInput(Progress, "--providers", "claude-cli", "wake-gate");

        Assert.Equal((0, "batch"), (run.ExitCode, run.Json().GetProperty("decision").GetString()));
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData(null, "--providers", "bogus", "wake-gate")]
    [InlineData(null, "wake-gate", "--providers")]
    [InlineData("bogus", "wake-gate")]
    [InlineData(",", "wake-gate")]
    public async Task A_list_the_judge_cant_read_is_a_usage_error_for_a_question(string? variable, params string[] args)
    {
        var claude = Cli().Decides("batch", 0.9);

        var run = await Judge(claude, variable).RunWithInput(Progress, args);

        Assert.Equal((64, ""), (run.ExitCode, run.Stdout));
        Assert.StartsWith("usage: judge ", run.Stderr, StringComparison.Ordinal);
        Assert.Empty(claude.Calls);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Other_commands_take_the_flag_too()
    {
        var run = await new JudgeCli(_state).Run("health", "--providers", "jev");

        Assert.Equal(0, run.ExitCode);
        Assert.Equal("ok", run.Json().GetProperty("status").GetString());
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_bad_AW_JUDGE_PROVIDERS_never_breaks_health_or_the_brief_commands()
    {
        var judge = Judge(Cli(), "bogus");

        var health = await judge.Run("health");
        var save = await judge.RunWithInput(
            """{"toolUseId":"tu-1","sessionId":"s1","promptId":"p1","dispatchName":null,"subagentType":"main","goal":"g","acceptanceCriteria":"a","proofCommand":"p"}""", "brief", "save");
        var map = await judge.Run("brief", "map-by-name", "builder-a");

        Assert.Equal((0, 0, 0), (health.ExitCode, save.ExitCode, map.ExitCode));
        Assert.Equal("ok", health.Json().GetProperty("status").GetString());
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_bad_flag_value_on_another_command_is_ignored()
    {
        var run = await new JudgeCli(_state).Run("health", "--providers", "bogus");

        Assert.Equal(0, run.ExitCode);
    }

    public void Dispose() => _state.Dispose();
}
