namespace Aw.Sindri.Core.Graph.Templates;

/// <summary>Where a starter template reads CI from: a GitHub connection and one repo's branch (#3534).</summary>
/// <param name="Connection">The connection's name, such as github:work.</param>
/// <param name="Owner">The repo's owner.</param>
/// <param name="Repo">The repo's name.</param>
/// <param name="Branch">The branch whose runs the template reads, usually the default branch.</param>
public sealed record GitHubCiSource(string Connection, string Owner, string Repo, string Branch);
