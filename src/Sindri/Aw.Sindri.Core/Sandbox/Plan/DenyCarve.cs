namespace Aw.Sindri.Core.Sandbox.Plan;

/// <summary>Splits a denied directory around the writable paths inside it, so the agent CLI's own read deny doesn't cover the paths a Step writes.</summary>
internal static class DenyCarve
{
    /// <summary>Gets the paths to deny in place of <paramref name="denied"/>.</summary>
    /// <param name="denied">The denied path.</param>
    /// <param name="writable">The Step's writable paths.</param>
    /// <param name="entries">The host's directory listing, or null when it has none.</param>
    /// <returns>The path itself when no writable path lies strictly inside it or the listing is unavailable, else its entries that don't lead to a writable path, level by level.</returns>
    internal static IReadOnlyList<string> Around(string denied, IReadOnlyList<string> writable, Func<string, IReadOnlyList<string>>? entries)
    {
        _ = (writable, entries);   // why: the red stub; the carve arrives with the fix
        return [denied];
    }
}
