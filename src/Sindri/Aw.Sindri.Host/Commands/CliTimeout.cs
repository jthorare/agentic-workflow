using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Text;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// The CLI's optional hard cap on one call to the control plane: AW_SINDRI_CLI_TIMEOUT, whole seconds. Unset, empty or 0 means no cap,
/// so a slow but healthy answer (a 40 s map review) completes; a dead port still fails fast through <see cref="Connect"/>.
/// </summary>
internal static class CliTimeout
{
    /// <summary>The variable that names the cap.</summary>
    internal const string Variable = "AW_SINDRI_CLI_TIMEOUT";

    /// <summary>How long the CLI waits to connect to the recorded port before it reports SND-API-002 (the connection, not the answer). The ping and the request share one handler (#3727), whose connect cap is the ping's 2 s; CliTimeoutTests keeps this value equal to that handler's, so the error names the cap that applies.</summary>
    internal static readonly TimeSpan Connect = TimeSpan.FromSeconds(2);

    /// <summary>The largest cap HttpClient.Timeout accepts, in whole seconds (int.MaxValue milliseconds, about 24.8 days).</summary>
    internal const int MaxSeconds = int.MaxValue / 1000;

    /// <summary>Reads the cap from the environment.</summary>
    /// <param name="environment">Reads an environment variable.</param>
    /// <returns>The cap, null for none, or SND-CLI-003 for a value that isn't a whole number of seconds.</returns>
    public static Result<TimeSpan?> Read(Func<string, string?> environment)
    {
        var text = environment(Variable);
        if (string.IsNullOrWhiteSpace(text))
        {
            return (TimeSpan?)null;
        }

        if (!int.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) || seconds > MaxSeconds)
        {
            return SindriErrors.InvalidInput.With($"{Variable} must be a whole number of seconds up to {MaxSeconds} (0 or unset: no cap), not '{SafeText.Line(text, 40)}'");
        }

        return seconds == 0 ? (TimeSpan?)null : TimeSpan.FromSeconds(seconds);
    }
}
