namespace Aw.Sindri.Core.Evolve;

/// <summary>The held reason of each gate layer: fixed words that name the layer and never carry matched text.</summary>
public static class LeakReasons
{
    /// <summary>The reason for a hold by one layer.</summary>
    /// <param name="layer">The layer.</param>
    /// <returns>Such as 'held by the verbatim layer'.</returns>
    public static string For(LeakLayer layer) => $"held by the {layer.ToString().ToLowerInvariant()} layer";
}
