namespace Aw.Context.Testing.Linear;

/// <summary>An issue of the stateful fake Linear, read-only (names, not ids, for the things it points at).</summary>
/// <param name="Id">The issue id (the client's, when issueCreate sent one).</param>
/// <param name="Identifier">The identifier, 'ENG-3'.</param>
/// <param name="Title">The title.</param>
/// <param name="Description">The description, or null.</param>
/// <param name="State">The workflow state's name.</param>
/// <param name="Assignee">The assignee's name, or null.</param>
/// <param name="Priority">0 (none) to 4 (low).</param>
/// <param name="Estimate">The estimate, or null.</param>
/// <param name="Cycle">The cycle's name, or null.</param>
/// <param name="Project">The project's name, or null.</param>
/// <param name="Labels">The label names.</param>
/// <param name="Team">The team key.</param>
/// <param name="CreatedAt">When it was created (the fake's clock, ISO 8601).</param>
/// <param name="UpdatedAt">When it last changed (see <see cref="LinearUpdatedAt"/>).</param>
/// <param name="ArchivedAt">When it was archived, or null.</param>
public sealed record FakeLinearIssue(
    string Id,
    string Identifier,
    string Title,
    string? Description,
    string State,
    string? Assignee,
    int Priority,
    int? Estimate,
    string? Cycle,
    string? Project,
    IReadOnlyList<string> Labels,
    string Team,
    string CreatedAt,
    string UpdatedAt,
    string? ArchivedAt);
