using System.Text.Json.Serialization;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Profile;

/// <summary>One entry of 'index.concepts' (docs/csharp/sindri-profile.md): a concept the owner pins, with the features and files that belong to it.</summary>
/// <param name="Id">The slug of the concept's key ('scheduling' pins concept:scheduling); left out, the name gives one. Not 'other' (reserved).</param>
/// <param name="Name">What the map shows (required).</param>
/// <param name="Summary">A line about the concept.</param>
/// <param name="Features">Feature node keys or key globs ('feature:shifts', 'feature:shifts.*').</param>
/// <param name="Paths">Repo-relative file globs; the features implemented there belong to the concept.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ConceptSettings(
    ConceptPinId? Id = null,
    string? Name = null,
    string? Summary = null,
    IReadOnlyList<string>? Features = null,
    IReadOnlyList<string>? Paths = null);
