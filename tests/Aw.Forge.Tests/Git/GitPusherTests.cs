using Aw.Forge.Core;
using Aw.Forge.Core.Pulls;
using Aw.Forge.Infrastructure.Git;
using Aw.Redaction;

namespace Aw.Forge.Tests.Git;

/// <summary>GitPusher against a local bare repository in a temp directory: real git, no network.</summary>
[Trait("Lane", "integration")]
public sealed class GitPusherTests : IDisposable
{
    private static readonly Dictionary<string, string> _quiet = new(StringComparer.Ordinal)
    {
        ["GIT_CONFIG_NOSYSTEM"] = "1",
        ["GIT_CONFIG_GLOBAL"] = "/dev/null",
        ["GIT_TERMINAL_PROMPT"] = "0",
    };

    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("aw-forge-push-");
    private readonly GitPusher _pusher = new(Redactor.Default);
    private string? _first;
    private string? _second;

    private string Remote => Path.Combine(_root.FullName, "remote.git");

    private string Work => Path.Combine(_root.FullName, "work");

    public void Dispose() => _root.Delete(recursive: true);

    private static async Task<string> Git(string directory, params string[] arguments)
    {
        var ran = await ProcessRunner.Run(
            new ProcessSpec("git", ["-c", "user.name=t", "-c", "user.email=t@example.test", "-c", "commit.gpgsign=false", .. arguments], _quiet, directory, TimeSpan.FromSeconds(30)),
            TestContext.Current.CancellationToken);
        Assert.NotNull(ran);
        Assert.True(ran.ExitCode == 0, $"git {string.Join(' ', arguments)} failed: {ran.Error}");
        return ran.Output.Trim();
    }

    private async Task Arrange()
    {
        _ = Directory.CreateDirectory(Remote);
        _ = Directory.CreateDirectory(Work);
        _ = await Git(Remote, "init", "--quiet", "--bare");
        _ = await Git(Work, "init", "--quiet");
        _ = await Git(Work, "commit", "--quiet", "--allow-empty", "-m", "first");
        _first = await Git(Work, "rev-parse", "HEAD");
        _ = await Git(Work, "commit", "--quiet", "--allow-empty", "-m", "second");
        _second = await Git(Work, "rev-parse", "HEAD");
    }

    private PushRequest Request(string sha, string? lease, string? remote = null, string branch = "feature") =>
        new(Work, sha, remote ?? Remote, branch, lease);

    private Task<string> RemoteTip(string branch = "feature") => Git(Remote, "rev-parse", "refs/heads/" + branch);

    [Fact]
    public async Task A_new_branch_is_pushed_when_the_lease_says_it_must_not_exist()
    {
        await Arrange();

        var result = await _pusher.Push(Request(_first!, lease: null), authorizationHeader: null, TestContext.Current.CancellationToken);

        Assert.Equal(PushOutcome.Pushed, result.Value);
        Assert.Equal(_first, await RemoteTip());
    }

    [Fact]
    public async Task An_existing_branch_moves_when_the_lease_matches_its_tip()
    {
        await Arrange();
        _ = await _pusher.Push(Request(_first!, lease: null), authorizationHeader: null, TestContext.Current.CancellationToken);

        var result = await _pusher.Push(Request(_second!, lease: _first), authorizationHeader: null, TestContext.Current.CancellationToken);

        Assert.Equal(PushOutcome.Pushed, result.Value);
        Assert.Equal(_second, await RemoteTip());
    }

    [Fact]
    public async Task A_stale_lease_is_HeadMoved_and_leaves_the_branch_alone()
    {
        await Arrange();
        _ = await _pusher.Push(Request(_second!, lease: null), authorizationHeader: null, TestContext.Current.CancellationToken);

        var result = await _pusher.Push(Request(_first!, lease: _first), authorizationHeader: null, TestContext.Current.CancellationToken);

        Assert.Equal(PushOutcome.HeadMoved, result.Value);
        Assert.Equal(_second, await RemoteTip());
    }

    [Fact]
    public async Task A_new_branch_lease_fails_when_the_branch_already_exists()
    {
        await Arrange();
        _ = await _pusher.Push(Request(_first!, lease: null), authorizationHeader: null, TestContext.Current.CancellationToken);

        var result = await _pusher.Push(Request(_second!, lease: null), authorizationHeader: null, TestContext.Current.CancellationToken);

        Assert.Equal(PushOutcome.HeadMoved, result.Value);
        Assert.Equal(_first, await RemoteTip());
    }

    [Fact]
    public async Task A_refusing_pre_push_hook_in_the_repository_is_not_run()
    {
        await Arrange();
        var hook = Path.Combine(Work, ".git", "hooks", "pre-push");
        await File.WriteAllTextAsync(hook, "#!/bin/sh\necho hook ran > \"" + Path.Combine(_root.FullName, "hook-ran") + "\"\nexit 1\n", TestContext.Current.CancellationToken);
        Executable.Make(hook);

        var result = await _pusher.Push(Request(_first!, lease: null), authorizationHeader: null, TestContext.Current.CancellationToken);

        Assert.Equal(PushOutcome.Pushed, result.Value);
        Assert.False(File.Exists(Path.Combine(_root.FullName, "hook-ran")));
    }

    [Fact]
    public async Task A_repository_url_rewrite_does_not_redirect_the_push()
    {
        await Arrange();
        var decoy = Path.Combine(_root.FullName, "decoy.git");
        _ = await Git(Work, "config", "url." + decoy + ".insteadOf", Remote);

        var result = await _pusher.Push(Request(_first!, lease: null), authorizationHeader: null, TestContext.Current.CancellationToken);

        Assert.Equal(PushOutcome.Pushed, result.Value);
        Assert.Equal(_first, await RemoteTip());
        Assert.False(Directory.Exists(decoy));
    }

    [Fact]
    public async Task A_remote_that_cannot_be_reached_is_AW_FORGE_008()
    {
        await Arrange();

        var result = await _pusher.Push(Request(_first!, lease: null, remote: Path.Combine(_root.FullName, "missing.git")), authorizationHeader: null, TestContext.Current.CancellationToken);

        Assert.Equal(ForgeErrors.PushFailed.Code, result.Error.Code);
    }

    [Fact]
    public async Task The_credential_header_reaches_git_through_the_environment_and_is_in_no_output()
    {
        await Arrange();
        const string secret = "sekret-value-0002";
        var header = GitPusher.BasicHeader(secret);
        var encoded = header[(header.LastIndexOf(' ') + 1)..];
        var seen = Path.Combine(_root.FullName, "hook-env.txt");
        var hook = Path.Combine(Remote, "hooks", "pre-receive");
        _ = Directory.CreateDirectory(Path.GetDirectoryName(hook)!);
        // A pre-receive hook on the remote runs inside the push (a file:// remote, so git's own environment reaches it): it records
        // the header git was given and prints it to the push's stderr before refusing, the worst case for an echo.
        await File.WriteAllTextAsync(hook, "#!/bin/sh\necho \"$GIT_CONFIG_VALUE_0\" > \"" + seen + "\"\necho \"remote saw: $GIT_CONFIG_VALUE_0\" >&2\nexit 1\n", TestContext.Current.CancellationToken);
        Executable.Make(hook);

        var result = await _pusher.Push(Request(_first!, lease: null, remote: new Uri(Remote).AbsoluteUri), header, TestContext.Current.CancellationToken);

        Assert.Equal(header, (await File.ReadAllTextAsync(seen, TestContext.Current.CancellationToken)).Trim());
        Assert.Equal(ForgeErrors.PushFailed.Code, result.Error.Code);
        Assert.DoesNotContain(secret, result.Error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(encoded, result.Error.ToString(), StringComparison.Ordinal);
        Assert.Contains("remote saw", result.Error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Bad_input_is_AW_FORGE_001_before_git_runs()
    {
        await Arrange();
        var cancel = TestContext.Current.CancellationToken;

        var badSha = await _pusher.Push(Request("not-a-sha", lease: null), authorizationHeader: null, cancel);
        var badBranch = await _pusher.Push(Request(_first!, lease: null, branch: "a..b"), authorizationHeader: null, cancel);
        var optionRemote = await _pusher.Push(Request(_first!, lease: null, remote: "--receive-pack=evil"), authorizationHeader: null, cancel);
        var absentSha = await _pusher.Push(Request(new string('a', 40), lease: null), authorizationHeader: null, cancel);
        var notARepo = await _pusher.Push(new PushRequest(RepoDir: _root.FullName, Sha: _first!, Remote: Remote, Branch: "feature", LeaseSha: null), authorizationHeader: null, cancel);

        Assert.All([badSha, badBranch, optionRemote, absentSha, notARepo], r => Assert.Equal(ForgeErrors.Invalid.Code, r.Error.Code));
    }
}
