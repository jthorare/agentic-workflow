namespace Aw.Sindri.Ui.Api.Projects;

/// <summary>One row of the From Linear tab. Name and lead came from Linear: untrusted, drawn as plain text.</summary>
/// <param name="Key">"linear:project/&lt;uuid&gt;", what Attach sends.</param>
/// <param name="Name">Linear's name.</param>
/// <param name="Lead">The lead's name, or null.</param>
/// <param name="State">The state word ("inProgress", "planned", …), or null when Linear's status is unknown.</param>
/// <param name="OpenIssues">Stored open issues in the project.</param>
/// <param name="Url">The project's Linear url, or null.</param>
internal sealed record ProjectLinearChoice(string Key, string Name, string? Lead, string? State, int OpenIssues, string? Url);
