using Aw.Kernel.Testing;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Setup;
using Aw.Sindri.Tests.Ledger;
using Aw.Sindri.Tests.Workflow.Effects;

namespace Aw.Sindri.Tests.AddRepo;

/// <summary>The git adapter of AddRepo against real repositories (setup-once spec §5.1): the main checkout, the folders that can never be a repo, and git that runs nothing the folder brings.</summary>
[Lane(TestLane.Integration)]
public sealed class GitRepoInspectorTests : IDisposable
{
    private readonly TempHome _home = new();
    private readonly List<IDisposable> _repos = [];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private string SindriHome => Path.Combine(_home.Path, "sindri");

    public void Dispose()
    {
        foreach (var repo in _repos)
        {
            repo.Dispose();
        }

        _home.Dispose();
    }

    private GitRepoInspector Inspector() => new(new SindriSettings(SindriHome, Path.Combine(SindriHome, "profile.yaml")));

    private async Task<TempGitRepo> Repo()
    {
        var repo = await TempGitRepo.Create();
        _repos.Add(repo);
        return repo;
    }

    [Fact]
    public async Task A_linked_worktree_resolves_to_the_main_checkout()
    {
        var main = await Repo();
        var worktree = Path.Combine(_home.Path, "wt");
        await TempGitRepo.GitIn(main.Path, "worktree", "add", "-q", worktree);

        var inspected = await Inspector().Inspect(worktree, Token);

        Assert.Equal(RealPath.Of(main.Path), inspected.Value.Checkout);
    }

    [Fact]
    public async Task A_folder_inside_a_checkout_resolves_to_the_checkout()
    {
        var main = await Repo();
        var inside = Path.Combine(main.Path, "src");
        _ = Directory.CreateDirectory(inside);

        var inspected = await Inspector().Inspect(inside, Token);

        Assert.Equal(RealPath.Of(main.Path), inspected.Value.Checkout);
    }

    [Fact]
    public async Task A_checkout_with_no_commit_has_a_branch_and_no_head()
    {
        var empty = await TempGitRepo.Create(commits: 0);
        _repos.Add(empty);

        var inspected = await Inspector().Inspect(empty.Path, Token);

        Assert.Equal(("main", null), (inspected.Value.Git.DefaultBranch, inspected.Value.Git.Head));
    }

    [Fact]
    public async Task A_checkout_inside_the_sindri_home_is_refused()
    {
        var inside = await TempGitRepo.Create();
        _repos.Add(inside);
        var inspector = new GitRepoInspector(new SindriSettings(Path.GetDirectoryName(inside.Path)!, Path.Combine(inside.Path, "..", "profile.yaml")));

        var inspected = await inspector.Inspect(inside.Path, Token);

        Assert.Equal("SND-SETUP-001", inspected.Error.Code);
        Assert.Contains("inside Sindri's own folder", inspected.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_checkout_that_contains_the_sindri_home_is_refused()
    {
        var outer = await Repo();
        var inspector = new GitRepoInspector(new SindriSettings(Path.Combine(outer.Path, ".sindri"), Path.Combine(outer.Path, ".sindri", "profile.yaml")));

        var inspected = await inspector.Inspect(outer.Path, Token);

        Assert.Equal("SND-SETUP-001", inspected.Error.Code);
        Assert.Contains("contains Sindri's own folder", inspected.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_filesystem_monitor_in_the_repo_config_is_never_run()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "the monitor is a shell script");
        var repo = await Repo();
        var marker = Path.Combine(_home.Path, "ran");
        var script = Path.Combine(_home.Path, "monitor.sh");
        await File.WriteAllTextAsync(script, $"#!/bin/sh\ntouch {marker}\n", Token);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(script, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
        _ = await repo.Git("config", "core.fsmonitor", script);
        _ = await repo.Git("config", "core.hooksPath", Path.Combine(_home.Path, "hooks"));

        var inspected = await Inspector().Inspect(repo.Path, Token);

        Assert.True(inspected.IsSuccess);
        Assert.False(File.Exists(marker));
    }

    [Fact]
    public async Task A_file_instead_of_a_folder_is_refused()
    {
        var file = Path.Combine(_home.Path, "file.txt");
        await File.WriteAllTextAsync(file, "x", Token);

        var inspected = await Inspector().Inspect(file, Token);

        Assert.Equal("SND-SETUP-001", inspected.Error.Code);
    }
}
