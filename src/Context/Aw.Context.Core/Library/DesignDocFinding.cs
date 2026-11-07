namespace Aw.Context.Core.Library;

/// <summary>An error found in one file: one <see cref="DesignDocScope.Check"/> found against the rest of a doc's scope, or a library near miss.</summary>
/// <param name="Path">The doc's path.</param>
/// <param name="Error">The error (012, 015 or 016 from the scope check; 019 for a near miss).</param>
public sealed record DesignDocFinding(string Path, DesignDocError Error);
