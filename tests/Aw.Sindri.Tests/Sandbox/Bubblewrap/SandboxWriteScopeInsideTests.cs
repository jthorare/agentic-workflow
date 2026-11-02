using Aw.Kernel.Testing;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Infrastructure.Sandbox;
using Aw.Sindri.Testing.Sandbox;
using Aw.Sindri.Tests.Launch;

namespace Aw.Sindri.Tests.Sandbox.Bubblewrap;

/// <summary>
/// SB-5b proof (sandbox spec §12 SB-5b): a Step with a write scope in the real bubblewrap sandbox, on a private tmux server. It commits on its run branch and the engine can read
/// the diff from outside, it can't move another ref or push, and the toolchain answers through PATH. Each fact runs one tight script inside the wrapped sandbox; output and exit
/// code go to files under the box. Heavy lane: it needs bwrap, socat and git.
/// </summary>
[Lane(TestLane.Heavy)]
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class SandboxWriteScopeInsideTests
{
    // The run branch the fixture's worktree is on (SandboxBox's namespace 'sindri/x/1' plus '/work').
    private const string RunBranch = "sindri/x/1/work";

    private static SandboxBox NewBox()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "the bubblewrap sandbox is Linux-only");
        Assert.SkipUnless(BubblewrapBox.Unavailable is null, BubblewrapBox.Unavailable ?? string.Empty);
        Assert.SkipUnless(SandboxBox.GitAvailable, "git is not installed");
        return SandboxBox.Create(BubblewrapBox.FixtureParent, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
    }

    [Fact]
    public async Task A_commit_inside_moves_the_run_branch_and_the_engine_reads_its_diff_after_the_session()
    {
        using var box = NewBox();
        var before = Git(box, "rev-parse", RunBranch);

        var ran = await RunInside(box, $"cd {Quote(box.Worktree)} && echo edited > edited.txt && git add edited.txt && git commit -q -m 'edit inside' && echo committed");

        Assert.True(ran.Exit == 0, "the commit inside the sandbox failed: " + ran.Output);
        Assert.NotEqual(before, Git(box, "rev-parse", RunBranch), StringComparer.Ordinal);
        Assert.Contains("edited.txt", Git(box, "diff", "--name-only", "main", RunBranch), StringComparison.Ordinal);   // what the engine reads once the session has ended
        Assert.Equal("edit inside", Git(box, "log", "-1", "--format=%s", RunBranch), StringComparer.Ordinal);
        Assert.Empty(ProcessListing.Under(box.Root));
    }

    [Fact]
    public async Task Another_ref_can_not_be_written_from_inside()
    {
        using var box = NewBox();
        var main = Git(box, "rev-parse", "main");
        var other = Git(box, "rev-parse", "other");

        // a commit first, so HEAD differs from every ref the script tries to move
        var script = $"cd {Quote(box.Worktree)} && echo x > x.txt && git add x.txt && git commit -q -m moved || exit 90\n"
            + "git update-ref refs/heads/main HEAD; echo \"update-main=$?\"\n"
            + "git update-ref refs/heads/other HEAD; echo \"update-other=$?\"\n"
            + "git branch created-inside; echo \"branch=$?\"\n"
            + "git tag created-inside; echo \"tag=$?\"\n";
        var ran = await RunInside(box, script);

        Assert.True(ran.Exit == 0, "the script could not commit on the run branch: " + ran.Output);
        foreach (var attempt in new[] { "update-main", "update-other", "branch", "tag" })
        {
            Assert.Matches($"{attempt}=[1-9]", ran.Output);
        }

        Assert.Equal(main, Git(box, "rev-parse", "main"), StringComparer.Ordinal);
        Assert.Equal(other, Git(box, "rev-parse", "other"), StringComparer.Ordinal);
        Assert.Empty(Git(box, "for-each-ref", "refs/heads/created-inside", "refs/tags"));
        Assert.Empty(ProcessListing.Under(box.Root));
    }

    [Fact]
    public async Task A_push_is_refused_by_the_run_areas_pre_push_hook()
    {
        using var box = NewBox();
        var remote = Path.Combine(box.RunDirectory, "remote.git");   // under the run directory, so it is reachable inside and the hook (which runs after connecting) is what stops the push
        Assert.Equal(0, SandboxBox.Run("git", ["init", "-q", "--bare", remote]).Exit);
        var main = Git(box, "rev-parse", "main");

        var ran = await RunInside(box, $"cd {Quote(box.Worktree)} && git push {Quote(remote)} HEAD:refs/heads/pushed; echo \"push=$?\"");

        Assert.Matches("push=[1-9]", ran.Output);
        Assert.Contains("git push is not available in a sandboxed session", ran.Output, StringComparison.Ordinal);   // RunAreaPreparer's refusing pre-push
        Assert.Empty(SandboxBox.Run("git", ["-C", remote, "for-each-ref"]).Output.Trim());
        Assert.Equal(main, Git(box, "rev-parse", "main"), StringComparer.Ordinal);
        Assert.Empty(ProcessListing.Under(box.Root));
    }

    [Fact]
    public async Task Dotnet_build_works_inside()
    {
        using var box = NewBox();
        Assert.SkipUnless(box.HasDotnet, "no .NET SDK on this box");

        // why: a project with no packages restores offline (the SDK's own packs), and its own Directory.Build.* and nuget.config stop the repo's from applying. Once the shims are
        // in the run area, PATH resolves dotnet to the heavy shim first, so this is the build a Step really runs.
        var probe = Path.Combine(box.Worktree, "probe");
        _ = Directory.CreateDirectory(probe);
        await File.WriteAllTextAsync(Path.Combine(probe, "Directory.Build.props"), "<Project />", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(probe, "Directory.Build.targets"), "<Project />", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(probe, "nuget.config"), "<configuration><packageSources><clear /></packageSources></configuration>", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(probe, "probe.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(probe, "Program.cs"), "System.Console.WriteLine(\"probe-ok\");", TestContext.Current.CancellationToken);

        var shimmed = LinkHeavy(box);
        var ran = await RunInside(box, $"command -v dotnet && cd {Quote(probe)} && dotnet build -nologo -v q && dotnet bin/Debug/net10.0/probe.dll");

        Assert.True(ran.Exit == 0, "dotnet build inside the sandbox failed: " + ran.Output);
        Assert.Contains("probe-ok", ran.Output, StringComparison.Ordinal);
        Assert.True(!shimmed || ran.Output.Contains("/shims/dotnet", StringComparison.Ordinal), "dotnet did not resolve to the heavy shim: " + ran.Output);
        Assert.Empty(ProcessListing.Under(box.Root));
    }

    [Fact]
    public async Task Sandbox_exec_of_the_launch_reads_the_worktree_but_can_not_write_it_and_has_no_launch_secret()
    {
        using var box = NewBox();
        Assert.Equal(0, (await RunInside(box, "true")).Exit);   // the launch exec reproduces ran first: it made the host folders the exec binds read-only
        var before = Git(box, "rev-parse", RunBranch);

        var script = $"touch {Quote(Path.Combine(box.Worktree, "exec.txt"))}; echo \"worktree=$?\"\n"
            + $"cat {Quote(Path.Combine(box.Worktree, ".git"))} > /dev/null; echo \"read=$?\"\n"
            + "touch /tmp/scratch; echo \"scratch=$?\"\n"
            + "env | grep -c '^AW_SINDRI_LAUNCH_SECRET=' | sed 's/^/secret=/'\n";
        var ran = await RunInside(box, script, plan => plan.ForExec(LaunchId.From("LCH-" + new string('e', 32)), Path.Combine(box.Root, "exec-area"), Path.Combine(Path.GetDirectoryName(plan.Network.NetDir)!, "eeeeeeeeeeee")).Value);

        Assert.True(ran.Exit == 0, "the exec script failed: " + ran.Output);
        Assert.Matches("worktree=[1-9]", ran.Output);
        Assert.Contains("read=0", ran.Output, StringComparison.Ordinal);
        Assert.Contains("scratch=0", ran.Output, StringComparison.Ordinal);
        Assert.Contains("secret=0", ran.Output, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(box.Worktree, "exec.txt")));
        Assert.Equal(before, Git(box, "rev-parse", RunBranch), StringComparer.Ordinal);
        Assert.Empty(ProcessListing.Under(box.Root));
    }

    // The box's state dir is a scratch one: copy the owner's heavy wrapper into it, as the real state dir has it, so the shim path runs. False when this box has no heavy.
    private static bool LinkHeavy(SandboxBox box)
    {
        var real = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".agentic-workflow", "bin-r1b");
        if (!File.Exists(Path.Combine(real, "heavy")))
        {
            return false;
        }

        var copy = Path.Combine(box.State, "bin-r1b", "heavy");   // a copy, not a link: the box sees only what the plan binds
        _ = Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
        File.Copy(Path.Combine(real, "heavy"), copy, overwrite: true);
        File.SetUnixFileMode(copy, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return true;
    }

    // Plans the box's write-scope request, starts the launch's egress forwarders and runs 'sh -c script' wrapped, on a private tmux server. Output and exit code come back through files.
    // 'reshape' turns the planned launch into what runs, such as 'sandbox exec''s reproduction of it.
    private static async Task<Ran> RunInside(SandboxBox box, string script, Func<SandboxPlan, SandboxPlan>? reshape = null)
    {
        using var tmux = new PrivateTmux();
        var ct = TestContext.Current.CancellationToken;
        var sandbox = BubblewrapBox.Create();
        var plan = sandbox.Plan(box.WriteScope());
        Assert.True(plan.IsSuccess, plan.IsError ? plan.Error.ToString() : null);

        var running = reshape is null ? plan.Value : reshape(plan.Value);
        await using var egress = StartEgress(running);
        var wrapped = sandbox.Wrap(running, ["/bin/sh", "-c", script]);
        Assert.True(wrapped.IsSuccess, wrapped.IsError ? wrapped.Error.ToString() : null);

        var output = Path.Combine(box.Root, "inside.out");
        var code = Path.Combine(box.Root, "inside.code");
        File.Delete(output);   // a second run in one box must not read the first one's files
        File.Delete(code);
        var line = string.Join(' ', wrapped.Value.Select(Quote)) + $" > {Quote(output)} 2>&1; echo $? > {Quote(code)}";
        var run = Path.Combine(box.Root, "inside.sh");
        await File.WriteAllTextAsync(run, line, ct);   // why: a write-scope argv is longer than tmux's message limit (about 16 KB), so tmux runs it from a file
        Assert.NotNull(tmux.Run("new-session", "-d", "-s", "write-scope-inside", "-c", box.Root, "sh", run));
        for (var waited = 0; waited < 1200 && !File.Exists(code); waited++)
        {
            await Task.Delay(100, ct);
        }

        Assert.True(File.Exists(code), "the script did not finish inside the sandbox in 120 s");
        return new Ran(int.Parse((await File.ReadAllTextAsync(code, ct)).Trim(), System.Globalization.CultureInfo.InvariantCulture), await File.ReadAllTextAsync(output, ct));
    }

    private static RunningEgress StartEgress(SandboxPlan plan)
    {
        var network = new SystemEgressNetwork();
        var started = new EgressService(network, network, Environment.GetEnvironmentVariable, Path.Combine(plan.RunDirectory, "egress")).Start(plan.Network, EgressCaps.Default);
        Assert.True(started.IsSuccess, started.IsError ? started.Error.ToString() : null);
        return started.Value;
    }

    // From outside the sandbox, in the box's real repository.
    private static string Git(SandboxBox box, params string[] args)
    {
        var (exit, output) = SandboxBox.Run("git", ["-C", box.Repo, .. args]);
        Assert.True(exit == 0, $"git {string.Join(' ', args)} failed: {output}");
        return output.Trim();
    }

    private static string Quote(string value) => "'" + value.Replace("'", "'\\''", StringComparison.Ordinal) + "'";

    private sealed record Ran(int Exit, string Output);
}
