namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>The real scope control: 'systemctl --user' for the unit and the cgroup files for the counters (sandbox spec §3.3, spike item 4).</summary>
/// <param name="unit">The scope unit.</param>
/// <param name="processes">Runs systemctl.</param>
/// <param name="host">Reads the cgroup files.</param>
internal sealed class SystemdScope(string unit, ISandboxProcesses processes, IHostPaths host) : IScopeControl
{
    /// <inheritdoc />
    public ValueTask<ScopeSample?> Sample(CancellationToken cancellationToken) => throw new NotSupportedException($"{unit} {processes} {host}");

    /// <inheritdoc />
    public ValueTask<string?> Result(CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc />
    public ValueTask SetRuntimeMax(TimeSpan runtimeMax, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc />
    public ValueTask Kill(bool force, CancellationToken cancellationToken) => throw new NotSupportedException();
}
