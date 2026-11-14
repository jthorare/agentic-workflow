using System.Text.RegularExpressions;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>The reason the Didn't start card shows: the innermost words of serve's last error line, without the codes, the 'sindri serve' prefix or the log path (the mono line under the card names the log).</summary>
internal static partial class StartError
{
    /// <summary>The reason in plain words.</summary>
    /// <param name="raw">The log's last error line, or null when it holds none.</param>
    /// <returns>The reason, or <see cref="Labels.NoErrorLine"/> when there is none.</returns>
    internal static string Reason(string? raw)
    {
        if (raw is null)
        {
            return Labels.NoErrorLine;
        }

        var said = Prefix().Replace(LogSuffix().Replace(raw, string.Empty), string.Empty).Trim();
        return said.Length == 0 ? Labels.NoErrorLine : said;
    }

    // " (log: <path>)" at the end: the mono line under the card already names the log.
    [GeneratedRegex(@"\s*\(log:[^)]*\)\s*$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex LogSuffix();

    // The leading chain of "SND-XXX-NNN " codes and "'sindri serve' exited: " prefixes.
    [GeneratedRegex(@"^(?:SND-[A-Z]+-[0-9]+\s+|'[^']*'\s+exited:\s*)+", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Prefix();
}
