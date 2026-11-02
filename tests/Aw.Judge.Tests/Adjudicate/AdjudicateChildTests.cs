using System.Text.Json.Nodes;
using Aw.Judge.Core.Ids;
using Aw.Judge.Core.Labels;
using Aw.Judge.Core.Processes;
using Aw.Judge.Features.Adjudicate;
using Aw.Judge.Host;
using Aw.Judge.Infrastructure.Providers;
using Aw.Judge.Testing;
using Aw.Kernel.Json;
using Microsoft.Extensions.Time.Testing;
using ProviderQuestions = Aw.Judge.Tests.Providers.Questions;

namespace Aw.Judge.Tests.Adjudicate;

// The child 'judge adjudicate' spawns reads stored, untrusted text, so it gets nothing to act with: the real claude
// provider (adjudicator options) runs over a stub `claude`, and every spawned call is checked for the five properties
// the cutover spec names (§5.2, P5 exit): no tools, no project settings, no hooks, a working directory outside any repo,
// and a minimal environment with no tokens.
[Trait("Lane", "unit")]
public sealed class AdjudicateChildTests
{
    private static readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    private readonly FakeProcessRunner _runner = new();

    [Fact]
    public async Task The_child_has_no_tools_no_project_settings_no_hooks_a_scratch_directory_and_no_tokens()
    {
        _ = _runner.With("claude", new FakeProgram { StandardOutput = ProviderQuestions.ClaudeStructured("""{"decision":"ask"}""") });
        var store = new AdjudicateEvalStore().With(new EvalItem(EvalItemId.From("item-a"), "ask-check", """{"transcriptTail":"the tail"}""", "decision:a", "continue", DateTimeOffset.UnixEpoch));
        var provider = new ClaudeCliProvider(_runner) { Options = ClaudeCliOptions.Adjudicator };
        var service = new AdjudicateService(store, new FakeProviderCatalog(provider), new FakeInputRedactor(), new FakeTimeProvider(), AwJson.CreateOptions(JudgeJsonContext.Default));

        var result = await service.Handle(A.AdjudicateRequest().Build(), _ct);

        Assert.Equal(new AdjudicateResponse(1, 0, 0), result.Value);
        Assert.Equal(3, _runner.Calls.Count);
        foreach (var call in _runner.Calls)
        {
            AssertNoToolsSettingsOrHooks(call.Request);
            AssertScratchDirectoryAndEnvironment(call);
        }
    }

    [Fact]
    public async Task The_chains_claude_keeps_haiku_low_effort_and_the_inherited_environment()
    {
        _ = _runner.With("claude", new FakeProgram { StandardOutput = ProviderQuestions.ClaudeStructured("""{"decision":"send"}""") });

        _ = await new ClaudeCliProvider(_runner).Decide(ProviderQuestions.Text, ProviderQuestions.Input, ProviderQuestions.Budget, _ct);

        var request = Assert.Single(_runner.Calls).Request;
        Assert.Null(request.InheritOnly);
        Assert.Equal("0", request.Environment["MAX_THINKING_TOKENS"]);
        Assert.Contains("\"alwaysThinkingEnabled\":false", After(request.Arguments, "--settings"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_adjudicator_lets_the_model_think()
    {
        _ = _runner.With("claude", new FakeProgram { StandardOutput = ProviderQuestions.ClaudeStructured("""{"decision":"send"}""") });

        _ = await new ClaudeCliProvider(_runner) { Options = ClaudeCliOptions.Adjudicator }.Decide(ProviderQuestions.Text, ProviderQuestions.Input, ProviderQuestions.Budget, _ct);

        var request = Assert.Single(_runner.Calls).Request;
        Assert.DoesNotContain("MAX_THINKING_TOKENS", request.Environment.Keys, StringComparer.Ordinal);
        Assert.Contains("\"alwaysThinkingEnabled\":true", After(request.Arguments, "--settings"), StringComparison.Ordinal);
    }

    private static void AssertNoToolsSettingsOrHooks(ProcessRequest request)
    {
        var argv = request.Arguments;
        Assert.Equal("claude", request.FileName);
        Assert.Equal("opus", After(argv, "--model"));
        Assert.Equal("high", After(argv, "--effort"));

        // 1. no tools: the tool list is empty and nothing is allowed.
        Assert.Equal("", After(argv, "--tools"));
        Assert.DoesNotContain("--allowedTools", argv, StringComparer.Ordinal);
        Assert.DoesNotContain("--dangerously-skip-permissions", argv, StringComparer.Ordinal);

        // 2. no project settings (nor user or local ones, nor CLAUDE.md, nor MCP servers, nor slash commands).
        Assert.Equal("", After(argv, "--setting-sources"));
        Assert.Contains("--strict-mcp-config", argv, StringComparer.Ordinal);
        Assert.Equal("""{"mcpServers":{}}""", After(argv, "--mcp-config"));
        Assert.Contains("--disable-slash-commands", argv, StringComparer.Ordinal);
        Assert.Contains("--no-session-persistence", argv, StringComparer.Ordinal);

        // 3. no hooks.
        var settings = JsonNode.Parse(After(argv, "--settings"))!;   // why: the flag's value is the settings JSON
        Assert.True(settings["disableAllHooks"]!.GetValue<bool>());
        Assert.Equal("1", request.Environment["AW_JUDGE_CHILD"]);   // the recursion guard: every aw:* hook exits at once
    }

    private static void AssertScratchDirectoryAndEnvironment(ProcessCall call)
    {
        var request = call.Request;
        var argv = request.Arguments;

        // 4. a scratch working directory outside any repository, empty when the child starts.
        var directory = request.WorkingDirectory!;
        Assert.StartsWith(Path.GetFullPath(Path.GetTempPath()), Path.GetFullPath(directory), StringComparison.Ordinal);
        Assert.Empty(call.WorkingDirectoryFiles!);
        Assert.False(InsideARepository(directory), $"{directory} is inside a repository");

        // 5. a minimal environment: only the named variables are inherited, and no token is among them or added.
        Assert.NotNull(request.InheritOnly);
        Assert.Contains("PATH", request.InheritOnly, StringComparer.Ordinal);
        Assert.Contains("HOME", request.InheritOnly, StringComparer.Ordinal);
        foreach (var token in new[] { "GH_TOKEN", "GITHUB_TOKEN", "AW_FORGE_TOKEN", "ANTHROPIC_API_KEY", "TYPESAFE_API_KEY", "LINEAR_API_KEY" })
        {
            Assert.DoesNotContain(token, request.InheritOnly, StringComparer.Ordinal);
            Assert.DoesNotContain(token, request.Environment.Keys, StringComparer.Ordinal);
        }

        Assert.DoesNotContain(request.InheritOnly, name => name.Contains("TOKEN", StringComparison.OrdinalIgnoreCase) || name.Contains("KEY", StringComparison.OrdinalIgnoreCase) || name.Contains("SECRET", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("TYPESAFE_API_KEY", request.Unset, StringComparer.Ordinal);

        // The input only ever travels on stdin.
        Assert.DoesNotContain(argv, argument => argument.Contains("the tail", StringComparison.Ordinal));
        Assert.Contains("the tail", request.StandardInput, StringComparison.Ordinal);
    }

    private static string After(IReadOnlyList<string> argv, string flag) => argv[argv.Index().First(pair => string.Equals(pair.Item, flag, StringComparison.Ordinal)).Index + 1];

    private static bool InsideARepository(string directory)
    {
        for (var at = new DirectoryInfo(directory); at is not null; at = at.Parent)
        {
            if (Directory.Exists(Path.Combine(at.FullName, ".git")) || File.Exists(Path.Combine(at.FullName, ".git")))
            {
                return true;
            }
        }

        return false;
    }
}
