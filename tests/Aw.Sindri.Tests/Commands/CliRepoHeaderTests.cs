using Aw.Kernel;
using Aw.Sindri.Core.Auth;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Commands;

/// <summary>
/// #3727: the CLI looked up its repo (the profile file read, parsed and checked, then a `git` process) before every request, to fill X-Sindri-Repo. The workflow reads never use that header, so they skip the lookup
/// and nothing runs; every other verb keeps it exactly, so a session caller is still scoped to its repo.
/// </summary>
public sealed class CliRepoHeaderTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private sealed class CountingProfile : IApprovedProfile
    {
        public int Reads { get; private set; }

        public ValueTask<Result<LoadedProfile?>> Latest(CancellationToken cancellationToken)
        {
            Reads++;
            return ValueTask.FromResult<Result<LoadedProfile?>>(new LoadedProfile(new SindriProfile(1, "t", Repos: [new RepoEntry("api", "/work/api")]), ProfileHash.From(new string('a', 64)), "/p.yaml"));
        }
    }

    private sealed class CountingLocator : IRepoLocator
    {
        public int Locates { get; private set; }

        public ValueTask<RepoName?> Locate(string directory, IReadOnlyList<RepoEntry> repos, CancellationToken cancellationToken)
        {
            Locates++;
            return ValueTask.FromResult<RepoName?>(RepoName.From("api"));
        }

        public ValueTask<RepoPlacement?> Place(string directory, IReadOnlyList<RepoEntry> repos, CancellationToken cancellationToken) => ValueTask.FromResult<RepoPlacement?>(null);
    }

    private static async Task<(int Reads, int Locates, string? Header)> Send(params string[] args)
    {
        var profile = new CountingProfile();
        var locator = new CountingLocator();
        await using var provider = new ServiceCollection().AddSingleton<IHumanTokenStore>(InMemoryHumanTokenStore.WithToken()).AddSingleton<IApprovedProfile>(profile).AddSingleton<IRepoLocator>(locator).BuildServiceProvider();
        var headers = CommandTable.Headers(args, new AgentSession(Terminal: true, Prover: false), provider);
        using var request = new HttpRequestMessage(HttpMethod.Get, "http://127.0.0.1:1/sindri/x");

        await headers.Apply(request, Token);

        return (profile.Reads, locator.Locates, request.Headers.TryGetValues("X-Sindri-Repo", out var value) ? value.Single() : null);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("wf", "list", "--json")]
    [InlineData("wf", "show", "morning-check")]
    [InlineData("wf", "runs", "morning-check")]
    [InlineData("wf", "run-show", "morning-check@1:1")]
    [InlineData("wf", "blocks")]
    [InlineData("wf", "docs", "retry")]
    public async Task A_workflow_read_does_not_read_the_profile_or_run_git_and_sends_no_repo_header(params string[] args)
        => Assert.Equal((0, 0, null), await Send(args));

    [Theory, Trait("Lane", "unit")]
    [InlineData("queue")]
    [InlineData("items", "--json")]
    [InlineData("wf", "run", "morning-check")]
    [InlineData("wf", "create", "x")]
    [InlineData("status")]
    [InlineData("context", "search", "x")]
    [InlineData("todo", "add", "x")]
    public async Task Every_other_verb_still_looks_up_its_repo_and_sends_the_header(params string[] args)
        => Assert.Equal((1, 1, "api"), await Send(args));
}
