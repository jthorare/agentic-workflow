namespace Aw.Context.Core.Lenses;

/// <summary>What a code index that trails the map along main says about itself (code lens 'catching-up' limit): the commit it describes, the commit the map is at, and the files changed between them.</summary>
/// <param name="IndexCommit">The commit the index describes.</param>
/// <param name="MapCommit">The map version's commit.</param>
/// <param name="Pending">The files changed from the index's commit to the map's: rows in them may be out of date, rows in any other file are current.</param>
public sealed record SearchedCatchingUp(string IndexCommit, string MapCommit, IReadOnlyList<string> Pending);
