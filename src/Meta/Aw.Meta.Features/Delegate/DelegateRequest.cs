namespace Aw.Meta.Features.Delegate;

/// <summary>The Delegate request: hand an owner request to a lane as a GitHub issue and an ACTION message.</summary>
/// <param name="Lane">The lane; null to file nothing and list the lanes, with the ones whose keywords the title or text hit.</param>
/// <param name="Title">The issue title, one line; null only with <paramref name="Issue"/>, which brings its own.</param>
/// <param name="What">The issue body: what the owner wants; null only with <paramref name="Issue"/>.</param>
/// <param name="Issue">An existing issue to delegate instead of filing a new one; null to file one.</param>
public sealed record DelegateRequest(string? Lane, string? Title, string? What, int? Issue = null)
{
    /// <summary>Labels for the new issue (gh issue create --label); ignored with <see cref="Issue"/>.</summary>
    public IReadOnlyList<string> Labels { get; init; } = [];

    /// <summary>How long one wait for a woken non-Claude lane's hook lasts (AW_META_WAKE_CONFIRM_SECONDS, as lane-send); the wake waits twice.</summary>
    public TimeSpan WakeConfirm { get; init; } = TimeSpan.FromSeconds(10);
}
