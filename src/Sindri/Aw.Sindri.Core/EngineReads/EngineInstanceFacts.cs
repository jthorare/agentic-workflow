using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Core.EngineReads;

/// <summary>One running instance as the adapter read it from Temporal.</summary>
/// <param name="Address">The instance's address (its workflow id).</param>
/// <param name="Recipe">The recipe it runs.</param>
/// <param name="State">Its state, from the 'State' search attribute.</param>
/// <param name="Waiting">What it waits on; null when nothing is pending.</param>
/// <param name="Timers">Its running timers.</param>
/// <param name="Retries">The retries of its pending activity, if one retries.</param>
/// <param name="HistoryLength">The events in its Temporal history.</param>
public sealed record EngineInstanceFacts(InstanceAddress Address, EngineRecipeRef Recipe, string? State, EngineWaiting? Waiting, IReadOnlyList<EngineTimer> Timers, EngineRetries? Retries, long HistoryLength);
