using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Events;

/// <summary>The 'project.hidden' payload (projects page spec §5.1): the owner hid or unhid a project they lead.</summary>
/// <param name="CommandId">The command that did it.</param>
/// <param name="Linear">The led Linear project ('linear:project/&lt;id&gt;').</param>
/// <param name="Hidden">True to hide it, false to unhide it.</param>
/// <param name="Version">The hide version after this change: a led project that was never hidden is at 0, so the first hide is 1.</param>
public sealed record ProjectHidden(CommandId CommandId, string Linear, bool Hidden, long Version);
