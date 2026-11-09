using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Host.Catalog;

/// <summary>
/// The command catalog (API spec §3.3): the shipped rows and the rows still pending their phase (G7), and the challenge and Step completion rows. Routes and CLI
/// forms are Host facts, so the rows live here; each source is a static class beside it.
/// </summary>
internal sealed class CommandCatalog : ICommandCatalog
{
    public IReadOnlyList<CommandRow> Rows { get; } = [.. ShellRows.All, .. ItemRows.All, .. ProjectRows.All, .. TodoRows.All, .. LinearRows.All, .. RequestRows.All, .. ServiceRows.All, .. PendingRows.All, .. SessionRows.All, .. ContextRows.All, .. SetupRows.All, .. SandboxRows.All, .. ReviewRows.All, .. LinearCommentCreateRows.All, .. LinearConnectionRows.All, .. ConnectionRows.All, .. ChallengeRows.All, .. CompleteStepRows.All, .. MemoryRows.All, .. WorkflowRunRows.All, .. ResourceRows.All];

    public IReadOnlyList<CommandRow> For(Type commandType) => [.. Rows.Where(row => row.CommandType == commandType)];
}
