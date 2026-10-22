namespace Aw.Sindri.Core.Setup;

/// <summary>Where the profile stands.</summary>
/// <param name="Presence">Missing, unapproved, drifted or approved.</param>
public sealed record ProfileFact(ProfilePresence Presence)
{
    /// <summary>What the live profile allows in plain words, or null when there is no live profile to read.</summary>
    public IReadOnlyList<SetupPreviewLine>? Preview { get; init; }
}
