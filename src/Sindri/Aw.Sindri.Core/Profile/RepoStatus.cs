namespace Aw.Sindri.Core.Profile;

/// <summary>Where a repo stands against the approved profile.</summary>
/// <param name="State">The state.</param>
/// <param name="Path">The repository's top level, or null outside git.</param>
/// <param name="Name">The profile name: the approved entry when onboarded, the live profile's entry when waiting for approval, else null.</param>
public sealed record RepoStatus(RepoState State, string? Path, string? Name);
