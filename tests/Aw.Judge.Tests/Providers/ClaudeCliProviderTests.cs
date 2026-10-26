using System.Text.Json.Nodes;
using Aw.Judge.Core.Providers;
using Aw.Judge.Infrastructure.Providers;
using Aw.Judge.Testing;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.Providers;

[Lane(TestLane.Unit)]
public sealed class ClaudeCliProviderTests
{
    private readonly FakeProcessRunner _runner = new();

    [Fact]
    public async Task An_in_enum_structured_answer_is_decided_with_the_default_confidence()
    {
        Install(Questions.ClaudeStructured("""{"decision":"batch"}"""));

        var result = await Decide(Questions.Text);

        Assert.Equal(new ProviderDecided("batch", AgentAnswer.DefaultConfidence, "model"), result);
    }

    [Fact]
    public async Task It_runs_haiku_headless_with_the_schema_from_a_fresh_workspace_it_deletes()
    {
        Install(Questions.ClaudeStructured("""{"decision":"send"}"""));

        _ = await Decide(Questions.Text);

        var call = Assert.Single(_runner.Calls);
        var request = call.Request;
        Assert.Equal("claude", request.FileName);
        Assert.Equal(Questions.Budget, request.Timeout);
        Assert.Equal(Questions.Text.Prompt, request.StandardInput);
        Assert.Equal(["-p", "--model", "haiku", "--effort", "low"], request.Arguments.Take(5), StringComparer.Ordinal);
        Assert.Equal("", After(request.Arguments, "--tools"));
        Assert.Equal("", After(request.Arguments, "--setting-sources"));
        Assert.Equal("json", request.Arguments[^1]);
        Assert.DoesNotContain(request.Arguments, argument => argument.Contains("hello", StringComparison.Ordinal) || argument.Contains(Questions.Text.Prompt, StringComparison.Ordinal));
        var schema = JsonNode.Parse(After(request.Arguments, "--json-schema"))!;   // why: the flag's value is the schema JSON
        Assert.Equal("""["send","batch","drop"]""", schema["properties"]!["decision"]!["enum"]!.ToJsonString());   // why: the lax schema always has the decision enum
        Assert.Equal("1", request.Environment["AW_JUDGE_CHILD"]);
        Assert.Equal("0", request.Environment["MAX_THINKING_TOKENS"]);
        Assert.Contains("TYPESAFE_API_KEY", request.Unset, StringComparer.Ordinal);
        Assert.Empty(Assert.IsType<IReadOnlyList<string>>(call.WorkingDirectoryFiles, exactMatch: false));
        Assert.False(Directory.Exists(request.WorkingDirectory), "the workspace is deleted afterwards");
    }

    [Fact]
    public async Task A_model_and_effort_option_replaces_haiku_and_low_in_the_argv()
    {
        Install(Questions.ClaudeStructured("""{"decision":"send"}"""));

        _ = await new ClaudeCliProvider(_runner) { Options = new ClaudeCliOptions(Model: "opus", Effort: "high") }
            .Decide(Questions.Text, Questions.Input, Questions.Budget, TestContext.Current.CancellationToken);

        var request = Assert.Single(_runner.Calls).Request;
        Assert.Equal("opus", After(request.Arguments, "--model"));
        Assert.Equal("high", After(request.Arguments, "--effort"));
    }

    [Fact]
    public async Task Without_structured_output_the_result_text_is_read_as_json()
    {
        Install(Questions.ResultText("""{"decision":"drop"}"""));

        Assert.Equal(new ProviderDecided("drop", 1, "model"), await Decide(Questions.Text));
    }

    [Theory]
    [InlineData("""{"type":"result","structured_output":{"decision":"wake"}}""", "out-of-enum")]
    [InlineData("""{"type":"result","structured_output":{"verdict":"send"}}""", "unparseable-answer")]
    [InlineData("""{"type":"result","result":"I would send it."}""", "unparseable-answer")]
    [InlineData("""{"type":"result","is_error":true,"result":"rate limited"}""", "cli-reported-error")]
    [InlineData("Error: not logged in", "unparseable-output")]
    [InlineData("", "unparseable-output")]
    public async Task A_malformed_or_out_of_schema_answer_is_errored(string output, string reason)
    {
        Install(output);

        Assert.Equal(new ProviderErrored(reason), await Decide(Questions.Text));
    }

    [Fact]
    public async Task A_non_zero_exit_is_errored_with_its_code()
    {
        _ = _runner.With("claude", new FakeProgram { ExitCode = 1, StandardError = "boom" });

        Assert.Equal(new ProviderErrored("exit-1"), await Decide(Questions.Text));
    }

    [Fact]
    public async Task Past_the_budget_it_is_unavailable_with_timeout()
    {
        _ = _runner.With("claude", new FakeProgram { Runs = TimeSpan.FromMinutes(1) });

        Assert.Equal(new ProviderUnavailable(ProviderUnavailable.Timeout), await Decide(Questions.Text));
    }

    [Fact]
    public async Task A_cli_the_system_refuses_to_start_is_errored_so_health_counts_it()
    {
        _ = _runner.With("claude", new FakeProgram { FailsToStart = true });

        Assert.Equal(new ProviderErrored("spawn-failed"), await Decide(Questions.Text));
    }

    [Fact]
    public async Task Not_installed_is_unavailable() =>
        Assert.Equal(new ProviderUnavailable("not-installed"), await Decide(Questions.Text));

    [Fact]
    public async Task Declared_extras_ride_back_and_undeclared_fields_are_dropped()
    {
        Install(Questions.ClaudeStructured("""{"decision":"chosen","chosenIndex":2,"why":"x","confidence":0.4}"""));

        var result = Assert.IsType<ProviderDecided>(await Decide(Questions.WithIndex));

        Assert.Equal(0.4, result.Confidence);
        Assert.Equal("""{"chosenIndex":2}""", result.Extra?.ToJsonString());
    }

    [Theory]
    [InlineData("""{"decision":"chosen","chosenIndex":"2"}""")]
    [InlineData("""{"decision":"chosen","chosenIndex":1.5}""")]
    public async Task An_extra_of_the_wrong_type_is_errored(string answer)
    {
        Install(Questions.ClaudeStructured(answer));

        Assert.Equal(new ProviderErrored("extra-out-of-schema"), await Decide(Questions.WithIndex));
    }

    [Fact]
    public async Task An_image_question_runs_in_the_evidence_directory_with_only_the_read_tool()
    {
        Install(Questions.ClaudeStructured("""{"decision":"broken","reasons":["the button is cut off"]}"""));
        var evidence = Directory.CreateTempSubdirectory("judge-evidence-").FullName;

        var result = Assert.IsType<ProviderDecided>(await new ClaudeCliProvider(_runner).Decide(Questions.Image, Questions.Evidence(evidence), Questions.Budget, TestContext.Current.CancellationToken));

        Directory.Delete(evidence);
        Assert.Equal("""{"reasons":["the button is cut off"]}""", result.Extra?.ToJsonString());
        var request = Assert.Single(_runner.Calls).Request;
        Assert.Equal(evidence, request.WorkingDirectory);
        Assert.Equal("Read", After(request.Arguments, "--tools"));
        Assert.Equal("Read(./**)", After(request.Arguments, "--allowedTools"));
        Assert.Equal(Questions.Image.Prompt, request.StandardInput);
        Assert.Equal("json", request.Arguments[^1]);
    }

    [Fact]
    public async Task An_image_question_with_a_relative_evidence_directory_is_errored_without_a_run_or_a_throw()
    {
        var result = await new ClaudeCliProvider(_runner).Decide(Questions.Image, Questions.Evidence("relative/dir"), Questions.Budget, TestContext.Current.CancellationToken);

        Assert.Equal(new ProviderErrored("invalid-image-input"), result);
        Assert.Empty(_runner.Calls);
    }

    [Fact]
    public async Task An_image_question_without_evidence_paths_is_errored_without_a_run()
    {
        var result = await new ClaudeCliProvider(_runner).Decide(Questions.Image, new JsonObject { ["text"] = "x" }, Questions.Budget, TestContext.Current.CancellationToken);

        Assert.Equal(new ProviderErrored("invalid-image-input"), result);
        Assert.Empty(_runner.Calls);
    }

    [Fact]
    public void It_covers_every_class_including_image() =>
        Assert.Equal(ContentClasses.All.Order(), new ClaudeCliProvider(_runner).Classes.Order());

    internal static string After(IReadOnlyList<string> arguments, string flag)
    {
        var index = arguments.ToList().IndexOf(flag);
        Assert.True(index >= 0 && index + 1 < arguments.Count, $"{flag} is passed with a value");
        return arguments[index + 1];
    }

    private void Install(string stdout) => _ = _runner.With("claude", new FakeProgram { StandardOutput = stdout });

    private async Task<ProviderResult> Decide(QuestionRef question) =>
        await new ClaudeCliProvider(_runner).Decide(question, Questions.Input, Questions.Budget, TestContext.Current.CancellationToken);
}
