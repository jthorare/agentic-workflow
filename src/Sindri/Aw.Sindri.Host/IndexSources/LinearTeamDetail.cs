namespace Aw.Sindri.Host.IndexSources;

/// <summary>A team a Linear key can read.</summary>
/// <param name="Id">Linear's id for the team.</param>
/// <param name="Key">The team key, such as 'ENG'.</param>
/// <param name="Name">The team's name.</param>
internal sealed record LinearTeamDetail(string Id, string Key, string Name);
