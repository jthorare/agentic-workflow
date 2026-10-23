namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>Which T3 provider runs a Sindri catalog agent.</summary>
internal static class T3Providers
{
    /// <summary>The T3 driver of a built-in catalog agent, or null for one T3 has no driver for (an added entry such as aider).</summary>
    /// <param name="agent">The catalog agent id.</param>
    /// <returns>The driver kind, or null.</returns>
    public static string? DriverFor(string agent) => agent switch
    {
        "claude-code" => "claudeAgent",
        "codex" => "codex",
        "cursor" => "cursor",
        _ => null,
    };

    /// <summary>The first instance of <paramref name="driverKind"/> with no constraint (a disabled one carries one) and a model, with its first model.</summary>
    /// <param name="capabilities">T3's providers.</param>
    /// <param name="driverKind">The driver.</param>
    /// <returns>The instance and model, or null.</returns>
    public static T3Model? ModelFor(T3Wire.Capabilities capabilities, string driverKind) =>
        capabilities.Providers
            .Where(p => string.Equals(p.DriverKind, driverKind, StringComparison.Ordinal) && (p.Constraints is null || p.Constraints.Count == 0) && p.Models.Count > 0)
            .Select(static p => new T3Model(p.ProviderInstanceId, p.Models[0].Id))
            .FirstOrDefault();
}
