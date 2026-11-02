using Aw.Sindri.Core.Sandbox;

namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>IScopeControl over 'systemctl --user' (sandbox spec §3.3), run through the sandbox's process runner: an argument list, a timeout, no shell.</summary>
/// <param name="processes">Runs systemctl.</param>
/// <param name="systemctl">The systemctl program.</param>
internal sealed class SystemctlScopeControl(ISandboxProcesses processes, string systemctl = "systemctl") : IScopeControl
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(10);

    /// <inheritdoc />
    public Task<string?> ControlGroup(string unit, CancellationToken cancellationToken) => Show(unit, "ControlGroup", cancellationToken);

    /// <inheritdoc />
    public Task<string?> Result(string unit, CancellationToken cancellationToken) => Show(unit, "Result", cancellationToken);

    /// <inheritdoc />
    public Task<bool> Kill(string unit, string signal, CancellationToken cancellationToken) =>
        Task.Run(() => processes.Run(systemctl, ["--user", "kill", "--kill-whom=all", $"--signal={signal}", unit], _timeout).Succeeded, cancellationToken);

    /// <inheritdoc />
    public Task<bool> SetRuntimeMax(string unit, TimeSpan ceiling, CancellationToken cancellationToken)
    {
        var argv = RuntimeCeiling.Argv(systemctl, unit, ceiling);
        return Task.Run(() => processes.Run(argv[0], [.. argv.Skip(1)], _timeout).Succeeded, cancellationToken);
    }

    private Task<string?> Show(string unit, string property, CancellationToken cancellationToken) =>
        Task.Run(
            () =>
            {
                var run = processes.Run(systemctl, ["--user", "show", "-p", property, "--value", unit], _timeout);
                return run.Succeeded && run.Output.Trim() is { Length: > 0 } value ? value : null;
            },
            cancellationToken);
}
