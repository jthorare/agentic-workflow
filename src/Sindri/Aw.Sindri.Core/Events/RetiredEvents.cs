namespace Aw.Sindri.Core.Events;

/// <summary>The event kinds the recipe, pipeline and scoping engines wrote before they were removed. Old ledgers still hold them; projections show them as history only (the item timeline lists every event).</summary>
public static class RetiredEvents
{
    private static readonly string[] _prefixes = ["workflow.", "line.", "recipe.", "scope."];

    /// <summary>Whether <paramref name="eventType"/> is a retired kind ('workflow.*', 'line.*', 'recipe.*' or 'scope.*'). 'profile.approved' stays live until profile approval is dropped.</summary>
    /// <param name="eventType">The event type.</param>
    /// <returns>True for a retired kind.</returns>
    public static bool Is(string eventType)
    {
        ArgumentNullException.ThrowIfNull(eventType);
        return _prefixes.Any(prefix => eventType.StartsWith(prefix, StringComparison.Ordinal));
    }
}
