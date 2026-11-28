namespace Aw.Kernel.Telemetry;

/// <summary>Whether this process's <see cref="TelemetryRuntime"/> has started, registered beside it so a host (and a test) can ask without starting it (#3727).</summary>
public sealed class TelemetryStart
{
    /// <summary>Gets a value indicating whether the runtime has started: always true after AddKernelTelemetry; after AddDeferredKernelTelemetry, once something resolved it or a problem had to be written.</summary>
    public bool Started { get; internal set; }
}
