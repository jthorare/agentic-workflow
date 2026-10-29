namespace Aw.Sindri.Core.Projects;

/// <summary>A row of the 'project-hidden' view: whether the owner hides a led Linear project, keyed by its Linear key. A project never hidden has no row (hide version 0).</summary>
/// <param name="Linear">The led Linear project ('linear:project/&lt;id&gt;').</param>
/// <param name="Hidden">Whether it is hidden now.</param>
/// <param name="Version">The hide version: one more per hide or unhide.</param>
/// <param name="Sequence">The ledger sequence that last changed the row.</param>
public sealed record ProjectHiddenRow(string Linear, bool Hidden, long Version, long Sequence);
