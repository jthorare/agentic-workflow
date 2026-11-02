using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.ViewModels.Components;

/// <summary>
/// One button for a thing a desk step asks the person to read ("Open the design"): a link opens in the browser, a path opens as a file, and a review with
/// neither takes the person to its fact. Questions with no file stay in the desk form and get no button.
/// </summary>
internal sealed class DeskReviewViewModel
{
    private readonly DeskReview _review;
    private readonly Action<string> _showFact;

    internal DeskReviewViewModel(DeskReview review, ILauncher launcher, Action<string> showFact)
    {
        (_review, _showFact, Launch) = (review, showFact, new LinkLaunch(launcher));
        Label = Labels.ReviewButton(review.Kind);
        Name = Labels.ReviewButtonName(Label, review.Fact);
    }

    /// <summary>The launch of the review's link or file, with its failed line.</summary>
    public LinkLaunch Launch { get; }

    /// <summary>The button's words.</summary>
    public string Label { get; }

    /// <summary>The button's accessible name.</summary>
    public string Name { get; }

    /// <summary>The fact this review stands for.</summary>
    internal string Fact => _review.Fact;

    /// <summary>The buttons for a desk step's reviews, in the order served.</summary>
    /// <param name="reviews">The served reviews; null for none.</param>
    /// <param name="launcher">Opens a link or a file.</param>
    /// <param name="showFact">Takes the person to a fact.</param>
    /// <returns>One per review, but questions with nothing to open.</returns>
    internal static IReadOnlyList<DeskReviewViewModel> Of(IReadOnlyList<DeskReview>? reviews, ILauncher launcher, Action<string> showFact) =>
        [.. (reviews ?? []).Where(r => !(string.Equals(r.Kind, "questions", StringComparison.Ordinal) && Target(r) == ReviewTarget.Fact)).Select(r => new DeskReviewViewModel(r, launcher, showFact))];

    /// <summary>Opens what the review stands for: its link, else its file, else the fact in the facts panel.</summary>
    /// <returns>A task that ends when it was opened (or handed on).</returns>
    internal async Task Open()
    {
        switch (Target(_review))
        {
            case ReviewTarget.Link:
                _ = await Launch.Open(new Uri(_review.Url!, UriKind.Absolute), CancellationToken.None).ConfigureAwait(true);   // why: Target is Link only for an absolute http(s) url
                break;
            case ReviewTarget.File:
                _ = await Launch.OpenPath(_review.Path!, CancellationToken.None).ConfigureAwait(true);   // why: Target is File only for a non-empty path
                break;
            case ReviewTarget.Fact:
            default:
                _showFact(_review.Fact);
                break;
        }
    }

    // A link opens only when it is a web address: nothing a fact serves is run as a program or a file URL.
    private static ReviewTarget Target(DeskReview review) =>
        Uri.TryCreate(review.Url, UriKind.Absolute, out var uri) && (string.Equals(uri.Scheme, "http", StringComparison.Ordinal) || string.Equals(uri.Scheme, "https", StringComparison.Ordinal)) ? ReviewTarget.Link
        : !string.IsNullOrWhiteSpace(review.Path) ? ReviewTarget.File
        : ReviewTarget.Fact;

    private enum ReviewTarget
    {
        Fact,
        Link,
        File,
    }
}
