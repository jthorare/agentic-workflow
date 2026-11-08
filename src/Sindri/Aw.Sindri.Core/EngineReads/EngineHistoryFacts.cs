using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Core.EngineReads;

/// <summary>A page of an instance's history as the adapter read it.</summary>
/// <param name="Address">The instance.</param>
/// <param name="Run">The run the events belong to.</param>
/// <param name="Events">The page's events, newest first.</param>
/// <param name="Total">The events in the history.</param>
public sealed record EngineHistoryFacts(InstanceAddress Address, string Run, IReadOnlyList<EngineEventFacts> Events, long Total);
