namespace Aw.Sindri.Core.Projects;

/// <summary>A row of the 'project-hidden' view: the owner's state for a led Linear project, keyed by its Linear key: whether it is hidden and its notes. A project never hidden or noted has no row (version 0).</summary>
/// <param name="Linear">The led Linear project ('linear:project/&lt;id&gt;').</param>
/// <param name="Hidden">Whether it is hidden now.</param>
/// <param name="Version">The card's version: one more per hide, unhide or notes change.</param>
/// <param name="Sequence">The ledger sequence that last changed the row.</param>
/// <param name="Notes">The owner's notes, or null.</param>
public sealed record ProjectHiddenRow(string Linear, bool Hidden, long Version, long Sequence, string? Notes = null);
