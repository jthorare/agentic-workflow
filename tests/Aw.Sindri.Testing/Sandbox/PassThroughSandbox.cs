using Aw.Kernel;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Plan;

namespace Aw.Sindri.Testing.Sandbox;

/// <summary>
/// The test-only sandbox (sandbox spec SB12): Wrap returns the argv unchanged and SelfCheck passes. Only test hosts register it,
/// never the published product Host and never through a switch.
/// </summary>
public sealed class PassThroughSandbox : ISandbox
{
    /// <inheritdoc />
    public Result<SandboxCapabilities> Probe() => new SandboxCapabilities(SandboxMechanism.PassThrough, "test-only pass-through");

    /// <inheritdoc />
    public Result<SandboxPlan> Plan(SandboxRequest request) => SandboxPlanner.Build(SandboxMechanism.PassThrough, request);

    /// <inheritdoc />
    public Task<Result<Unit>> SelfCheck(SandboxPlan plan, CancellationToken ct) => Task.FromResult<Result<Unit>>(Unit.Value);

    /// <inheritdoc />
    public Result<IReadOnlyList<string>> Wrap(SandboxPlan plan, IReadOnlyList<string> argv) => Results.Success(argv);

    /// <inheritdoc />
    public void Discard(SandboxPlan plan)
    {
        // Wrap writes nothing, so there is nothing to remove.
    }
}
