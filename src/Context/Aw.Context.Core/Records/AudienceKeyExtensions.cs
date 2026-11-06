namespace Aw.Context.Core.Records;

/// <summary>Questions about an audience key's kind.</summary>
public static class AudienceKeyExtensions
{
    /// <summary>Whether the key is an 'owner:' one: a record under it is read only through its audience, never through a link (PR review page spec 2026-11-04 D17).</summary>
    /// <param name="audience">The record's audience, or null for none.</param>
    /// <returns>True for 'owner:&lt;name&gt;'.</returns>
    public static bool IsOwner(this AudienceKey? audience) => audience is { } key && key.Value.StartsWith("owner:", StringComparison.Ordinal);
}
