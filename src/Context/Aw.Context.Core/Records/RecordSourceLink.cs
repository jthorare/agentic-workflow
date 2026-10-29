namespace Aw.Context.Core.Records;

/// <summary>Where a stored record opens outside Sindri.</summary>
/// <param name="Permalink">The link, or null when the record has none Sindri may give.</param>
/// <param name="Aliases">Its other names, such as the Linear identifier 'SPL-103'.</param>
public sealed record RecordSourceLink(Uri? Permalink, IReadOnlyList<string> Aliases);
