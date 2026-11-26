namespace Aw.Sindri.Ui.Api.Projects;

/// <summary>A typed branch or branch-prefix source of a project (#4499).</summary>
/// <param name="Repo">The GitHub repo, owner/name.</param>
/// <param name="Name">The branch name, or the start of one when <paramref name="Prefix"/> is true.</param>
/// <param name="Prefix">Whether <paramref name="Name"/> is a prefix of the branches the project means.</param>
internal sealed record ProjectBranchView(string Repo, string Name, bool Prefix);
