using Aw.Sindri.Core.Text;

namespace Aw.Sindri.Features;

/// <summary>Takes secrets out of the owner-typed text of a project event before it is written (projects page spec §5.1): the note and each link, so a pasted token or a '?token=' link never reaches a session that reads the ledger.</summary>
internal static class ProjectText
{
    /// <summary>The note scrubbed, or null.</summary>
    /// <param name="note">The note, or null.</param>
    /// <param name="scrubber">Redacts the secrets.</param>
    /// <returns>The scrubbed note.</returns>
    internal static string? Note(string? note, ITextScrubber scrubber) => note is null ? null : scrubber.Scrub(note);

    /// <summary>Each link scrubbed, or null.</summary>
    /// <param name="links">The links, or null.</param>
    /// <param name="scrubber">Redacts the secrets.</param>
    /// <returns>The scrubbed links.</returns>
    internal static IReadOnlyList<string>? Links(IReadOnlyList<string>? links, ITextScrubber scrubber) => links?.Select(scrubber.Scrub).ToList();
}
