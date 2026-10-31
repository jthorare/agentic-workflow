using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the concept layer's pin cards, concept pins spec §3.6-§3.8 and §5.</summary>
internal static partial class Labels
{
    /// <summary>The mark on a card the model proposed.</summary>
    internal const string PinSuggestedMark = "Suggested";

    /// <summary>The button on a derived card.</summary>
    internal const string PinThis = "Pin this";

    /// <summary>The button on a pinned card.</summary>
    internal const string PinEdit = "Edit pin";

    /// <summary>The layer head's button for a pin from nothing.</summary>
    internal const string PinNew = "New pin";

    /// <summary>The layer head's button for a list of pins.</summary>
    internal const string PinAddSeveral = "Add several";

    /// <summary>A written pin the approval hasn't covered.</summary>
    internal const string PinNotApproved = "Pinned, not approved yet";

    /// <summary>A removal the approval hasn't covered.</summary>
    internal const string PinUnpinNotApproved = "Unpinned, not approved yet";

    /// <summary>Why a written pin waits.</summary>
    internal const string PinOtherChangesWaiting = "Other changes are waiting, so this needs your review.";

    /// <summary>The button that opens the needs-you review.</summary>
    internal const string PinReviewAndApprove = "Review and approve";

    /// <summary>The button that opens the held map's review.</summary>
    internal const string PinOpenReview = "Open review";

    /// <summary>The button that queues a refresh again.</summary>
    internal const string PinTryAgain = "Try again";

    /// <summary>The layer head with no pin.</summary>
    internal const string PinNoPinsYet = "No pins yet. Press Pin this on a concept to make it yours.";

    /// <summary>The layer's one status line while the map rebuilds.</summary>
    internal const string PinRebuildingMap = "Rebuilding the map";

    /// <summary>The layer's one status line when the rebuild has landed.</summary>
    internal const string PinMapReady = "The map is ready";

    /// <summary>A card's state line: what was done, then where the rebuild stands.</summary>
    /// <param name="change">"created", "updated" or "removed".</param>
    /// <param name="rest">The rest of the line.</param>
    /// <returns>"Pinned · the map rebuild starts within a minute".</returns>
    internal static string PinStateLine(string? change, string rest) => $"{PinDone(change)} · {rest}";

    /// <summary>The state line while the map rebuild is held back.</summary>
    internal const string PinRestHeld = "the rebuild waits until you've looked at the held map";

    /// <summary>The state line when no repo is on its default branch.</summary>
    internal const string PinRestNoMainBranch = "the rebuild waits until a repo is back on its main branch";

    /// <summary>The state line while the build service waits out its backoff.</summary>
    /// <param name="at">The next try, as a local time.</param>
    /// <returns>"the last refreshes failed; the next try is at 14:05".</returns>
    internal static string PinRestBackoff(string at) => $"the last refreshes failed; the next try is at {at}";

    /// <summary>The state line before the build service's next tick.</summary>
    internal const string PinRestStartsSoon = "the map rebuild starts within a minute";

    /// <summary>The state line while the refresh runs.</summary>
    /// <param name="progress">The job's latest progress line, or null.</param>
    /// <returns>"rebuilding the map: resolving calls".</returns>
    internal static string PinRestBuilding(string? progress) => string.IsNullOrEmpty(progress) ? "rebuilding the map" : $"rebuilding the map: {progress}";

    /// <summary>The state line when the last refresh failed.</summary>
    /// <param name="code">The failure's code.</param>
    /// <returns>"Couldn't rebuild the map with this pin (SND-CONTEXT-040). Try again.".</returns>
    internal static string PinRestFailed(string code) => $"Couldn't rebuild the map with this pin ({code}). Try again.";

    /// <summary>The problem line for features the map no longer has.</summary>
    /// <param name="gone">How many.</param>
    /// <returns>"2 of its features are gone from the map".</returns>
    internal static string PinFeaturesGone(int gone) => gone == 1 ? "1 of its features is gone from the map" : string.Create(CultureInfo.InvariantCulture, $"{gone} of its features are gone from the map");

    /// <summary>The problem line for a pin nothing matches.</summary>
    internal const string PinMatchesNothing = "Nothing in the map matches this yet. Add features or words with Edit pin.";

    /// <summary>The problem line for features an earlier pin lists too.</summary>
    /// <param name="count">How many.</param>
    /// <param name="owner">The earlier pin's name.</param>
    /// <returns>"3 features are also listed in Billing. Billing keeps them because it is listed first.".</returns>
    internal static string PinClaimedTwice(int count, string owner) => string.Create(CultureInfo.InvariantCulture,
        $"{count} {(count == 1 ? "feature is" : "features are")} also listed in {owner}. {owner} keeps {(count == 1 ? "it" : "them")} because it is listed first.");

    /// <summary>The layer head's coverage.</summary>
    /// <param name="features">Features in pinned concepts.</param>
    /// <param name="allFeatures">Features in all.</param>
    /// <param name="records">Records in pinned concepts.</param>
    /// <param name="allRecords">Records in all.</param>
    /// <returns>"You've pinned 120 of 300 features and 410 of 2,300 records. The rest are in suggestions or in no concept.".</returns>
    internal static string PinCoverage(int features, int allFeatures, int records, int allRecords) => string.Create(CultureInfo.InvariantCulture,
        $"You've pinned {features:N0} of {allFeatures:N0} features and {records:N0} of {allRecords:N0} records. The rest are in suggestions or in no concept.");

    private static string PinDone(string? change) => change switch
    {
        "updated" => "Edited",
        "removed" => "Unpinned",
        _ => "Pinned",
    };
}
