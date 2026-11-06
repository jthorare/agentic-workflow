namespace Aw.Context.Core.Library;

/// <summary>An error <see cref="DesignDocScope.Check"/> found in one doc against the rest of its scope.</summary>
/// <param name="Path">The doc's path.</param>
/// <param name="Error">The error (012, 015 or 016).</param>
public sealed record DesignDocFinding(string Path, DesignDocError Error);
