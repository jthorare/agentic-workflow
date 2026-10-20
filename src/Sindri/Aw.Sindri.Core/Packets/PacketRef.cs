namespace Aw.Sindri.Core.Packets;

/// <summary>A packet directory and what aw-packets needs to check it.</summary>
/// <param name="Kind">The packet's kind.</param>
/// <param name="Directory">The packet directory.</param>
/// <param name="Repo">A directory inside the repository the packet builds (aw-packets' --repo, its base commit's files come from git); null for the packet directory itself.</param>
/// <param name="RepoSlug">'owner/name', which a scope packet is filed under when it is frozen; null for every other kind.</param>
/// <param name="Instance">The workflow instance's slug, which a scope packet is filed under when it is frozen; null for every other kind.</param>
public sealed record PacketRef(PacketKind Kind, string Directory, string? Repo = null, string? RepoSlug = null, string? Instance = null);
