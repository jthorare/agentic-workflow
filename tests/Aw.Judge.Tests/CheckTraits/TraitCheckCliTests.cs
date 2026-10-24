using Aw.Judge.Core.Ports;
using Aw.Judge.Infrastructure.Processes;
using Aw.Judge.Infrastructure.Traits;
using Aw.Judge.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Judge.Tests.CheckTraits;

// trait-check.sh branches on trait-check's own exit scheme (hook contract): 0 nothing to report or skipped, 2 findings (stderr
// for edit and stop, JSON lines on stdout for scan), 3 Jev unavailable, 64 usage. The command runs in process over a temp
// repo (its real git and trait files) and a stub Jev.
public sealed class TraitCheckCliTests : IDisposable
{
    private readonly TempStateDirectory _state = new();
    private readonly TraitRepo _repo = new();

    [Fact, Trait("Lane", "integration")]
    public async Task With_jev_excluded_the_check_exits_3()
    {
        var run = await Cli(CheckTraitsHarness.Violating()).Run("trait-check", "--stop", "--providers", "rules");

        Assert.Equal(new CliRun(3, "", "trait-check: jev unavailable (excluded)\n"), run);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task With_the_environment_excluding_jev_the_check_exits_3()
    {
        var cli = Cli(CheckTraitsHarness.Violating(), new Dictionary<string, string>(StringComparer.Ordinal) { ["AW_JUDGE_PROVIDERS"] = "rules" });

        Assert.Equal(3, (await cli.Run("trait-check", "--edit", "--file", "src/app.ts")).ExitCode);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task An_edit_of_a_file_no_trait_covers_exits_0_and_prints_nothing()
    {
        var run = await Cli(CheckTraitsHarness.Violating()).Run("trait-check", "--edit", "--file", "docs/readme.md", "--providers", "rules");

        Assert.Equal(new CliRun(0, "", ""), run);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_repo_without_traits_exits_0_even_when_jev_is_excluded()
    {
        File.Delete(Path.Combine(_repo.Root, ".agents", "traits", "no-todo.yaml"));

        var run = await Cli(CheckTraitsHarness.Violating()).Run("trait-check", "--stop", "--providers", "rules");

        Assert.Equal(new CliRun(0, "", ""), run);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Findings_exit_2_with_the_message_on_stderr_for_edit_and_json_lines_on_stdout_for_scan()
    {
        var cli = Cli(CheckTraitsHarness.Violating());

        var edit = await cli.Run("trait-check", "--edit", "--file", "src/app.ts");
        var scan = await cli.Run("trait-check", "--scan", "--path", "src");

        Assert.Equal((2, ""), (edit.ExitCode, edit.Stdout));
        Assert.StartsWith("trait-check: your last edit likely breaks 1 project trait. Fix each one", edit.Stderr, StringComparison.Ordinal);
        Assert.Contains("1. no-todo — src/app.ts lines 1-3 (p=0.90)\n   Rule: No TODO comments are left in source files.\n   Fix: Open an issue and link it.\n   Details: .agents/traits/no-todo.yaml", edit.Stderr, StringComparison.Ordinal);
        Assert.Equal(2, scan.ExitCode);
        Assert.Equal("{\"trait\":\"no-todo\",\"severity\":\"block\",\"path\":\"src/app.ts\",\"lines\":[1,3],\"p\":0.9}\n", scan.Stdout);
        Assert.Equal("trait-check: 1 finding from 1 Jev requests\n", scan.Stderr);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_stop_names_the_branch_in_its_message()
    {
        var run = await Cli(CheckTraitsHarness.Violating()).Run("trait-check", "--stop", "--base", "origin/main", "--min-p", "0.5", "--budget-ms", "5000");

        Assert.Equal(2, run.ExitCode);
        Assert.StartsWith("trait-check: before you finish, this branch's changes likely break 1 project trait marked severity: block.", run.Stderr, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData("--stop", "--file", "a.ts")]
    [InlineData("--edit", "--file", "a.ts", "--path", "src")]
    [InlineData("--stop", "--trait", "x")]
    [InlineData("--stop", "--budget-ms", "0")]
    [InlineData("--stop", "--budget-ms", "soon")]
    [InlineData("--stop", "--min-p", "1.5")]
    [InlineData("--scan", "--concurrency", "0")]
    [InlineData("--scan", "--stop")]
    [InlineData("--stop", "--base")]
    public async Task A_malformed_command_line_is_a_usage_error_before_the_check_runs(params string[] tail)
    {
        var jev = CheckTraitsHarness.Violating();

        var run = await Cli(jev).Run(["trait-check", .. tail]);

        Assert.Equal(64, run.ExitCode);
        Assert.StartsWith("usage: judge trait-check ", run.Stderr, StringComparison.Ordinal);
        Assert.Empty(jev.Asked);
    }

    public void Dispose()
    {
        _repo.Dispose();
        _state.Dispose();
    }

    private JudgeCli Cli(FakeBatchClassifier jev, IReadOnlyDictionary<string, string>? environment = null) => new(_state)
    {
        Environment = environment ?? new Dictionary<string, string>(StringComparer.Ordinal),
        Overrides = services => services
            .AddSingleton<IProcessRunner>(new SystemProcessRunner(Environment.GetEnvironmentVariable))
            .AddSingleton<ITraitFiles>(new FileTraitFiles(_repo.Root))
            .AddSingleton<IBatchClassifier>(jev),
    };
}
