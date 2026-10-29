using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Features.CheckSandbox;

/// <summary>The Steps Check again would resume: the instances parked on a sandbox code, oldest first, no more than the free session slots (sandbox spec §6, §8).</summary>
/// <param name="parks">The parked instances.</param>
/// <param name="slots">The free session slots.</param>
internal sealed class SandboxResumeList(ISandboxParks parks, ISandboxSlots slots)
{
    /// <summary>Lists them.</summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The instances to resume, none when no slot is free.</returns>
    public async ValueTask<IReadOnlyList<WorkflowInstanceId>> Next(CancellationToken cancellationToken) =>
        [.. (await NextParked(cancellationToken).ConfigureAwait(false)).Select(static p => p.Instance)];

    /// <summary>Lists them with what the parks know about each: when it parked and which item it runs for.</summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The parks to resume, in the same order as <see cref="Next"/>.</returns>
    public async ValueTask<IReadOnlyList<ParkedOnSandbox>> NextParked(CancellationToken cancellationToken)
    {
        var parked = await parks.Parked(cancellationToken).ConfigureAwait(false);
        if (parked.Count == 0)
        {
            return [];
        }

        var free = await slots.Free(cancellationToken).ConfigureAwait(false);
        return [.. parked.OrderBy(static p => p.Since).Take(Math.Max(free, 0))];
    }
}
