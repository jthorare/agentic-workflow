using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels.Agents;

/// <summary>The session chips (collector spec CO14, CU-6), shared by the Agents page and the session sheet.</summary>
internal static class SessionChips
{
    /// <summary>The chip for a served state. A word this build doesn't know renders as itself in capitals, so a newer Sindri never fails the read.</summary>
    /// <param name="state">The state as served (running, asking, idle, unknown, or something newer).</param>
    /// <returns>The glyph and the word.</returns>
    internal static SessionChip For(string state) => state switch
    {
        "running" => new("●", "Working"),
        "asking" => new("◐", "Asking you"),
        "idle" => new("○", "Idle"),
        "unknown" => new("○", "Unknown"),
        _ => new("○", state),
    };

    /// <summary>The state's word in lower case, for an accessible name ("running").</summary>
    /// <param name="state">The state as served.</param>
    /// <returns>The word.</returns>
    internal static string Word(string state) => For(state).Word.ToLower(CultureInfo.InvariantCulture);
}
