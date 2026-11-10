namespace Aw.Sindri.Core.Connections;

/// <summary>
/// The one place that decides which token a source reads with (#2508): the first connected connection of the preset on the Connections page, else the profile's token pointer. Every feed or reader of a preset
/// that can be connected there (github, slack, linear) asks this port, so no source keeps the old "profile pointer only" order.
/// </summary>
public interface IConnectionCredentials
{
    /// <summary>Finds the token for a preset.</summary>
    /// <param name="preset">The preset ('github', 'slack', 'linear').</param>
    /// <param name="profilePointer">The profile's secret pointer for the source, or null when the profile names none.</param>
    /// <returns>The connected connection's access token, else the token the profile pointer resolves to, else null (no feed).</returns>
    public string? TokenFor(string preset, string? profilePointer);
}
