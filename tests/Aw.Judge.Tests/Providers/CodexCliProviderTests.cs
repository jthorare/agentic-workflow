using System.Text.Json.Nodes;
using Aw.Judge.Core.Providers;
using Aw.Judge.Infrastructure.Providers;
using Aw.Judge.Testing;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.Providers;

[Lane(TestLane.Unit)]
public sealed class CodexCliProviderTests
{
    private const string Started = """{"type":"thread.started","thread_id":"t1"}""";
    private const string Completed = """{"type":"turn.completed","usage":{"input_tokens":10}}""";

    private readonly FakeProcessRunner _runner = new();

    [Fact]
    public async Task The_last_agent_message_is_the_answer()
    {
        Install(Started, Message("""{"decision":"send"}"""), Message("""{"decision":"drop"}"""), Completed);

        Assert.Equal(new ProviderDecided("drop", 1, "model"), await Decide(Questions.Text));
    }

    [Fact]
    public async Task A_question_that_names_a_model_runs_that_model_instead_of_the_default()
    {
        Install(Message("""{"decision":"no-good-candidate","chosenIndex":null}"""));

        _ = await Decide(Questions.WithIndex with { Model = "gpt-6" });

        Assert.Equal("gpt-6", ClaudeCliProviderTests.After(Assert.Single(_runner.Calls).Request.Arguments, "--model"));
    }

    [Fact]
    public async Task It_passes_a_strict_schema_file_in_the_workspace_and_the_prompt_after_the_separator()
    {
        string? schemaText = null;
        _ = _runner.With("codex", request =>
        {
            schemaText = File.ReadAllText(ClaudeCliProviderTests.After(request.Arguments, "--output-schema"));
            return new FakeProgram { StandardOutput = Message("""{"decision":"no-good-candidate","chosenIndex":null}""") };
        });

        var result = await Decide(Questions.WithIndex);

        Assert.Equal(new ProviderDecided("no-good-candidate", 1, "model"), result);
        var call = Assert.Single(_runner.Calls);
        var arguments = call.Request.Arguments;
        Assert.Equal(["exec", "--model", CodexCliProvider.Model], arguments.Take(3), StringComparer.Ordinal);
        Assert.Equal(call.Request.WorkingDirectory, ClaudeCliProviderTests.After(arguments, "-C"));
        Assert.Equal(["schema.json"], call.WorkingDirectoryFiles ?? [], StringComparer.Ordinal);
        Assert.Contains("hooks", arguments, StringComparer.Ordinal);
        Assert.Equal(["--", "-"], arguments.TakeLast(2), StringComparer.Ordinal);
        Assert.Equal(Questions.WithIndex.Prompt, call.Request.StandardInput);
        var schema = JsonNode.Parse(schemaText!)!;   // why: the program read the file during the run
        Assert.Equal("""["decision","chosenIndex"]""", schema["required"]!.ToJsonString());   // why: strict mode lists every property
        Assert.False(schema["additionalProperties"]!.GetValue<bool>());   // why: strict mode sets it
        Assert.Equal("""["integer","null"]""", schema["properties"]!["chosenIndex"]!["type"]!.ToJsonString());   // why: extras are nullable
        Assert.False(Directory.Exists(call.Request.WorkingDirectory));
    }

    [Fact]
    public async Task A_failed_turn_without_a_message_is_a_cli_error()
    {
        Install(Started, """{"type":"turn.failed","error":{"message":"invalid_json_schema"}}""");

        Assert.Equal(new ProviderErrored("cli-reported-error"), await Decide(Questions.Text));
    }

    [Theory]
    [InlineData("garbage\nmore garbage", "unparseable-output")]
    [InlineData("""{"type":"item.completed","item":{"type":"agent_message","text":"send it"}}""", "unparseable-answer")]
    [InlineData("""{"type":"item.completed","item":{"type":"agent_message","text":"{\"decision\":\"wake\"}"}}""", "out-of-enum")]
    [InlineData("""{"type":"item.completed","item":{"type":"agent_message","text":"{\"decision\":\"send\",\"decision\":\"drop\"}"}}""", "unparseable-answer")]
    [InlineData("""{"type":"item.completed","item":{"type":"agent_message","text":"{\"decision\":\"\\ud800\"}"}}""", "unparseable-answer")]
    [InlineData("""{"type":"item.completed","item":{"type":"agent_message","text":"{\"decision\":\"send\",\"\\ud800\":1}"}}""", "unparseable-answer")]
    public async Task A_malformed_or_out_of_schema_answer_is_errored(string output, string reason)
    {
        Install(output);

        Assert.Equal(new ProviderErrored(reason), await Decide(Questions.Text));
    }

    [Fact]
    public async Task Images_are_attached_by_absolute_path_before_the_separator()
    {
        Install(Message("""{"decision":"ok","reasons":null}"""));
        var evidence = Path.Combine(Path.GetTempPath(), "judge-evidence");

        var result = await new CodexCliProvider(_runner).Decide(Questions.Image, Questions.Evidence(evidence, baseline: "before.png"), Questions.Budget, TestContext.Current.CancellationToken);

        Assert.Equal(new ProviderDecided("ok", 1, "model"), result);
        var arguments = Assert.Single(_runner.Calls).Request.Arguments.ToList();
        var separator = arguments.IndexOf("--");
        Assert.Equal(["--image", Path.Combine(evidence, "after.png"), "--image", Path.Combine(evidence, "before.png")], arguments[(separator - 4)..separator]);
        Assert.StartsWith(Questions.Image.Prompt, Assert.Single(_runner.Calls).Request.StandardInput, StringComparison.Ordinal);
    }

    private static string Message(string text) =>
        new JsonObject { ["type"] = "item.completed", ["item"] = new JsonObject { ["type"] = "agent_message", ["text"] = text } }.ToJsonString();

    private void Install(params string[] lines) => _ = _runner.With("codex", new FakeProgram { StandardOutput = string.Join('\n', lines) + "\n" });

    private async Task<ProviderResult> Decide(QuestionRef question) =>
        await new CodexCliProvider(_runner).Decide(question, Questions.Input, Questions.Budget, TestContext.Current.CancellationToken);
}
