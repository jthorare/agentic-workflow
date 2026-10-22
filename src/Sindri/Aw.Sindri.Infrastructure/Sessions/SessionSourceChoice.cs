namespace Aw.Sindri.Infrastructure.Sessions;

/// <summary>Which process and socket sources a box gets: /proc where there is one, ps and lsof on macOS, and the /proc ones (which then say unsupported-platform) when neither works.</summary>
internal static class SessionSourceChoice
{
    /// <summary>Whether the macOS sources are the ones to use.</summary>
    /// <param name="procRootOverridden">True when AW_SESSIONS_PROC_ROOT names a root (a test override: always the /proc sources).</param>
    /// <param name="procExists">Whether the /proc root is a directory.</param>
    /// <param name="isMacOS">Whether this is macOS.</param>
    /// <param name="psExists">Whether ps is at its absolute path.</param>
    /// <returns>True for the macOS sources.</returns>
    internal static bool UsesMac(bool procRootOverridden, bool procExists, bool isMacOS, bool psExists) =>
        !procRootOverridden && !procExists && isMacOS && psExists;
}
