using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>One 'index.concepts' entry of a profile with where it lives.</summary>
/// <param name="Key">The key path <see cref="ProfileDiff"/> gives the entry ('index.concepts.billing', 'repos.api.index.concepts.2'): its id, or its position when it has none.</param>
/// <param name="SetIn">'profile' or 'repo &lt;name&gt;'.</param>
/// <param name="Repo">The repo whose own entry it is, or null for the profile's.</param>
/// <param name="Settings">The entry.</param>
internal sealed record ProfilePin(string Key, string SetIn, string? Repo, ConceptSettings Settings);
