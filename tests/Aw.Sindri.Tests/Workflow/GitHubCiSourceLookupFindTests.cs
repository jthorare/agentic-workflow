using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Graph.Templates;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Infrastructure.Workflow;
using Aw.Sindri.Testing.Connections;

namespace Aw.Sindri.Tests.Workflow;

/// <summary>#3534: the CI source is a connected GitHub connection plus the first profile repo's github.com origin; every miss is null. Uses real git in a temp repo.</summary>
[Lane(TestLane.Integration)]
public sealed class GitHubCiSourceLookupFindTests : IDisposable
{
    private readonly DirectoryInfo _repo = Directory.CreateTempSubdirectory("aw-ci-lookup-");
    private readonly InMemoryConnectionStore _connections = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _repo.Delete(recursive: true);

    [Fact]
    public async Task No_connection_gives_no_source()
    {
        await Git("init", "-q");
        await Git("remote", "add", "origin", "https://github.com/acme/app.git");

        Assert.Null(await Lookup(withRepo: true).Find(Token));
    }

    [Fact]
    public async Task A_connection_that_needs_sign_in_gives_no_source()
    {
        _ = await _connections.Upsert(ConnectionSamples.Oauth(ConnectionSamples.Name("github", "work"), ConnectionStatus.NeedsSignIn), Token);
        await Git("init", "-q");
        await Git("remote", "add", "origin", "https://github.com/acme/app.git");

        Assert.Null(await Lookup(withRepo: true).Find(Token));
    }

    [Fact]
    public async Task A_profile_with_no_repo_gives_no_source()
    {
        _ = await _connections.Upsert(ConnectionSamples.Oauth(ConnectionSamples.Name("github", "work")), Token);

        Assert.Null(await Lookup(withRepo: false).Find(Token));
    }

    [Fact]
    public async Task A_repo_with_no_origin_or_a_non_github_origin_gives_no_source()
    {
        _ = await _connections.Upsert(ConnectionSamples.Oauth(ConnectionSamples.Name("github", "work")), Token);
        await Git("init", "-q");
        Assert.Null(await Lookup(withRepo: true).Find(Token));

        await Git("remote", "add", "origin", "https://gitlab.com/acme/app.git");
        Assert.Null(await Lookup(withRepo: true).Find(Token));
    }

    [Fact]
    public async Task A_connected_github_connection_and_a_github_origin_give_the_source_on_main_by_default()
    {
        _ = await _connections.Upsert(ConnectionSamples.Oauth(ConnectionSamples.Name("github", "work")), Token);
        await Git("init", "-q");
        await Git("remote", "add", "origin", "git@github.com:acme/app.git");

        var source = await Lookup(withRepo: true).Find(Token);

        Assert.Equal(new GitHubCiSource("github:work", "acme", "app", "main"), source);
    }

    [Fact]
    public async Task The_origin_heads_branch_is_the_default_branch()
    {
        _ = await _connections.Upsert(ConnectionSamples.Oauth(ConnectionSamples.Name("github", "work")), Token);
        await Git("init", "-q");
        await Git("remote", "add", "origin", "https://github.com/acme/app");
        await Git("symbolic-ref", "refs/remotes/origin/HEAD", "refs/remotes/origin/develop");

        var source = await Lookup(withRepo: true).Find(Token);

        Assert.Equal("develop", source?.Branch);
    }

    private GitHubCiSourceLookup Lookup(bool withRepo) => new(_connections, new Approved(withRepo ? [new RepoEntry("app", _repo.FullName)] : []));

    private async Task Git(params string[] args) => Assert.Equal(0, (await GitProcess.Run(_repo.FullName, args, Token)).Exit);

    private sealed class Approved(IReadOnlyList<RepoEntry> repos) : IApprovedProfile
    {
        public ValueTask<Result<LoadedProfile?>> Latest(CancellationToken cancellationToken)
            => ValueTask.FromResult<Result<LoadedProfile?>>(new LoadedProfile(new SindriProfile(1, "t", Repos: repos), ProfileHash.From(new string('a', 64)), "/p.yaml"));
    }
}
