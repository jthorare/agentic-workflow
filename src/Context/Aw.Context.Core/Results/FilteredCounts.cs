using System.Text.Json.Serialization;

namespace Aw.Context.Core.Results;

/// <summary>How many files an answer left out of the product picture, per role the person named in the repo's 'index.roles' (issue 1105). Ignored files are not in the map, so only demoted ones are counted here.</summary>
/// <param name="Demoted">The demoted files the answer carries (they rank last), by role name ('index.demote'); never empty.</param>
public sealed record FilteredCounts([property: JsonPropertyName("demoted")] IReadOnlyDictionary<string, int> Demoted);
