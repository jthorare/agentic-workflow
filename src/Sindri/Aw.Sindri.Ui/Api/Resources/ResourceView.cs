namespace Aw.Sindri.Ui.Api.Resources;

/// <summary>One resource an owner holds: a database or a disk folder.</summary>
/// <param name="Owner">Who holds it ('workflow:triage').</param>
/// <param name="Kind">'database' or 'disk'.</param>
/// <param name="Name">The resource's name.</param>
/// <param name="Size">Its size in bytes; null when it could not be measured.</param>
/// <param name="Path">Where it lives.</param>
/// <param name="LimitBytes">The cap in bytes (a disk has one); null for a database or a resource with none declared.</param>
internal sealed record ResourceView(string Owner, string Kind, string Name, long? Size, string Path, long? LimitBytes = null);
