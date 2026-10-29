using Aw.Sindri.Core.Text;

namespace Aw.Sindri.Core.Projects;

/// <summary>Takes secrets out of the owner-typed text of a project event before it is written (projects page spec §5.1): the notes and each link, so a pasted token or a '?token=' link never reaches a session that reads the ledger.</summary>
public static class ProjectText
{
    /// <summary>The notes scrubbed, or null.</summary>
    /// <param name="notes">The notes, or null.</param>
    /// <param name="scrubber">Redacts the secrets.</param>
    /// <returns>The scrubbed notes.</returns>
    public static string? Notes(string? notes, ITextScrubber scrubber) => notes is null ? null : scrubber.Scrub(notes);

    /// <summary>Each link scrubbed, or null.</summary>
    /// <param name="links">The links, or null.</param>
    /// <param name="scrubber">Redacts the secrets.</param>
    /// <returns>The scrubbed links.</returns>
    public static IReadOnlyList<string>? Links(IReadOnlyList<string>? links, ITextScrubber scrubber) => links?.Select(scrubber.Scrub).ToList();
}
