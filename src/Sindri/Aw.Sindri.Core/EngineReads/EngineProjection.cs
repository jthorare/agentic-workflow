using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Core.EngineReads;

/// <summary>What the engine in depth derives from the adapter's facts (API spec §3.8, E-30): the served instance, the pending counts and the stuck rule, shared by the engine reads and GET /status.</summary>
public static class EngineProjection
{
    /// <summary>The served instance of <paramref name="facts"/> at <paramref name="now"/>: its opaque id, how long it has waited and whether it is stuck.</summary>
    /// <param name="facts">What the adapter read.</param>
    /// <param name="now">The clock now.</param>
    /// <returns>The instance.</returns>
    public static EngineInstance Instance(EngineInstanceFacts facts, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(facts);
        var waiting = facts.Waiting is { } wait ? new EngineWaitingOn(wait.Kind, wait.Name, Math.Max(0, (long)(now - wait.Since).TotalSeconds)) : null;
        return new EngineInstance(WorkflowInstanceId.Of(facts.Address), facts.Address, facts.Recipe, facts.State, waiting, facts.Timers, facts.Retries, facts.HistoryLength,
            EngineStuck.Is(facts.Waiting, facts.Timers.Count, now));
    }

    /// <summary>What the running instances wait on. 'continueAsNew' is 0: a continue-as-new swaps the run inside one workflow task, so no instance is ever read waiting to.</summary>
    /// <param name="running">Every running instance.</param>
    /// <param name="now">The clock now.</param>
    /// <returns>The counts.</returns>
    public static EnginePending Pending(IEnumerable<EngineInstanceFacts> running, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(running);
        List<EngineInstanceFacts> list = [.. running];
        return new EnginePending(
            list.Count(static i => i.Waiting is { } w && string.Equals(w.Kind, EngineWaitKinds.Update, StringComparison.Ordinal)),
            list.Count(static i => i.Waiting is { } w && string.Equals(w.Kind, EngineWaitKinds.Signal, StringComparison.Ordinal)),
            Stuck(list, now),
            ContinueAsNew: 0);
    }

    /// <summary>How many of <paramref name="running"/> are stuck.</summary>
    /// <param name="running">Every running instance.</param>
    /// <param name="now">The clock now.</param>
    /// <returns>The count.</returns>
    public static int Stuck(IEnumerable<EngineInstanceFacts> running, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(running);
        return running.Count(i => EngineStuck.Is(i.Waiting, i.Timers.Count, now));
    }
}
