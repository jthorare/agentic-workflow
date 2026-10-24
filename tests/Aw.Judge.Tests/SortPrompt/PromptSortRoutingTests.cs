using Aw.Judge.Core.Ids;
using Aw.Judge.Core.Providers;
using Aw.Judge.Features.SetPromptSort;
using Aw.Judge.Features.SortPrompt;
using Aw.Kernel.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Judge.Tests.SortPrompt;

// 'judge prompt-sort' is one route decided by the second word (cutover spec §5.2): none or 'scaffold' go to unit 6.1's
// handlers, a stubbed subcommand to the not-yet-built answer, anything else to the unknown-subcommand answer. The
// handlers are proven here with recording services in place of 6.1's, so these tests keep passing when 6.1 lands.
public sealed class PromptSortRoutingTests : IDisposable
{
    private readonly TempStateDirectory _state = new();

    [Fact, Trait("Lane", "integration")]
    public async Task An_unknown_subcommand_is_named_on_stderr_with_exit_1()
    {
        var run = await new JudgeCli(_state).RunWithInput("not read", "prompt-sort", "frobnicate", "--x");

        Assert.Equal(new CliRun(1, "", "judge: unknown prompt-sort subcommand: frobnicate\n"), run);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_subcommand_match_is_exact()
    {
        Assert.Equal(
            new CliRun(1, "", "judge: unknown prompt-sort subcommand: Why\n"),
            await new JudgeCli(_state).Run("prompt-sort", "Why", "d1"));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task No_subcommand_is_the_hook_path_and_hands_the_prompt_session_and_allowlist_to_the_sorter()
    {
        var sorter = new RecordingService<SortPromptRequest, SortPromptResponse>(new SortPromptResponse("scaffold note"));
        var cli = new JudgeCli(_state) { Overrides = services => services.AddScoped<IService<SortPromptRequest, SortPromptResponse>>(_ => sorter) };

        var run = await cli.RunWithInput("""{"prompt":"fix the login bug","sessionId":"sess-7"}""", "prompt-sort", "--providers", "rules");

        Assert.Equal(new CliRun(0, """{"context":"scaffold note"}""" + "\n", ""), run);
        var request = Assert.Single(sorter.Requests);
        Assert.Equal(("fix the login bug", SessionId.From("sess-7")), (request.Prompt, request.Session));
        Assert.Equal([ProviderName.Rules], request.Providers);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_hook_path_prints_nothing_when_no_scaffold_fires()
    {
        var sorter = new RecordingService<SortPromptRequest, SortPromptResponse>(new SortPromptResponse());
        var cli = new JudgeCli(_state) { Overrides = services => services.AddScoped<IService<SortPromptRequest, SortPromptResponse>>(_ => sorter) };

        var run = await cli.RunWithInput("""{"prompt":"hello","sessionId":""}""", "prompt-sort");

        Assert.Equal(new CliRun(0, "", ""), run);
        Assert.Null(Assert.Single(sorter.Requests).Session);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_hook_path_falls_back_to_the_calling_session()
    {
        var sorter = new RecordingService<SortPromptRequest, SortPromptResponse>(new SortPromptResponse());
        var cli = new JudgeCli(_state)
        {
            Environment = new Dictionary<string, string>(StringComparer.Ordinal) { ["AW_SESSION_ID"] = "from-env" },
            Overrides = services => services.AddScoped<IService<SortPromptRequest, SortPromptResponse>>(_ => sorter),
        };

        _ = await cli.RunWithInput("""{"prompt":"hello"}""", "prompt-sort");

        Assert.Equal(SessionId.From("from-env"), Assert.Single(sorter.Requests).Session);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Stdin_that_is_not_the_hook_record_is_invalid_input()
    {
        var run = await new JudgeCli(_state).RunWithInput("not json", "prompt-sort");

        Assert.Equal((1, ""), (run.ExitCode, run.Stdout));
        Assert.StartsWith("invalid input: ", run.Stderr, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_bad_allowlist_is_a_usage_error_before_the_sorter_runs()
    {
        var sorter = new RecordingService<SortPromptRequest, SortPromptResponse>(new SortPromptResponse());
        var cli = new JudgeCli(_state) { Overrides = services => services.AddScoped<IService<SortPromptRequest, SortPromptResponse>>(_ => sorter) };

        var run = await cli.RunWithInput("""{"prompt":"hello"}""", "prompt-sort", "--providers", "bogus");

        Assert.Equal(64, run.ExitCode);
        Assert.Empty(sorter.Requests);
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData("on", true)]
    [InlineData("off", false)]
    public async Task Scaffold_hands_the_switch_to_the_config_writer_and_prints_the_config(string word, bool on)
    {
        var writer = new RecordingService<SetPromptSortRequest, SetPromptSortResponse>(new SetPromptSortResponse(Core.Config.JudgeConfig.Default));
        var cli = new JudgeCli(_state) { Overrides = services => services.AddScoped<IService<SetPromptSortRequest, SetPromptSortResponse>>(_ => writer) };

        var run = await cli.Run("prompt-sort", "scaffold", "brief", word);

        Assert.Equal(0, run.ExitCode);
        Assert.Equal(new ScaffoldSwitch("brief", on), Assert.Single(writer.Requests).Scaffold);
        Assert.Contains("wake-gate", run.Stdout, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData("scaffold")]
    [InlineData("scaffold brief")]
    [InlineData("scaffold brief maybe")]
    [InlineData("scaffold brief on extra")]
    public async Task Scaffold_with_the_wrong_arguments_is_a_usage_error(string tail)
    {
        var run = await new JudgeCli(_state).Run(["prompt-sort", .. tail.Split(' ')]);

        Assert.Equal(new CliRun(64, "", "usage: judge prompt-sort scaffold <name> on|off\n"), run);
    }

    public void Dispose() => _state.Dispose();
}
