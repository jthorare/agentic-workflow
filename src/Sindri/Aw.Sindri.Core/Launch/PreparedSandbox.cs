using Aw.Sindri.Core.Sandbox;

namespace Aw.Sindri.Core.Launch;

/// <summary>One launch's confinement, ready to run (<see cref="ISandboxLaunch.Prepare"/>). The plan itself stays with the sandbox adapter.</summary>
/// <param name="Summary">The plan's summary: the launch's <c lang="text">launch.started</c> sandbox field (<see cref="StartedLaunch.Sandbox"/>).</param>
/// <param name="Argv">The wrapped argv: the scope, then the sandbox, then the agent.</param>
/// <param name="SessionHome">The per-launch session home the agent sees as <c lang="text">AW_SINDRI_HOME</c> (§3.1).</param>
/// <param name="RunArea">The launch's run area (scratch, plan file, wrap limits file).</param>
public sealed record PreparedSandbox(SandboxSummary Summary, IReadOnlyList<string> Argv, string SessionHome, string RunArea);
