using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>What <see cref="Launcher"/> is composed of: the catalog, the host and viewer ports, and the facts SL8 and the config gate read.</summary>
/// <param name="Catalog">The agent catalog (a reloading one in the product: each launch reads the file as it is then).</param>
/// <param name="HostFor">The host for a launch: the tmux host for Steps (SL4), the configured one for human opens.</param>
/// <param name="Viewer">The terminal viewer (attended launches only).</param>
/// <param name="Options">Files and bounds.</param>
/// <param name="Time">The clock the stage bounds run on.</param>
internal sealed record LauncherParts(IAgentCatalog Catalog, Func<bool, ISessionHost> HostFor, ITerminalViewer Viewer, LauncherOptions Options, TimeProvider Time)
{
    /// <summary>The built-in entry's argvHash for an id (a bin-only override keeps it, SL8), or null for an id with no built-in.</summary>
    public Func<AgentId, string?> BuiltinHash { get; init; } = static _ => null;

    /// <summary>The approved profile's <c lang="text">agents:</c> block, read per Step launch.</summary>
    public Func<CancellationToken, ValueTask<AgentSettings?>> Approved { get; init; } = static _ => ValueTask.FromResult<AgentSettings?>(null);

    /// <summary>The launcher.json as read now (problems, dropped entries), or null when nothing is checked.</summary>
    public Func<LauncherConfig>? Config { get; init; }

    /// <summary>The scratch checkouts the holdout evaluator runs in (LN-LCH2).</summary>
    public IScratchCheckouts Scratch { get; init; } = new GitScratchCheckouts();

    /// <summary>The usage reader a catalog entry's 'usage' field names, or null when there is none: the probe reports the target as limited without one.</summary>
    public Func<string, IUsageReader?> Usage { get; init; } = static _ => null;

    /// <summary>The MCP endpoint and repo name for a session working in a repo path, or null when there is none to give (nothing is passed then).</summary>
    public Func<string, CancellationToken, ValueTask<McpTarget?>> McpFor { get; init; } = static (_, _) => ValueTask.FromResult<McpTarget?>(null);

    /// <summary>Records the end of the launch's session (exit) when the launch is live; Stop calls it after the host stopped the session.</summary>
    public Func<LaunchId, CancellationToken, ValueTask> EndLive { get; init; } = static (_, _) => ValueTask.CompletedTask;

    /// <summary>Marks a Step's working directory trusted for the agent CLI that asks (claude-code), before it starts; nothing by default.</summary>
    public IAgentTrust Trust { get; init; } = new NoAgentTrust();

    /// <summary>Finds an agent CLI the way the login shell does; null when it doesn't resolve.</summary>
    public Func<string, CancellationToken, ValueTask<string?>> ResolveBin { get; init; } = BinResolver.Resolve;
}
