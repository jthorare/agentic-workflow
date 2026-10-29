using Aw.Sindri.Core.Projects.Vocabulary;

namespace Aw.Sindri.Features.ListLinearProjects;

/// <summary>One row of the From Linear tab. Name and lead came from Linear: untrusted, fenced for a session.</summary>
/// <param name="Key">'linear:project/&lt;id&gt;', what Attach takes.</param>
/// <param name="Name">Linear's name.</param>
/// <param name="Lead">The lead's name, or null.</param>
/// <param name="State">The state in the Projects page's words, or null when Linear's status is unknown.</param>
/// <param name="OpenIssues">Stored open issues in the project.</param>
/// <param name="Url">The project's Linear url when it is one a result may carry, else null.</param>
public sealed record LinearProjectChoice(string Key, string Name, string? Lead, ProjectState? State, int OpenIssues, string? Url);
