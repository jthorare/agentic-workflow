namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>The shape of <c lang="text">config/launcher/terminals.json</c> (read through <see cref="LauncherJsonContext"/>).</summary>
/// <param name="Schema">The file's schema version.</param>
/// <param name="Terminals">The rows, in detection order.</param>
internal sealed record TerminalTemplatesFile(int Schema, IReadOnlyList<TerminalTemplateRow> Terminals);
