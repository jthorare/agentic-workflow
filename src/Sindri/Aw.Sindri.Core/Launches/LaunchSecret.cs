namespace Aw.Sindri.Core.Launches;

/// <summary>
/// A launch's secret as a call carries it (API spec §8.1, header X-Sindri-Launch-Secret). It prints as nothing, so a request record, a log line or an
/// exception that holds one never shows it.
/// </summary>
/// <param name="Value">The secret.</param>
public readonly record struct LaunchSecret(string Value)
{
    /// <summary>Never the secret.</summary>
    /// <returns>A fixed word.</returns>
    public override string ToString() => "LaunchSecret(redacted)";
}
