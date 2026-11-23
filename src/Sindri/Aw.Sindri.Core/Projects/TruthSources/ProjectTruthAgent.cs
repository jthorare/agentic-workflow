namespace Aw.Sindri.Core.Projects.TruthSources;

/// <summary>One live agent session, cut down to what the truth counts.</summary>
/// <param name="Repo">Its repo (owner/name), or null.</param>
/// <param name="Running">Running or asking.</param>
public sealed record ProjectTruthAgent(string? Repo, bool Running);
