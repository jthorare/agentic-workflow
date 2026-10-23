namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>A T3 Code project: its id, its folder and the key of the repository T3 knows it as.</summary>
/// <param name="Id">The project id.</param>
/// <param name="Root">The project's folder.</param>
/// <param name="RepositoryKey">T3's 'repositoryIdentity.canonicalKey' ('github.com/owner/name'), or null for a folder that isn't a repository.</param>
internal sealed record T3Project(string Id, string Root, string? RepositoryKey);
