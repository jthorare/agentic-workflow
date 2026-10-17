namespace Aw.Sindri.Core.Launch;

/// <summary>What <c lang="bash">sindri launch init</c> chose.</summary>
/// <param name="Viewer">The viewer's name ('print', 'warp' or the terminal's label).</param>
/// <param name="Reason">Why, in a few words ('no GUI session', 'from $TERM_PROGRAM').</param>
/// <param name="Path">The file written, or null with <c lang="bash">ToStdout</c>.</param>
/// <param name="Json">The file's text.</param>
public sealed record LaunchInitReport(string Viewer, string Reason, string? Path, string Json);
