using System.Text.Json.Nodes;
using Aw.Judge.Core.Providers;
using Aw.Judge.Infrastructure.Providers;
using Aw.Judge.Testing;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.Providers;

/// <summary>
/// The usage-limit flag on ProviderErrored, per harness: set only from a channel the CLI marks as an error and only when the
/// message starts with the limit text; the reason code is whatever it was before.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class UsageLimitProviderTests
{
    private const string Echo = "Could not read /repo/notes/You've hit your session limit · resets 2:50am (UTC).md";

    private readonly FakeProcessRunner _runner = new();

    public static TheoryData<string> Claude => [.. UsageLimitStrings.Claude];

    // ---- Claude ----

    [Theory]
    [MemberData(nameof(Claude))]
    public async Task Claude_an_error_envelope_that_starts_with_the_limit_text_is_a_limit_with_the_old_reason(string message)
    {
        Install("claude", ClaudeEnvelope(isError: true, message));

        var result = Assert.IsType<ProviderErrored>(await Decide("claude"));

        Assert.True(result.UsageLimit);
        Assert.False(string.IsNullOrEmpty(result.LimitPattern));
        Assert.Equal("cli-reported-error", result.ReasonCode);
    }

    [Theory]
    [MemberData(nameof(Claude))]
    public async Task Claude_a_non_zero_exit_with_the_limit_text_on_standard_error_is_a_limit_with_the_old_reason(string message)
    {
        Install("claude", new FakeProgram { ExitCode = 1, StandardError = message + "\n" });

        var result = Assert.IsType<ProviderErrored>(await Decide("claude"));

        Assert.True(result.UsageLimit);
        Assert.Equal("exit-1", result.ReasonCode);
    }

    [Fact]
    public async Task Claude_a_non_zero_exit_that_prints_the_error_envelope_is_a_limit()
    {
        Install("claude", new FakeProgram { ExitCode = 1, StandardOutput = ClaudeEnvelope(isError: true, UsageLimitStrings.Claude[0]).StandardOutput });

        var result = Assert.IsType<ProviderErrored>(await Decide("claude"));

        Assert.True(result.UsageLimit);
        Assert.Equal("exit-1", result.ReasonCode);
    }

    [Fact]
    public async Task Claude_a_prompt_echoed_in_an_error_is_not_a_limit()
    {
        Install("claude", ClaudeEnvelope(isError: true, Echo));

        Assert.Equal(new ProviderErrored("cli-reported-error"), await Decide("claude"));
    }

    [Fact]
    public async Task Claude_a_successful_answer_that_says_it_is_not_searched()
    {
        Install("claude", ClaudeEnvelope(isError: false, UsageLimitStrings.Claude[0]));

        var result = Assert.IsType<ProviderErrored>(await Decide("claude"));

        Assert.False(result.UsageLimit);
        Assert.Equal("unparseable-answer", result.ReasonCode);
    }

    [Fact]
    public async Task Claude_a_structured_answer_is_decided_even_when_a_field_quotes_the_limit()
    {
        Install("claude", Questions.ClaudeStructured(new JsonObject { ["decision"] = "send", ["note"] = UsageLimitStrings.Claude[0] }.ToJsonString()));

        _ = Assert.IsType<ProviderDecided>(await Decide("claude"));
    }

    [Theory]
    [InlineData(1, "Killed", "exit-1")]
    [InlineData(137, "", "exit-137")]
    [InlineData(1, "Error: something about a limit", "exit-1")]
    public async Task Claude_a_crash_is_not_a_limit(int exit, string standardError, string reason)
    {
        Install("claude", new FakeProgram { ExitCode = exit, StandardError = standardError });

        Assert.Equal(new ProviderErrored(reason), await Decide("claude"));
    }

    [Fact]
    public async Task Claude_standard_error_that_echoes_the_prompt_is_not_a_limit()
    {
        Install("claude", new FakeProgram { ExitCode = 1, StandardError = Echo });

        Assert.Equal(new ProviderErrored("exit-1"), await Decide("claude"));
    }

    [Fact]
    public async Task Claude_a_timeout_is_unavailable_and_not_a_limit()
    {
        _ = _runner.With("claude", new FakeProgram { Runs = TimeSpan.FromMinutes(5), StandardError = UsageLimitStrings.Claude[0] });

        Assert.Equal(new ProviderUnavailable("timeout"), await Decide("claude"));
    }

    [Fact]
    public async Task Claude_bad_json_is_not_a_limit()
    {
        Install("claude", new FakeProgram { StandardOutput = UsageLimitStrings.Claude[0] });

        Assert.Equal(new ProviderErrored("unparseable-output"), await Decide("claude"));
    }

    // ---- Codex ----

    [Fact]
    public async Task Codex_an_error_event_with_the_limit_info_is_a_limit_with_the_old_reason()
    {
        Install("codex", CodexOutput(CodexError(UsageLimitStrings.CodexMessage, UsageLimitStrings.CodexErrorInfo)));

        var result = Assert.IsType<ProviderErrored>(await Decide("codex"));

        Assert.True(result.UsageLimit);
        Assert.False(string.IsNullOrEmpty(result.LimitPattern));
        Assert.Equal("cli-reported-error", result.ReasonCode);
    }

    [Fact]
    public async Task Codex_a_failed_turn_after_the_error_event_is_still_a_limit()
    {
        Install("codex", CodexOutput(
            CodexError(UsageLimitStrings.CodexMessage, UsageLimitStrings.CodexErrorInfo),
            TurnFailed(UsageLimitStrings.CodexMessage)));

        Assert.True(Assert.IsType<ProviderErrored>(await Decide("codex")).UsageLimit);
    }

    [Fact]
    public async Task Codex_a_turn_failed_event_without_info_matches_its_anchored_message()
    {
        Install("codex", CodexOutput(TurnFailed(UsageLimitStrings.CodexMessage)));

        Assert.True(Assert.IsType<ProviderErrored>(await Decide("codex")).UsageLimit);
    }

    [Fact]
    public async Task Codex_an_error_event_without_info_matches_its_anchored_message()
    {
        Install("codex", CodexOutput(CodexError(UsageLimitStrings.CodexMessage, errorInfo: null)));

        Assert.True(Assert.IsType<ProviderErrored>(await Decide("codex")).UsageLimit);
    }

    [Fact]
    public async Task Codex_another_error_info_is_not_a_limit_even_when_the_message_looks_like_one()
    {
        Install("codex", CodexOutput(CodexError(UsageLimitStrings.CodexMessage, "context_window_exceeded")));

        Assert.Equal(new ProviderErrored("cli-reported-error"), await Decide("codex"));
    }

    [Fact]
    public async Task Codex_a_prompt_echoed_in_an_error_is_not_a_limit()
    {
        Install("codex", CodexOutput(CodexError(Echo, errorInfo: null)));

        Assert.Equal(new ProviderErrored("cli-reported-error"), await Decide("codex"));
    }

    [Fact]
    public async Task Codex_an_answer_that_quotes_the_limit_is_decided()
    {
        Install("codex", CodexOutput(AgentMessage("""{"decision":"send","note":"You've hit your usage limit."}""")));

        _ = Assert.IsType<ProviderDecided>(await Decide("codex"));
    }

    [Fact]
    public async Task Codex_an_agent_message_that_only_says_it_is_not_searched()
    {
        Install("codex", CodexOutput(AgentMessage(UsageLimitStrings.CodexMessage)));

        var result = Assert.IsType<ProviderErrored>(await Decide("codex"));

        Assert.False(result.UsageLimit);
        Assert.Equal("unparseable-answer", result.ReasonCode);
    }

    [Fact]
    public async Task Codex_a_non_zero_exit_with_the_limit_text_on_standard_error_is_a_limit()
    {
        Install("codex", new FakeProgram { ExitCode = 1, StandardError = UsageLimitStrings.CodexMessage });

        var result = Assert.IsType<ProviderErrored>(await Decide("codex"));

        Assert.True(result.UsageLimit);
        Assert.Equal("exit-1", result.ReasonCode);
    }

    [Fact]
    public async Task Codex_a_crash_and_bad_output_are_not_limits()
    {
        Install("codex", new FakeProgram { ExitCode = 2, StandardError = "panic" });
        Assert.Equal(new ProviderErrored("exit-2"), await Decide("codex"));

        Install("codex", CodexOutput("garbage"));
        Assert.Equal(new ProviderErrored("unparseable-output"), await Decide("codex"));
    }

    [Fact]
    public async Task Codex_a_timeout_is_unavailable_and_not_a_limit()
    {
        _ = _runner.With("codex", new FakeProgram { Runs = TimeSpan.FromMinutes(5), StandardError = UsageLimitStrings.CodexMessage });

        Assert.Equal(new ProviderUnavailable("timeout"), await Decide("codex"));
    }

    // ---- Cursor has no pattern yet ----

    [Fact]
    public async Task Cursor_an_error_envelope_is_never_flagged_until_its_pattern_is_captured()
    {
        Install("cursor-agent", ClaudeEnvelope(isError: true, UsageLimitStrings.Claude[0]));

        Assert.Equal(new ProviderErrored("cli-reported-error"), await Decide("cursor-agent"));
    }

    // ---- Usage ----

    [Fact]
    public async Task Claude_usage_and_the_resolved_model_ride_on_the_decision()
    {
        Install(
            "claude",
            """{"type":"result","is_error":false,"result":"","structured_output":{"decision":"send"},"usage":{"input_tokens":120,"cache_creation_input_tokens":5,"cache_read_input_tokens":7,"output_tokens":31},"modelUsage":{"claude-haiku-5-5":{"inputTokens":120,"outputTokens":31,"costUSD":0.001}}}""");

        var decided = Assert.IsType<ProviderDecided>(await Decide("claude"));

        Assert.Equal(new ProviderUsage(120, 31) { Model = "claude-haiku-5-5" }, decided.Usage);
    }

    [Fact]
    public async Task Claude_without_usage_has_none()
    {
        Install("claude", Questions.ClaudeStructured("""{"decision":"send"}"""));

        Assert.Null(Assert.IsType<ProviderDecided>(await Decide("claude")).Usage);
    }

    [Fact]
    public async Task Codex_usage_is_read_from_the_completed_turn()
    {
        Install("codex", CodexOutput(
            """{"type":"item.completed","item":{"type":"agent_message","text":"{\"decision\":\"send\"}"}}""",
            """{"type":"turn.completed","usage":{"input_tokens":900,"cached_input_tokens":512,"output_tokens":42,"reasoning_output_tokens":8}}"""));

        var decided = Assert.IsType<ProviderDecided>(await Decide("codex"));

        Assert.Equal(new ProviderUsage(900, 42), decided.Usage);
    }

    [Fact]
    public async Task Codex_without_a_completed_turn_has_no_usage()
    {
        Install("codex", CodexOutput("""{"type":"item.completed","item":{"type":"agent_message","text":"{\"decision\":\"send\"}"}}"""));

        Assert.Null(Assert.IsType<ProviderDecided>(await Decide("codex")).Usage);
    }

    [Fact]
    public async Task Cursor_usage_is_taken_when_its_envelope_has_it()
    {
        var envelope = JsonNode.Parse(Questions.ResultText("""{"decision":"send"}"""))!.AsObject();
        envelope["usage"] = JsonNode.Parse("""{"input_tokens":10,"output_tokens":3}""");
        Install("cursor-agent", envelope.ToJsonString());

        Assert.Equal(new ProviderUsage(10, 3), Assert.IsType<ProviderDecided>(await Decide("cursor-agent")).Usage);
    }

    // ---- Minimal environment ----

    [Theory]
    [InlineData("claude", null)]
    [InlineData("codex", "CODEX_HOME")]
    [InlineData("cursor-agent", "CURSOR_API_KEY")]
    public async Task A_minimal_environment_question_leaves_the_callers_tokens_behind(string binary, string? loginVariable)
    {
        Install(binary, binary == "codex" ? CodexOutput("garbage") : new FakeProgram { StandardOutput = "{}" });

        _ = await Decide(binary, minimal: true);

        var request = Assert.Single(_runner.Calls).Request;
        var inherited = Assert.IsType<IReadOnlyCollection<string>>(request.InheritOnly, exactMatch: false);
        Assert.DoesNotContain("GH_TOKEN", inherited, StringComparer.Ordinal);
        Assert.DoesNotContain("AW_FORGE_TOKEN", inherited, StringComparer.Ordinal);
        Assert.DoesNotContain("GH_TOKEN", request.Environment.Keys, StringComparer.Ordinal);
        Assert.DoesNotContain("AW_FORGE_TOKEN", request.Environment.Keys, StringComparer.Ordinal);
        Assert.Contains("PATH", inherited, StringComparer.Ordinal);
        Assert.Contains("HOME", inherited, StringComparer.Ordinal);
        if (loginVariable is not null)
        {
            Assert.Contains(loginVariable, inherited, StringComparer.Ordinal);
        }
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    [InlineData("cursor-agent")]
    public async Task Without_the_option_the_child_inherits_everything_as_before(string binary)
    {
        Install(binary, new FakeProgram { StandardOutput = "{}" });

        _ = await Decide(binary, minimal: false);

        Assert.Null(Assert.Single(_runner.Calls).Request.InheritOnly);
    }

    [Fact]
    public async Task A_claude_option_that_is_already_minimal_stays_minimal_without_the_question_flag()
    {
        Install("claude", new FakeProgram { StandardOutput = "{}" });

        _ = await new ClaudeCliProvider(_runner) { Options = ClaudeCliOptions.Adjudicator }.Decide(Questions.Text, Questions.Input, Questions.Budget, TestContext.Current.CancellationToken);

        Assert.NotNull(Assert.Single(_runner.Calls).Request.InheritOnly);
    }

    // ---- helpers ----

    private static FakeProgram ClaudeEnvelope(bool isError, string result) =>
        new() { StandardOutput = new JsonObject { ["type"] = "result", ["is_error"] = isError, ["result"] = result }.ToJsonString() };

    private static string CodexError(string message, string? errorInfo) =>
        new JsonObject { ["type"] = "error", ["message"] = message, ["codex_error_info"] = errorInfo }.ToJsonString();

    private static string TurnFailed(string message) =>
        new JsonObject { ["type"] = "turn.failed", ["error"] = new JsonObject { ["message"] = message } }.ToJsonString();

    private static string AgentMessage(string text) =>
        new JsonObject { ["type"] = "item.completed", ["item"] = new JsonObject { ["type"] = "agent_message", ["text"] = text } }.ToJsonString();

    private static FakeProgram CodexOutput(params string[] lines) => new() { StandardOutput = string.Join('\n', lines) + "\n" };

    private void Install(string binary, FakeProgram program) => _ = _runner.With(binary, program);

    private void Install(string binary, string stdout) => Install(binary, new FakeProgram { StandardOutput = stdout });

    private async Task<ProviderResult> Decide(string binary, bool minimal = false)
    {
        Aw.Judge.Core.Ports.IModelProvider provider = binary switch
        {
            "claude" => new ClaudeCliProvider(_runner),
            "codex" => new CodexCliProvider(_runner),
            _ => new CursorCliProvider(_runner, new FakeInputRedactor()),
        };
        return await provider.Decide(Questions.Text with { MinimalEnvironment = minimal }, Questions.Input, Questions.Budget, TestContext.Current.CancellationToken);
    }
}
