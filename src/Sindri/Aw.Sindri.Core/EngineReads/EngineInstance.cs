using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Core.EngineReads;

/// <summary>One instance as the engine sees it (API spec §3.8, E-30).</summary>
/// <param name="Id">The opaque instance id.</param>
/// <param name="Address">The instance's address.</param>
/// <param name="Recipe">The recipe it runs.</param>
/// <param name="State">Its state, from the 'State' search attribute.</param>
/// <param name="WaitingOn">What it waits on; null when nothing is pending.</param>
/// <param name="Timers">Its running timers.</param>
/// <param name="Retries">The retries of its pending activity, if one retries.</param>
/// <param name="HistoryLength">The events in its Temporal history.</param>
/// <param name="Stuck">Whether it only waits on a signal or update older than five minutes, with no timer.</param>
public sealed record EngineInstance(WorkflowInstanceId Id, InstanceAddress Address, EngineRecipeRef Recipe, string? State, EngineWaitingOn? WaitingOn, IReadOnlyList<EngineTimer> Timers, EngineRetries? Retries, long HistoryLength, bool Stuck);
