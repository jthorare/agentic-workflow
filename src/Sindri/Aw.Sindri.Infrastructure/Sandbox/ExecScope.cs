using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>How 'sandbox exec' wraps its command in the platform's scope: systemd-run on Linux, a process group on a Mac (#1936).</summary>
#pragma warning disable MA0182 // why: a red-commit stub, SandboxExec takes it in the fix (#1936)
internal static class ExecScope
{
    /// <summary>Gets the Linux scope: <see cref="SystemdScopeArgv.Start"/>.</summary>
    /// <param name="systemdRun">The systemd-run binary.</param>
    /// <returns>The scope function.</returns>
    internal static Func<LaunchId, ScopeLimits, IReadOnlyList<string>, IReadOnlyList<string>> Linux(string systemdRun) => throw new NotSupportedException();

    /// <summary>Gets the macOS scope: <see cref="ProcessGroupScopeArgv.Start"/>.</summary>
    /// <param name="pgroupsDirectory">The folder of the group files.</param>
    /// <returns>The scope function.</returns>
    internal static Func<LaunchId, ScopeLimits, IReadOnlyList<string>, IReadOnlyList<string>> Mac(string pgroupsDirectory) => throw new NotSupportedException();
}
#pragma warning restore MA0182
