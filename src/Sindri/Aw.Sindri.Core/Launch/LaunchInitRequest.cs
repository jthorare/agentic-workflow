namespace Aw.Sindri.Core.Launch;

/// <summary>The flags of <c lang="bash">sindri launch init</c>.</summary>
/// <param name="Viewer">A viewer to use instead of detecting one ('print', 'warp' or a terminals.json id), or null.</param>
/// <param name="Force">True to replace an existing file.</param>
/// <param name="ToStdout">True to write nothing and return the file's text.</param>
public sealed record LaunchInitRequest(string? Viewer, bool Force, bool ToStdout);
