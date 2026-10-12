using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Profile;

/// <summary>A profile as loaded: the typed record, its approval hash and where it came from.</summary>
/// <param name="Profile">The profile.</param>
/// <param name="Hash">Its hash over the canonical JSON (comments and key order don't change it).</param>
/// <param name="Source">The file it was read from.</param>
public sealed record LoadedProfile(SindriProfile Profile, ProfileHash Hash, string Source);
