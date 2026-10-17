using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Host.Catalog;

/// <summary>
/// The command catalog (API spec §3.3): the shipped rows and the rows still pending their phase (G7), and the engine's rows (EngineRows). Routes and CLI
/// forms are Host facts, so the rows live here; each source is a static class beside it.
/// </summary>
internal sealed class CommandCatalog : ICommandCatalog
{
    public IReadOnlyList<CommandRow> Rows { get; } = [.. ShellRows.All, .. ItemRows.All, .. RequestRows.All, .. ServiceRows.All, .. PendingRows.All, .. SessionRows.All, .. EngineRows.All, .. ContextRows.All];

    public IReadOnlyList<CommandRow> For(Type commandType) => [.. Rows.Where(row => row.CommandType == commandType)];
}
