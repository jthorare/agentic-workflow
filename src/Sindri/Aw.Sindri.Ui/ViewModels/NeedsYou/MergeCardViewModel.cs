using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>
/// The merge stop's card (F3 needs-you spec, stop 3): the pull request, its checks, the review threads, the proof and the docs, in words, from
/// "fact:prStatus". A part the engine has not read yet says so; nothing here sends anything.
/// </summary>
internal sealed class MergeCardViewModel
{
    private readonly PrStatusView _status;
    private readonly ILauncher _launcher;

    internal MergeCardViewModel(PrStatusView status, ILauncher? launcher = null)
    {
        ArgumentNullException.ThrowIfNull(status);
        (_status, _launcher) = (status, launcher ?? NullLauncher.Instance);
        Lines =
        [
            new(Labels.MergeChecks, status.Checks is { } checks ? Labels.MergeChecksLine(checks.Passed, checks.Failed, checks.Pending) : Labels.MergeChecksUnread),
            new(Labels.MergeReview, Labels.MergeReviewLine(status.Review.Open, status.Review.Resolved)),
            new(Labels.MergeProof, ProofWords(status.Proof)),
            new(Labels.MergeDocs, DocsWords(status.Docs)),
        ];
    }

    /// <summary>The heading.</summary>
    public string Heading { get; } = Labels.MergeCardHeading;

    /// <summary>"#12".</summary>
    public string PrText => Labels.MergePr(_status.Pr);

    /// <summary>The link's accessible name.</summary>
    public string PrName => Labels.MergePrName(_status.Pr);

    /// <summary>True for an http or https link.</summary>
    public bool CanOpen => Uri.TryCreate(_status.Url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    /// <summary>Checks, review, proof and docs, in that order.</summary>
    public IReadOnlyList<MergeCardLine> Lines { get; }

    /// <summary>Hands the pull request's link to the system. Only an http or https link is opened.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>A task that completes once the system answered.</returns>
    public async Task Open(CancellationToken cancellationToken)
    {
        if (CanOpen && Uri.TryCreate(_status.Url, UriKind.Absolute, out var uri))
        {
            _ = await _launcher.OpenUri(uri, cancellationToken).ConfigureAwait(true);
        }
    }

    private static string ProofWords(PrProofView? proof) => proof?.Status switch
    {
        null => Labels.MergeProofUnread,
        "passed" => Labels.MergeProofPassed,
        "failed" => Labels.MergeProofFailed,
        "none" => Labels.MergeProofNone,
        var other => other,
    };

    private static string DocsWords(PrDocsView docs) => docs.Status switch
    {
        "updated" => Labels.MergeDocsUpdated,
        "notNeeded" => Labels.MergeDocsNotNeeded,
        "missing" => Labels.MergeDocsMissing,
        var other => other,
    };
}
