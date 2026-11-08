using System.Globalization;
using Aw.Sindri.Core.EngineReads;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Testing.Engine;

/// <summary>Builders for what the engine reader reports: an instance, a wait, a timer and a history event.</summary>
public static class EngineFacts
{
    /// <summary>A running instance of 'workflow.step' that waits on <paramref name="waiting"/>.</summary>
    /// <param name="address">Its address.</param>
    /// <param name="waiting">What it waits on, or null.</param>
    /// <param name="state">Its state.</param>
    /// <param name="timers">Its running timers.</param>
    /// <param name="retries">The retries of its pending activity.</param>
    /// <returns>The facts.</returns>
    public static EngineInstanceFacts Instance(string address, EngineWaiting? waiting, string state = "checks", EngineTimer[]? timers = null, EngineRetries? retries = null) =>
        new(InstanceAddress.From(address), new EngineRecipeRef(RecipeId.From("workflow.step"), "3f1c09"), state, waiting, timers ?? [], retries, 88);

    /// <summary>A wait that began at <paramref name="since"/>.</summary>
    /// <param name="kind">One of <see cref="EngineWaitKinds"/>.</param>
    /// <param name="since">When it began.</param>
    /// <param name="name">The activity or timer name.</param>
    /// <returns>The wait.</returns>
    public static EngineWaiting Wait(string kind, DateTimeOffset since, string? name = null) => new(kind, name, since);

    /// <summary>A history event at <paramref name="time"/>.</summary>
    /// <param name="id">The Temporal event id.</param>
    /// <param name="time">When it happened.</param>
    /// <param name="type">The event type.</param>
    /// <param name="activityId">The RecordEvent activity id of a marker, or null.</param>
    /// <returns>The event.</returns>
    public static EngineEventFacts Event(long id, DateTimeOffset time, string type = "ActivityTaskScheduled", string? activityId = null) =>
        new(id, time, type, "step.checks", $"{type} {id.ToString(CultureInfo.InvariantCulture)}", activityId);
}
