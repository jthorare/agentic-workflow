using Aw.Judge.Core.Providers;
using Aw.Judge.Features.CheckTraits;
using Aw.Kernel.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Judge.Tests.CheckTraits;

// 'judge trait-check' reaches unit 6.2's handler with the hook's arguments (trait-check.sh: --edit --file <p>…, --stop).
// The handler is proven here with a recording service in place of 6.2's, so these tests keep passing when 6.2 lands.
public sealed class TraitCheckRoutingTests : IDisposable
{
    private readonly TempStateDirectory _state = new();

    [Fact, Trait("Lane", "integration")]
    public async Task Edit_hands_the_files_the_hook_names_to_the_check()
    {
        var (cli, check) = Cli();

        _ = await cli.Run("trait-check", "--edit", "--file", "src/a.ts", "--file", "src/b.ts");

        var request = Assert.Single(check.Requests);
        Assert.Equal(TraitCheckMode.Edit, request.Mode);
        Assert.Equal(["src/a.ts", "src/b.ts"], request.Files);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Stop_takes_an_optional_base_ref()
    {
        var (cli, check) = Cli();

        _ = await cli.Run("trait-check", "--stop", "--base", "origin/dev");

        var request = Assert.Single(check.Requests);
        Assert.Equal((TraitCheckMode.Stop, "origin/dev"), (request.Mode, request.BaseRef));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Scan_takes_paths_and_traits()
    {
        var (cli, check) = Cli();

        _ = await cli.Run("trait-check", "--scan", "--path", "src", "--path", "lib", "--trait", "no-todo");

        var request = Assert.Single(check.Requests);
        Assert.Equal(TraitCheckMode.Scan, request.Mode);
        Assert.Equal(["src", "lib"], request.Paths);
        Assert.Equal(["no-todo"], request.Traits);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_allowlist_reaches_the_check()
    {
        var (cli, check) = Cli();

        _ = await cli.Run("trait-check", "--stop", "--providers", "rules,claude-cli");

        Assert.Equal([ProviderName.Rules, ProviderName.ClaudeCli], Assert.Single(check.Requests).Providers);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_bad_allowlist_is_a_usage_error_before_the_check_runs()
    {
        var (cli, check) = Cli();

        var run = await cli.Run("trait-check", "--stop", "--providers", "bogus");

        Assert.Equal(64, run.ExitCode);
        Assert.Empty(check.Requests);
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData("")]
    [InlineData("--file a.ts")]
    public async Task Without_a_mode_it_is_a_usage_error(string tail)
    {
        string[] args = tail.Length == 0 ? ["trait-check"] : ["trait-check", .. tail.Split(' ')];

        var run = await new JudgeCli(_state).Run(args);

        Assert.Equal(new CliRun(64, "", "usage: judge trait-check --edit --file <path>… | --stop [--base <ref>] | --scan [--path <path>…] [--trait <name>…]\n"), run);
    }

    public void Dispose() => _state.Dispose();

    private (JudgeCli Cli, RecordingService<CheckTraitsRequest, CheckTraitsResponse> Check) Cli()
    {
        var check = new RecordingService<CheckTraitsRequest, CheckTraitsResponse>(new CheckTraitsResponse([]));
        return (new JudgeCli(_state) { Overrides = services => services.AddScoped<IService<CheckTraitsRequest, CheckTraitsResponse>>(_ => check) }, check);
    }
}
