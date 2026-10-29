namespace Aw.Context.Core.Sources;

/// <summary>A person in a source, as the Projects page names one.</summary>
/// <param name="Id">The user's id in the source (Linear's own id, without the 'linear:user/' prefix a 'led-by' link carries).</param>
/// <param name="Name">The user's display name, or null when the source gave none.</param>
public readonly record struct UserRef(ExternalId Id, string? Name);
