using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Reviews;

/// <summary>The rows of one project, or of one repo no project names.</summary>
/// <param name="By">What it groups by.</param>
/// <param name="Key">The project key for a project section; null for a repo one.</param>
/// <param name="Name">The project's name or the repo's owner/name.</param>
/// <param name="Rows">Newest activity first.</param>
public sealed record ReviewSection(ReviewSectionBy By, [property: JsonPropertyName("id")] string? Key, string Name, IReadOnlyList<ReviewRow> Rows);
