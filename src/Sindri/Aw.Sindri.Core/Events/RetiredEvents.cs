namespace Aw.Sindri.Core.Events;

/// <summary>The event kinds the recipe, pipeline and scoping engines wrote before they were removed. Old ledgers still hold them; projections show them as history only. Red stub.</summary>
public static class RetiredEvents
{
    /// <summary>Whether <paramref name="eventType"/> is a retired kind ('workflow.*', 'line.*', 'recipe.*' or 'scope.*').</summary>
    /// <param name="eventType">The event type.</param>
    /// <returns>True for a retired kind.</returns>
    public static bool Is(string eventType) => throw new NotSupportedException("RetiredEvents.Is is written in the green commit.");
}
