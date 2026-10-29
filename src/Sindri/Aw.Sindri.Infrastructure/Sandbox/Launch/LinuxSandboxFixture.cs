using System.Security.Cryptography;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Infrastructure.Launch;

namespace Aw.Sindri.Infrastructure.Sandbox.Launch;

/// <summary>
/// The throw-away request Check again plans and self-checks (sandbox spec §8): a ReadOnly Step on a small git repository under '&lt;home&gt;/sandbox/fixture', with the box's
/// real host facts. The repository is made once; nothing of the owner's is read or written besides it.
/// </summary>
/// <param name="sindriHome">The real AW_SINDRI_HOME.</param>
/// <param name="environment">Reads an environment variable.</param>
/// <param name="processes">Runs git for the one 'git init'.</param>
/// <param name="machine">The machine's memory and cores.</param>
internal sealed class LinuxSandboxFixture(string sindriHome, Func<string, string?> environment, ISandboxProcesses processes, ISandboxMachine machine) : ISandboxFixture
{
    private const int FixturePort = 7480;

    private static readonly TimeSpan _gitTimeout = TimeSpan.FromSeconds(30);
    private static readonly LaunchId _launch = LaunchId.From("LCH-" + new string('f', 32));

    /// <inheritdoc/>
    public SandboxRequest? Request()
    {
        var root = Path.Combine(sindriHome, "sandbox", "fixture");
        var repo = Path.Combine(root, "repo");
        var worktree = Path.Combine(repo, ".claude", "worktrees", "sindri-fixture-1");
        var runDirectory = Path.Combine(root, "run");
        var runArea = Path.Combine(root, "area");
        var netDir = Path.Combine(NetParent(environment), "fixture");
        try
        {
            foreach (var directory in new[] { worktree, runDirectory, runArea, netDir })
            {
                _ = Directory.CreateDirectory(directory);
            }

            MakeSessionHome();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;   // no fixture on this box: the self-check is skipped, not failed
        }

        if (!Directory.Exists(Path.Combine(repo, ".git")) && !processes.Run("git", ["-C", repo, "init", "-q"], _gitTimeout).Succeeded)
        {
            return null;
        }

        return Build(repo, worktree, runDirectory, runArea, netDir);
    }

    /// <inheritdoc/>
    public SandboxRequest? WriteScopeRequest()
    {
        var root = Path.Combine(sindriHome, "sandbox", "fixture-write");
        var repo = Path.Combine(root, "repo");
        var worktree = Path.Combine(repo, ".claude", "worktrees", "sindri-fixture-2");
        var runDirectory = Path.Combine(root, "run");
        var runArea = Path.Combine(root, "area");
        var netDir = Path.Combine(NetParent(environment), "fixture-write");
        try
        {
            foreach (var directory in new[] { repo, runDirectory, runArea, netDir })
            {
                _ = Directory.CreateDirectory(directory);
            }

            MakeSessionHome();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        // why: the git canary commits and moves refs, so the fixture's worktree is a real one on the run branch (the ReadOnly fixture's is an empty directory).
        if (!Directory.Exists(worktree) && !MakeWorktree(repo, worktree))
        {
            return null;
        }

        return Build(repo, worktree, runDirectory, runArea, netDir) with { Policy = new StepPolicy([], StepPermissionMode.Default, worktree) };
    }

    /// <summary>Makes what the plan binds from Sindri's home, the way a launch's Stage does: the launches and approved folders, and the fixture launch's session home with a real control.json.</summary>
    private void MakeSessionHome()
    {
        OwnerOnly.EnsureDirectory(Path.Combine(sindriHome, "launches"));
        OwnerOnly.EnsureDirectory(Path.Combine(sindriHome, "approved"));
        // why: a record of its own, not a link to the real control.json: that file may not exist yet, and a missing bind source fails bwrap before any canary runs.
        var control = new ControlEndpoint(new Uri($"http://127.0.0.1:{FixturePort}"), Environment.ProcessId, Epoch: 0);
        _ = SessionHome.Create(sindriHome, _launch, Convert.ToHexString(RandomNumberGenerator.GetBytes(32)), control);   // a fresh throw-away secret; the same home each run, replaced by Create
    }

    private bool MakeWorktree(string repo, string worktree)
    {
        string[][] steps =
        [
            ["init", "-q", "-b", "main"],
            ["-c", "user.name=sindri", "-c", "user.email=sindri@localhost", "commit", "-q", "--allow-empty", "-m", "fixture"],
            ["worktree", "add", "-q", worktree, "-b", "sindri/fixture/1/work"],
        ];
        return steps.All(args => processes.Run("git", ["-C", repo, "-c", "commit.gpgsign=false", .. args], _gitTimeout).Succeeded);
    }

    private SandboxRequest Build(string repo, string worktree, string runDirectory, string runArea, string netDir) =>
        new(
            _launch,
            ItemId.From("ITM-" + new string('f', 32)),
            AgentId.From("claude-code"),
            RepoName.From("fixture"),
            Instance: null,
            new SandboxPaths(repo, Path.Combine(repo, ".git"), worktree, runDirectory, runArea),
            "sindri/fixture/1",
            new StepPolicy([], StepPermissionMode.ReadOnly, WritablePath: null),
            Attended: false,
            StepTimeout: TimeSpan.FromMinutes(5),
            LongMax: null,
            SlotHeld: false,
            new SandboxNetFacts(netDir, SindriPort: FixturePort, ProxyPort: 41234),
            BoxHostFacts.Read(environment, sindriHome, machine),
            Settings: null,
            Login: null);

    /// <summary>Gets the short directory for unix sockets: $XDG_RUNTIME_DIR/sindri, else /tmp/sindri-(uid) (a socket path is limited to 104 bytes).</summary>
    /// <param name="environment">Reads an environment variable.</param>
    /// <returns>The directory.</returns>
    internal static string NetParent(Func<string, string?> environment) =>
        environment("XDG_RUNTIME_DIR") is { Length: > 0 } runtime
            ? Path.Combine(runtime, "sindri")
            : "/tmp/sindri-" + Sessions.OwnUid.Read().ToString(System.Globalization.CultureInfo.InvariantCulture);
}
