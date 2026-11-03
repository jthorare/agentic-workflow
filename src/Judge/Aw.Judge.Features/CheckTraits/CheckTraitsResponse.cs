namespace Aw.Judge.Features.CheckTraits;

/// <summary>
/// The CheckTraits response. The command exits 0 when there is nothing to report or the check was skipped, 2 when
/// <paramref name="Findings"/> is not empty (edit and stop: stderr; scan: JSON lines on stdout) and 3 when
/// <paramref name="JevUnavailable"/> (trait-check's own scheme, kept by trait-check.sh).
/// </summary>
/// <param name="Findings">The traits broken, worst first.</param>
/// <param name="JevUnavailable">Jev was excluded, has no key, couldn't be reached or ran out of budget, so nothing was judged.</param>
/// <param name="Reason">Why Jev was unavailable (the first request's reason code), or null.</param>
/// <param name="Requests">How many Jev requests were made.</param>
public sealed record CheckTraitsResponse(IReadOnlyList<TraitFinding> Findings, bool JevUnavailable = false, string? Reason = null, int Requests = 0)
{
    /// <summary>The reason code of an edited image file that was not judged because its picture was not sent.</summary>
    public const string ImageNotSent = "image-not-sent";

    /// <summary>
    /// Edited image files that were not judged, by repo-relative path, with <see cref="ImageNotSent"/>: left out of the 4 files and 4 MiB, unreadable, or dropped by Jev.
    /// A file that is skipped is never judged on its name alone. The command prints no field for it; the trait-check log has a line.
    /// </summary>
    public IReadOnlyDictionary<string, string> Skipped { get; init; } = NoSkips;

    // One shared instance, so two responses that skipped nothing are equal (record equality compares the dictionaries by reference).
    internal static IReadOnlyDictionary<string, string> NoSkips { get; } = new Dictionary<string, string>(StringComparer.Ordinal);
}
