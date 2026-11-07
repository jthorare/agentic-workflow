using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Reviews;

/// <summary>Which review settings built the page.</summary>
/// <param name="Name">The settings name, or default.</param>
/// <param name="From">Where the repo matched: review.projects[0], or default.</param>
/// <param name="Pick">Set when two project entries tie (§7.2): the settings the owner picks from.</param>
public sealed record ReviewPageSettings(
    string Name,
    string From,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ReviewPagePick? Pick = null);
