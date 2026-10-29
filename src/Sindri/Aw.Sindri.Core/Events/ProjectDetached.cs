using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Events;

/// <summary>The 'project.detached' payload (projects page spec §5.1): the owner removed an attached or hand-made project from the page.</summary>
/// <param name="CommandId">The command that removed it.</param>
/// <param name="Id">The project's id.</param>
/// <param name="Version">Its version after the removal.</param>
public sealed record ProjectDetached(CommandId CommandId, ProjectId Id, long Version);
