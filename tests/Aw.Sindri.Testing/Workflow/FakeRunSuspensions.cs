using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Testing.Workflow;

/// <summary>An in-memory <see cref="IRunSuspensions"/>: the launches a test suspended are suspended, every other launch is not.</summary>
public sealed class FakeRunSuspensions : IRunSuspensions
{
    private readonly HashSet<LaunchId> _suspended = [];

    /// <summary>Gets or sets a value indicating whether the answer is unavailable (the control plane is down).</summary>
    public bool Unavailable { get; set; }

    /// <summary>Suspends <paramref name="launch"/>'s run.</summary>
    /// <param name="launch">The launch.</param>
    public void Suspend(LaunchId launch) => _ = _suspended.Add(launch);

    /// <summary>Resumes <paramref name="launch"/>'s run.</summary>
    /// <param name="launch">The launch.</param>
    public void Resume(LaunchId launch) => _ = _suspended.Remove(launch);

    /// <inheritdoc/>
    public ValueTask<Result<bool>> IsSuspended(LaunchId launch, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<bool>>(Unavailable ? SindriErrors.LaunchInterrupted.With("the run's state can't be read") : _suspended.Contains(launch));
}
