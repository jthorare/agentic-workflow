using Aw.Forge.Core;
using Aw.Forge.Core.Pulls;
using Aw.Forge.Core.Ports;
using Aw.Forge.Core.Refs;
using Aw.Forge.Infrastructure;
using Aw.Forge.Infrastructure.Git;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Forge.Tests.Contract;

/// <summary>
/// The write side against a real GitHub scratch repository: push with a lease, open, find, reply once per marker, react once,
/// update, retarget and merge. It creates two branches and one pull request and deletes both branches at the end (closing the
/// pull request if the merge did not land). Skipped unless AW_FORGE_GITHUB_REPO, AW_FORGE_TOKEN and AW_FORGE_GITHUB_WRITE=1 are all
/// set: it writes to the repository, so it only runs when asked to, and only against a repository you do not mind writing to.
/// </summary>
[Trait("Lane", "heavy")]
public sealed class GitHubForgeWriteContractTests : IDisposable
{
    private readonly DirectoryInfo _work = Directory.CreateTempSubdirectory("aw-forge-ghwrite-");

    public void Dispose() => _work.Delete(recursive: true);

    [Fact]
    public async Task The_write_side_works_against_a_scratch_repository()
    {
        var repo = Environment.GetEnvironmentVariable("AW_FORGE_GITHUB_REPO");
        var credential = Environment.GetEnvironmentVariable("AW_FORGE_TOKEN");
        if (string.IsNullOrWhiteSpace(repo) || string.IsNullOrWhiteSpace(credential) || repo.Split('/') is not [var owner, var name]
            || Environment.GetEnvironmentVariable("AW_FORGE_GITHUB_WRITE") != "1")
        {
            Assert.Skip("set AW_FORGE_GITHUB_REPO (a scratch repository), AW_FORGE_TOKEN and AW_FORGE_GITHUB_WRITE=1 to run the GitHub write contract");
            return;
        }

        using var provider = new ServiceCollection()
            .AddForgeGitHubInfrastructure(Environment.GetEnvironmentVariable, new ForgeHosts(["github.com"]))
            .BuildServiceProvider();
        var forge = provider.GetRequiredService<IForge>();
        var scene = new Scene(new ForgeRepo("github.com", owner, name), GitPusher.BasicHeader(credential), TimeProvider.System.GetUtcNow().ToUnixTimeSeconds(), Path.Combine(_work.FullName, "repo"));
        try
        {
            var pull = await Arrange(forge, scene);
            await Conversation(forge, scene, pull);
            await PushAndMerge(forge, scene, pull);
        }
        finally
        {
            foreach (var branch in new[] { scene.Head, scene.Other })
            {
                _ = await TryRun(scene.Header, scene.Dir, ["push", "--quiet", "origin", "--delete", branch], CancellationToken.None);
            }
        }
    }

    private async Task<ForgeRef> Arrange(IForge forge, Scene scene)
    {
        var cancel = TestContext.Current.CancellationToken;
        _ = await Run(cancel, scene.Header, _work.FullName, "clone", "--quiet", scene.Remote, "repo");
        var main = Expect(await forge.DefaultBranch(scene.Repo, cancel));
        var mainTip = await Run(cancel, scene.Header, scene.Dir, "rev-parse", "HEAD");
        _ = await Run(cancel, scene.Header, scene.Dir, "checkout", "--quiet", "-b", scene.Head);
        _ = await Run(cancel, scene.Header, scene.Dir, "commit", "--quiet", "--allow-empty", "-m", "forge contract: one");
        scene.One = await Run(cancel, scene.Header, scene.Dir, "rev-parse", "HEAD");
        _ = await Run(cancel, scene.Header, scene.Dir, "commit", "--quiet", "--allow-empty", "-m", "forge contract: two");
        scene.Two = await Run(cancel, scene.Header, scene.Dir, "rev-parse", "HEAD");

        Assert.Equal(PushOutcome.Pushed, Expect(await forge.Push(new PushRequest(scene.Dir, scene.One, scene.Remote, scene.Head, LeaseSha: null), cancel)));
        Assert.Equal(PushOutcome.Pushed, Expect(await forge.Push(new PushRequest(scene.Dir, mainTip, scene.Remote, scene.Other, LeaseSha: null), cancel)));
        scene.Main = main;
        var body = ForgeMarker.Append("scratch pull request", scene.Marker);
        var pull = Expect(await forge.OpenPull(new OpenPullRequest(scene.Repo, scene.Head, main, scene.One, "Forge contract", body, Draft: false), cancel));
        Assert.Equal(pull, Expect(await forge.FindPull(new FindPullQuery(scene.Repo, scene.Head, Author: null, Marker: scene.Marker), cancel)));
        return pull;
    }

    private static async Task Conversation(IForge forge, Scene scene, ForgeRef pull)
    {
        var cancel = TestContext.Current.CancellationToken;
        var reply = new ReplyRequest(pull, "pull", "contract reply", $"r-{scene.Stamp}");
        var reaction = new ReactionRequest(pull, "pull", "+1", $"x-{scene.Stamp}");

        Assert.Equal(Delivery.Delivered, Expect(await forge.Reply(reply, cancel)));
        Assert.Equal(Delivery.AlreadyDelivered, Expect(await forge.Reply(reply, cancel)));
        Assert.Equal(Delivery.Delivered, Expect(await forge.React(reaction, cancel)));
        Assert.Equal(Delivery.AlreadyDelivered, Expect(await forge.React(reaction, cancel)));
    }

    private static async Task PushAndMerge(IForge forge, Scene scene, ForgeRef pull)
    {
        var cancel = TestContext.Current.CancellationToken;
        var lease = new PushRequest(scene.Dir, scene.Two, scene.Remote, scene.Head, LeaseSha: scene.One);

        Assert.Equal(PushOutcome.Pushed, Expect(await forge.Push(lease, cancel)));
        Assert.Equal(PushOutcome.HeadMoved, Expect(await forge.Push(lease, cancel)));
        _ = Expect(await forge.UpdatePull(new UpdatePullRequest(pull, "Forge contract, updated", Body: null), cancel));
        _ = Expect(await forge.Retarget(pull, scene.Other, cancel));
        _ = Expect(await forge.Retarget(pull, scene.Main, cancel));

        Assert.Equal(MergeStatus.HeadMoved, Expect(await forge.Merge(new MergeRequest(pull, MergeMethod.Squash, scene.One), cancel)).Status);
        var merged = Expect(await forge.Merge(new MergeRequest(pull, MergeMethod.Squash, scene.Two), cancel));
        Assert.True(merged.Status is MergeStatus.Merged or MergeStatus.Blocked, $"the merge on the current head is Merged, or Blocked by the scratch repository's rules, not {merged.Status}");
    }

    private sealed class Scene(ForgeRepo repo, string header, long stamp, string dir)
    {
        internal ForgeRepo Repo { get; } = repo;

        internal string Header { get; } = header;

        internal long Stamp { get; } = stamp;

        internal string Dir { get; } = dir;

        internal string Remote { get; } = $"https://github.com/{repo.Owner}/{repo.Name}.git";

        internal string Head { get; } = $"aw-forge-contract-{stamp}";

        internal string Other { get; } = $"aw-forge-contract-{stamp}-base";

        internal string Marker { get; } = $"contract-{stamp}";

        internal string One { get; set; } = string.Empty;

        internal string Two { get; set; } = string.Empty;

        internal string Main { get; set; } = string.Empty;
    }

    private static T Expect<T>(Aw.Kernel.Result<T> result)
    {
        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : string.Empty);
        return result.Value;
    }

    private static async Task<string> Run(CancellationToken cancel, string header, string directory, params string[] arguments)
    {
        var ran = await TryRun(header, directory, arguments, cancel);
        Assert.True(ran is { ExitCode: 0 }, $"git {arguments[0]} failed");
        return ran?.Output.Trim() ?? string.Empty;
    }

    private static Task<ProcessResult?> TryRun(string header, string directory, string[] arguments, CancellationToken cancel) =>
        ProcessRunner.Run(
            new ProcessSpec(
                "git",
                ["-c", "user.name=forge-contract", "-c", "user.email=forge-contract@example.test", "-c", "commit.gpgsign=false", .. arguments],
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["GIT_TERMINAL_PROMPT"] = "0",
                    ["GIT_CONFIG_COUNT"] = "1",
                    ["GIT_CONFIG_KEY_0"] = "http.extraHeader",
                    ["GIT_CONFIG_VALUE_0"] = header,
                },
                directory,
                TimeSpan.FromMinutes(2)),
            cancel);
}
