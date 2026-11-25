namespace Aw.Context.Core.Map;

/// <summary>One doctor check of the map ('context:map', 'context:retention').</summary>
/// <param name="Name">The check's name.</param>
/// <param name="State">Its outcome.</param>
/// <param name="Detail">What it found.</param>
/// <param name="Fix">What to do, or null when nothing.</param>
public sealed record MapHealth(string Name, MapHealthState State, string Detail, string? Fix)
{
    /// <summary>The 'context:map' detail while there is no map yet: Sindri's doctor reads it and words the first map's wait itself (#3216).</summary>
    public static string NoMapDetail => "no map yet";
}
