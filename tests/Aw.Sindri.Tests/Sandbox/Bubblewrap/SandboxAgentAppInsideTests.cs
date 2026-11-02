using System.Globalization;
using System.Security.Cryptography;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Infrastructure.Sandbox;
using Aw.Sindri.Infrastructure.Sandbox.Launch;
using Aw.Sindri.Testing.Sandbox;
using Aw.Sindri.Tests.Launch;
using Aw.Sindri.Tests.Sandbox.Launch;

namespace Aw.Sindri.Tests.Sandbox.Bubblewrap;

/// <summary>
/// SB-7 proof (sandbox spec §12 SB-7, §3.4 GitHub): a Step with 'sandbox.credentials.github: agent-app' gets a token the real mint got from a fake GitHub API, runs in the
/// real bubblewrap sandbox, pushes its run branch and is refused any other branch and a delete; the token is in no environment, argv, output or plan file, git's credential
/// helper hands it over for github.com, and a token renamed in from outside mid-run is read through the folder bind. Heavy lane: it needs bwrap, socat and git.
/// </summary>
[Lane(TestLane.Heavy)]
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class SandboxAgentAppInsideTests
{
    private const string RunBranch = "refs/heads/sindri/x/1/work";

    [Fact]
    public async Task An_agent_app_Step_pushes_only_its_run_branch_never_holds_the_token_and_sees_a_renewal()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "the bubblewrap sandbox is Linux-only");
        Assert.SkipUnless(BubblewrapBox.Unavailable is null, BubblewrapBox.Unavailable ?? string.Empty);
        Assert.SkipUnless(SandboxBox.GitAvailable, "git is not installed");
        using var box = SandboxBox.Create(BubblewrapBox.FixtureParent, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        using var fixture = await Fixture.Start(box);

        var ran = await RunAndRenew(fixture);

        var o = box.RunDirectory;
        AssertPushes(fixture, ran);
        Assert.Contains("only the run branch refs/heads/sindri/x/1 can be pushed", await Read(o + "/refused.err"), StringComparison.Ordinal);

        // The token is handed over by the helper for github.com only, and is nowhere else: not the Step's env, not any argv, not its output, not the plan file.
        Assert.Contains("username=x-access-token\npassword=" + fixture.First + "\n", await Read(o + "/cred.out"), StringComparison.Ordinal);
        Assert.DoesNotContain("ghs_proof", await Read(o + "/cred-other.out"), StringComparison.Ordinal);
        foreach (var file in new[] { o + "/env.out", o + "/ps.out", box.Root + "/inside.out", box.RunArea + "/sandbox-plan.json" })
        {
            Assert.DoesNotContain("ghs_proof", await Read(file), StringComparison.Ordinal);
        }

        // The renewal renamed a new file in (a new inode), and the running Step read it through the folder bind.
        Assert.NotEqual(ran.InodeBefore, Inode(fixture.TokenFile), StringComparer.Ordinal);
        Assert.Equal(ran.Second + "\n", await Read(o + "/second.out"));
        Assert.Contains("password=" + ran.Second + "\n", await Read(o + "/cred-second.out"), StringComparison.Ordinal);
        Assert.Equal(2, fixture.Api.Requests.Count);
        Assert.Empty(ProcessListing.Under(box.Root));
    }

    private static void AssertPushes(Fixture f, Ran ran)
    {
        Assert.True(ran.Exit == 0, "the script failed inside the sandbox: " + ran.Output);
        Assert.DoesNotContain("commit_failed", ran.Output, StringComparison.Ordinal);
        Assert.Contains("push_run=0\n", ran.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("push_default=0\n", ran.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("push_delete=0\n", ran.Output, StringComparison.Ordinal);
        Assert.Equal(Git("-C", f.Box.Worktree, "rev-parse", "HEAD"), Git("--git-dir", f.Remote, "rev-parse", RunBranch));
        Assert.Equal(f.DefaultBefore, Git("--git-dir", f.Remote, "rev-parse", f.DefaultBranch));
    }

    // Starts the Step, waits for its first half, renames a second minted token in (the renewal's own steps; AgentTokenRenewal's timer is proven with a fake clock), lets it finish.
    private static async Task<Ran> RunAndRenew(Fixture f)
    {
        var ct = TestContext.Current.CancellationToken;
        var sandbox = BubblewrapBox.Create();
        var plan = sandbox.Plan(f.Request);
        Assert.True(plan.IsSuccess, plan.IsError ? plan.Error.ToString() : null);
        await using var egress = StartEgress(plan.Value);
        var wrapped = sandbox.Wrap(plan.Value, ["/bin/sh", "-c", Script(f)]);
        Assert.True(wrapped.IsSuccess, wrapped.IsError ? wrapped.Error.ToString() : null);

        using var tmux = new PrivateTmux();
        var output = Path.Combine(f.Box.Root, "inside.out");
        var code = Path.Combine(f.Box.Root, "inside.code");
        var run = Path.Combine(f.Box.Root, "inside.sh");
        await File.WriteAllTextAsync(run, string.Join(' ', wrapped.Value.Select(Quote)) + $" > {Quote(output)} 2>&1; echo $? > {Quote(code)}", ct);   // why: a write-scope argv is longer than tmux's message limit, so tmux runs it from a file
        Assert.NotNull(tmux.Run("new-session", "-d", "-s", "agent-app-inside", "-c", f.Box.Root, "sh", run));

        await WaitFor(f.Box.RunDirectory + "/ready");
        var inode = Inode(f.TokenFile);
        var second = await f.Mint("ghs_proof_second");
        _ = TokenFile.Replace(f.SessionHome, second);
        await File.WriteAllTextAsync(f.Box.RunDirectory + "/go", string.Empty, ct);
        await WaitFor(code);
        return new Ran(int.Parse((await Read(code)).Trim(), CultureInfo.InvariantCulture), await Read(output), inode, second);
    }

    private static string Script(Fixture f)
    {
        var o = f.Box.RunDirectory;
        const string GitHub = "printf 'protocol=https\\nhost=github.com\\n\\n' | git credential fill";
        return $"cd {Quote(f.Box.Worktree)}\n"
            + "git commit -q --allow-empty -m 'step work' || echo commit_failed\n"   // why: a push of a commit the remote already has is 'up-to-date' and runs no hook
            + $"git push -q {Quote(f.Remote)} HEAD:{RunBranch} >/dev/null 2>&1; echo \"push_run=$?\"\n"
            + $"git push -q {Quote(f.Remote)} HEAD:{f.DefaultBranch} 2>{Quote(o + "/refused.err")}; echo \"push_default=$?\"\n"
            + $"git push -q {Quote(f.Remote)} :{RunBranch} >/dev/null 2>&1; echo \"push_delete=$?\"\n"
            + $"env > {Quote(o + "/env.out")}; ps -eo args > {Quote(o + "/ps.out")}\n"
            + $"{GitHub} > {Quote(o + "/cred.out")}; echo \"cred=$?\"\n"
            + $"printf 'protocol=https\\nhost=example.com\\n\\n' | GIT_TERMINAL_PROMPT=0 git credential fill > {Quote(o + "/cred-other.out")} 2>/dev/null; echo \"cred_other=$?\"\n"
            + $"touch {Quote(o + "/ready")}\n"
            + $"i=0; while [ ! -e {Quote(o + "/go")} ] && [ $i -lt 300 ]; do sleep 0.1; i=$((i+1)); done\n"
            + $"cat {Quote(f.TokenFile)} > {Quote(o + "/second.out")}\n"
            + $"{GitHub} > {Quote(o + "/cred-second.out")}\n"
            + "exit 0\n";
    }

    private static async Task WaitFor(string path)
    {
        for (var waited = 0; waited < 1200 && !File.Exists(path); waited++)
        {
            await Task.Delay(100, TestContext.Current.CancellationToken);
        }

        Assert.True(File.Exists(path), path + " did not appear in 120 s");
    }

    private static Task<string> Read(string path) => File.ReadAllTextAsync(path, TestContext.Current.CancellationToken);

    private static string Git(params string[] arguments) => SandboxBox.Run("git", arguments).Output.Trim();

    private static string Inode(string path) => SandboxBox.Run("stat", ["-c", "%i", path]).Output.Trim();

    private static RunningEgress StartEgress(SandboxPlan plan)
    {
        var network = new SystemEgressNetwork();
        var started = new EgressService(network, network, Environment.GetEnvironmentVariable, Path.Combine(plan.RunDirectory, "egress")).Start(plan.Network, EgressCaps.Default);
        Assert.True(started.IsSuccess, started.IsError ? started.Error.ToString() : null);
        return started.Value;
    }

    private static string Quote(string value) => "'" + value.Replace("'", "'\\''", StringComparison.Ordinal) + "'";

    private sealed record Ran(int Exit, string Output, string InodeBefore, string Second);

    // The agent-app Step's world: the real mint against a fake GitHub API with a throwaway App key, the first token in the session home, and a bare remote inside the
    // worktree (the only read-write path a push can reach) with its default branch recorded before the Step runs.
    private sealed class Fixture : IDisposable
    {
        private readonly RSA _key = RSA.Create(2048);
        private GitHubAppTokenSource? _source;

        private Fixture(SandboxBox box)
        {
            Box = box;
            Request = box.WriteScope() with { Settings = new SandboxSettings(Credentials: new SandboxCredentials(Github: GithubCredentials.AgentApp)) };
            SessionHome = SessionHomePaths.For(box.SindriHome, Request.Launch);
            Remote = box.Worktree + "/.proof-remote.git";
        }

        internal SandboxBox Box { get; }

        internal SandboxRequest Request { get; }

        internal string SessionHome { get; }

        internal string TokenFile => SessionHomePaths.GhTokenFile(SessionHome);

        internal string Remote { get; }

        internal FakeGitHubApi Api { get; } = new();

        internal string First { get; private set; } = string.Empty;

        internal string DefaultBranch { get; private set; } = string.Empty;

        internal string DefaultBefore { get; private set; } = string.Empty;

        internal static async Task<Fixture> Start(SandboxBox box)
        {
            var f = new Fixture(box);
            var keyFile = Path.Combine(box.Root, "app.pem");
            await File.WriteAllTextAsync(keyFile, f._key.ExportPkcs8PrivateKeyPem(), TestContext.Current.CancellationToken);
            var env = new Dictionary<string, string?>(StringComparer.Ordinal) { ["AGENT_APP_KEY_FILE"] = keyFile, ["AGENT_APP_API_URL"] = f.Api.Url };
            f._source = new GitHubAppTokenSource(name => env.GetValueOrDefault(name), GitHubAppTokenSource.CreateClient(), RealTime.Clock);
            f.First = await f.Mint("ghs_proof_first");
            _ = Infrastructure.Sandbox.Launch.TokenFile.Replace(f.SessionHome, f.First);

            Assert.Equal(0, SandboxBox.Run("git", ["clone", "-q", "--bare", box.Repo, f.Remote]).Exit);
            f.DefaultBranch = Git("--git-dir", f.Remote, "symbolic-ref", "HEAD");
            f.DefaultBefore = Git("--git-dir", f.Remote, "rev-parse", f.DefaultBranch);
            return f;
        }

        internal async Task<string> Mint(string token)
        {
            Api.Reply(201, $"{{\"token\":\"{token}\",\"expires_at\":\"2099-01-01T00:00:00Z\"}}");
            var minted = await _source!.Mint(Request.Launch, Request.Repo, TestContext.Current.CancellationToken);
            Assert.True(minted.IsSuccess, minted.IsError ? minted.Error.ToString() : null);
            return minted.Value.Value;
        }

        public void Dispose()
        {
            Api.Dispose();
            _key.Dispose();
        }
    }
}
