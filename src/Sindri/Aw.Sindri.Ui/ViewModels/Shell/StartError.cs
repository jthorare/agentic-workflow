namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>The reason the Didn't start card shows: the innermost words of serve's last error line, without the codes, the 'sindri serve' prefix or the log path (the mono line under the card names the log).</summary>
internal static class StartError
{
    /// <summary>The reason in plain words.</summary>
    /// <param name="raw">The log's last error line, or null when it holds none.</param>
    /// <returns>The reason, or <see cref="Labels.NoErrorLine"/> when there is none.</returns>
    internal static string Reason(string? raw) => raw ?? Labels.NoErrorLine;   // stub (red)
}
