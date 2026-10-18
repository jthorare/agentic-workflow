using Aw.Kernel;

namespace Aw.Sindri.Core.Sandbox.Plan;

/// <summary>
/// The confinement mechanism as a port (sandbox spec SB12). The product registers <see cref="UnavailableSandbox"/> until SB-5a;
/// the real adapters are bubblewrap (SB-2) and Seatbelt (SB-9); the test-only ones live in Aw.Sindri.Testing and are never in the product Host.
/// </summary>
public interface ISandbox
{
    /// <summary>Checks that this box can confine a session (cached per box by the caller, refreshed by Check again).</summary>
    /// <returns>The capabilities, or SND-LAUNCH-014.</returns>
    public Result<SandboxCapabilities> Probe();

    /// <summary>Builds the plan for one launch. Pure: no I/O.</summary>
    /// <param name="request">The launch.</param>
    /// <returns>The plan, or SND-LAUNCH-014/015/016/017.</returns>
    public Result<SandboxPlan> Plan(SandboxRequest request);

    /// <summary>Proves the plan inside the real sandbox with a canary per denial and per grant.</summary>
    /// <param name="plan">The plan.</param>
    /// <param name="ct">Cancels the work.</param>
    /// <returns>Success, or SND-LAUNCH-016 naming the canary.</returns>
    public Task<Result<Unit>> SelfCheck(SandboxPlan plan, CancellationToken ct);

    /// <summary>Wraps the agent's argv in the sandbox.</summary>
    /// <param name="plan">The plan.</param>
    /// <param name="argv">The agent's argv.</param>
    /// <returns>The wrapped argv, or SND-LAUNCH-014.</returns>
    public Result<IReadOnlyList<string>> Wrap(SandboxPlan plan, IReadOnlyList<string> argv);
}
