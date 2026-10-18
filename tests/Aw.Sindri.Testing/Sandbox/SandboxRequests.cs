using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Credentials;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Testing.Sandbox;

/// <summary>Builds sandbox plan requests with the fixed facts of a small fixture box; tests change one thing with 'with'.</summary>
public static class SandboxRequests
{
    /// <summary>The fixture owner's home.</summary>
    public const string Home = "/home/owner";

    /// <summary>The fixture state directory.</summary>
    public const string StateDir = "/home/owner/.agentic-workflow";

    /// <summary>The fixture sindri home.</summary>
    public const string SindriHome = "/home/owner/.agentic-workflow/sindri-app";

    /// <summary>The fixture repo checkout.</summary>
    public const string Repo = "/work/web";

    /// <summary>The fixture worktree.</summary>
    public const string Worktree = "/work/web/.claude/worktrees/sindri-x-1";

    /// <summary>The fixture run area.</summary>
    public const string RunArea = "/home/owner/.agentic-workflow/sindri-app/workflow/areas/run-1";

    /// <summary>The fixture run directory.</summary>
    public const string RunDirectory = "/home/owner/.agentic-workflow/sindri-app/workflow/runs/x/s/1";

    /// <summary>The box facts: 32 GiB, 8 cores.</summary>
    public static SandboxHostFacts Host { get; } = new(
        Home, StateDir, SindriHome, "/home/owner/repos/toolkit", 1000, 32L * 1024 * 1024 * 1024, 8, IsMacOs: false,
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["PATH"] = "/usr/local/bin:/usr/bin:/bin:/home/owner/.dotnet:/home/owner/.local/bin",
            ["LANG"] = "en_US.UTF-8",
            ["TERM"] = "xterm-256color",
            ["USER"] = "owner",
            ["SHELL"] = "/bin/bash",
            ["DOTNET_ROOT"] = "/home/owner/.dotnet",
            ["NVM_DIR"] = "/home/owner/.nvm",
            ["GH_TOKEN"] = "ghp_planted",
            ["AWS_SECRET_ACCESS_KEY"] = "planted",
            ["AW_HEAVY_LOCK_HELD"] = "1",
        },
        ["/home/other"]);

    /// <summary>A WriteScope Step of claude-code with an owner-copy login.</summary>
    public static SandboxRequest WriteScope { get; } = new(
        LaunchId.From("LCH-" + new string('a', 32)),
        ItemId.From("ITM-" + new string('b', 32)),
        AgentId.From("claude-code"),
        RepoName.From("web"),
        InstanceAddress.From("T-1/item/1"),
        new SandboxPaths(Repo, Repo + "/.git", Worktree, RunDirectory, RunArea),
        "sindri/x/1",
        new StepPolicy([], StepPermissionMode.Default, Worktree),
        Attended: false,
        StepTimeout: TimeSpan.FromMinutes(60),
        LongMax: null,
        SlotHeld: false,
        new SandboxNetFacts("/run/user/1000/sindri/ab12cd34", 7480, 41234),
        Host,
        Settings: null,
        Login: OwnerCopyLogin());

    /// <summary>A ReadOnly Step: no writable path.</summary>
    public static SandboxRequest ReadOnly { get; } = WriteScope with { Policy = new StepPolicy([], StepPermissionMode.ReadOnly, WritablePath: null) };

    /// <summary>An attended session (no Step policy), writing the item's worktree.</summary>
    public static SandboxRequest Attended { get; } = WriteScope with { Policy = null, Attended = true, StepTimeout = null, Instance = null };

    /// <summary>The prepared login the owner-copy source would give: the agent home read-only and the per-launch copy directory.</summary>
    /// <returns>The login.</returns>
    public static PreparedLogin OwnerCopyLogin() => new(
        ModelLoginSource.OwnerCopy,
        [
            new LoginMount(Home + "/.claude", Home + "/.claude", MountMode.ReadOnly),
            new LoginMount(RunArea + "/login", Home + "/.claude-run", MountMode.ReadWrite),
        ],
        new Dictionary<string, string>(StringComparer.Ordinal) { ["CLAUDE_CONFIG_DIR"] = Home + "/.claude-run" },
        [Home + "/.claude-run"],
        new LoginHandle(RunArea + "/login"));
}
