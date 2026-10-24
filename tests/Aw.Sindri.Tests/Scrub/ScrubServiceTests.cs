#pragma warning disable xUnit1004 // red until cutover unit 4.1, which removes each Skip
using Aw.Sindri.Features.Scrub;
using Aw.Sindri.Tests.Ledger;
using Aw.Sindri.Tests.Workflow.Effects;

namespace Aw.Sindri.Tests.Scrub;

/// <summary>'sindri scrub' (cutover spec §6.2 items 1 and 4): stdin redaction through Aw.Redaction, the staged scan, and the hook installer, against a real throwaway git repo.</summary>
public sealed class ScrubServiceTests
{
    private const string Red = "red until cutover unit 4.1";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact(Skip = Red), Trait("Lane", "unit")]
    public async Task Stdin_comes_back_redacted_and_the_secret_is_gone()
    {
        var done = await new ScrubService().Handle(new ScrubRequest(ScrubMode.Stdin, Text: $"key = {ScrubFixtures.Secret}\nplain\n"), Token);

        Assert.True(done.IsSuccess, done.IsError ? done.Error.Message : null);
        Assert.DoesNotContain(ScrubFixtures.Secret, done.Value.Output, StringComparison.Ordinal);
        Assert.Contains("[REDACTED:", done.Value.Output, StringComparison.Ordinal);
        Assert.Contains("plain", done.Value.Output, StringComparison.Ordinal);
    }

    [Fact(Skip = Red), Trait("Lane", "integration")]
    public async Task Staged_refuses_an_added_secret_with_SND_SCRUB_002()
    {
        using var repo = await TempGitRepo.Create();
        await File.WriteAllTextAsync(Path.Combine(repo.Path, "f.txt"), $"k = \"{ScrubFixtures.Secret}\"\n", Token);
        _ = await repo.Git("add", "f.txt");

        var done = await new ScrubService().Handle(new ScrubRequest(ScrubMode.Staged, Repo: repo.Path), Token);

        Assert.Equal("SND-SCRUB-002", done.Error.Code);
        Assert.Contains("f.txt:1", done.Error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(ScrubFixtures.Secret, done.Error.Message, StringComparison.Ordinal);
    }

    [Fact(Skip = Red), Trait("Lane", "integration")]
    public async Task Staged_reads_added_lines_only_so_removing_a_committed_secret_passes()
    {
        using var repo = await TempGitRepo.Create();
        var file = Path.Combine(repo.Path, "f.txt");
        await File.WriteAllTextAsync(file, $"k = \"{ScrubFixtures.Secret}\"\n", Token);
        _ = await repo.Git("add", "f.txt");
        _ = await repo.Git("-c", "user.email=t@example.invalid", "-c", "user.name=t", "commit", "-q", "--no-verify", "-m", "secret");
        await File.WriteAllTextAsync(file, "k = \"gone\"\n", Token);
        _ = await repo.Git("add", "f.txt");

        var done = await new ScrubService().Handle(new ScrubRequest(ScrubMode.Staged, Repo: repo.Path), Token);

        Assert.True(done.IsSuccess, done.IsError ? done.Error.Message : null);
    }

    [Fact(Skip = Red), Trait("Lane", "integration")]
    public async Task Staged_outside_a_repo_is_SND_SCRUB_004()
    {
        using var home = new TempHome();

        var done = await new ScrubService().Handle(new ScrubRequest(ScrubMode.Staged, Repo: home.Path), Token);

        Assert.Equal("SND-SCRUB-004", done.Error.Code);
    }

    [Fact(Skip = Red), Trait("Lane", "integration")]
    public async Task Staged_with_a_git_diff_that_fails_is_SND_SCRUB_005()
    {
        using var repo = await TempGitRepo.Create();
        await File.WriteAllTextAsync(Path.Combine(repo.Path, ".git", "index"), "not an index", Token);

        var done = await new ScrubService().Handle(new ScrubRequest(ScrubMode.Staged, Repo: repo.Path), Token);

        Assert.Equal("SND-SCRUB-005", done.Error.Code);
    }

    [Fact(Skip = Red), Trait("Lane", "integration")]
    public async Task Install_writes_the_v3_hook_where_git_says_with_the_given_bin_and_marks_it_executable()
    {
        using var repo = await TempGitRepo.Create();
        var expected = await repo.Git("rev-parse", "--path-format=absolute", "--git-path", "hooks/pre-commit");

        var done = await new ScrubService().Handle(new ScrubRequest(ScrubMode.InstallPreCommit, Repo: repo.Path, Bin: "/stable/sindri"), Token);

        Assert.True(done.IsSuccess, done.IsError ? done.Error.Message : null);
        Assert.Equal(expected, done.Value.HookPath);
        var text = await File.ReadAllTextAsync(expected, Token);
        Assert.Contains("# sindri-pre-commit v3", text, StringComparison.Ordinal);
        Assert.Contains("SINDRI='/stable/sindri'", text, StringComparison.Ordinal);
        if (!OperatingSystem.IsWindows())
        {
            Assert.True(File.GetUnixFileMode(expected).HasFlag(UnixFileMode.UserExecute));
        }
    }

    [Theory(Skip = Red), Trait("Lane", "integration")]
    [InlineData(ScrubFixtures.V2Hook)]
    [InlineData(ScrubFixtures.V1Hook)]
    public async Task Install_replaces_a_v2_or_v1_hook(string old)
    {
        using var repo = await TempGitRepo.Create();
        var hook = await repo.Git("rev-parse", "--path-format=absolute", "--git-path", "hooks/pre-commit");
        _ = Directory.CreateDirectory(Path.GetDirectoryName(hook)!);
        await File.WriteAllTextAsync(hook, old, Token);

        var done = await new ScrubService().Handle(new ScrubRequest(ScrubMode.InstallPreCommit, Repo: repo.Path, Bin: "/stable/sindri"), Token);

        Assert.True(done.IsSuccess, done.IsError ? done.Error.Message : null);
        var text = await File.ReadAllTextAsync(hook, Token);
        Assert.Contains("# sindri-pre-commit v3", text, StringComparison.Ordinal);
        Assert.DoesNotContain("shape", text, StringComparison.Ordinal);
        Assert.DoesNotContain("/old/sindri", text, StringComparison.Ordinal);
    }

    [Theory(Skip = Red), Trait("Lane", "integration")]
    [InlineData(ScrubFixtures.ForeignHook)]
    [InlineData(ScrubFixtures.MentionsMarker)]
    public async Task Install_refuses_a_foreign_hook_with_SND_SCRUB_003_and_leaves_it_alone(string foreign)
    {
        using var repo = await TempGitRepo.Create();
        var hook = await repo.Git("rev-parse", "--path-format=absolute", "--git-path", "hooks/pre-commit");
        _ = Directory.CreateDirectory(Path.GetDirectoryName(hook)!);
        await File.WriteAllTextAsync(hook, foreign, Token);

        var done = await new ScrubService().Handle(new ScrubRequest(ScrubMode.InstallPreCommit, Repo: repo.Path, Bin: "/stable/sindri"), Token);

        Assert.Equal("SND-SCRUB-003", done.Error.Code);
        Assert.Equal(foreign, await File.ReadAllTextAsync(hook, Token));
    }

    [Fact(Skip = Red), Trait("Lane", "integration")]
    public async Task Install_respects_core_hooksPath_by_writing_nothing_there_and_refusing_with_SND_SCRUB_003()
    {
        using var repo = await TempGitRepo.Create();
        var shared = Path.Combine(repo.Path, "shared-hooks");
        _ = await repo.Git("config", "core.hooksPath", shared);

        var done = await new ScrubService().Handle(new ScrubRequest(ScrubMode.InstallPreCommit, Repo: repo.Path, Bin: "/stable/sindri"), Token);

        Assert.Equal("SND-SCRUB-003", done.Error.Code);
        Assert.False(Directory.Exists(shared));
    }

    [Fact(Skip = Red), Trait("Lane", "integration")]
    public async Task Install_outside_a_repo_is_SND_SCRUB_004()
    {
        using var home = new TempHome();

        var done = await new ScrubService().Handle(new ScrubRequest(ScrubMode.InstallPreCommit, Repo: home.Path, Bin: "/stable/sindri"), Token);

        Assert.Equal("SND-SCRUB-004", done.Error.Code);
    }
}
#pragma warning restore xUnit1004
