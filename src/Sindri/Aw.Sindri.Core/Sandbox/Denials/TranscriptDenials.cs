namespace Aw.Sindri.Core.Sandbox.Denials;

/// <summary>What a transcript tail shows (sandbox spec §3.6, §15): refusals the CLI's own policy recorded, and paths a tool call failed on.</summary>
/// <param name="Observations">Policy denials, each already a <see cref="DenialObservation"/>.</param>
/// <param name="FailedPaths">Absolute paths named by a "Read-only file system", "No such file or directory" or "Permission denied" error; <see cref="PathDenials.Match"/> decides which are denials.</param>
public sealed record TranscriptDenials(IReadOnlyList<DenialObservation> Observations, IReadOnlyList<string> FailedPaths)
{
    /// <summary>Nothing seen.</summary>
    public static TranscriptDenials Empty { get; } = new([], []);
}
