using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the merge stop's pull request card. Never a served sentence.</summary>
internal static partial class Labels
{
    /// <summary>The card's heading.</summary>
    internal const string MergeCardHeading = "Pull request";

    /// <summary>The checks row's name.</summary>
    internal const string MergeChecks = "Checks";

    /// <summary>The review row's name.</summary>
    internal const string MergeReview = "Review";

    /// <summary>The proof row's name.</summary>
    internal const string MergeProof = "Proof";

    /// <summary>The docs row's name.</summary>
    internal const string MergeDocs = "Docs";

    /// <summary>Checks that have not been read.</summary>
    internal const string MergeChecksUnread = "Checks not read yet";

    /// <summary>Proof that has not been read.</summary>
    internal const string MergeProofUnread = "Proof not read yet";

    /// <summary>A proof that passed.</summary>
    internal const string MergeProofPassed = "Passed";

    /// <summary>A proof that failed.</summary>
    internal const string MergeProofFailed = "Failed";

    /// <summary>No proof ran.</summary>
    internal const string MergeProofNone = "No proof ran";

    /// <summary>Docs that were updated.</summary>
    internal const string MergeDocsUpdated = "Updated";

    /// <summary>Docs that this change does not need.</summary>
    internal const string MergeDocsNotNeeded = "Not needed";

    /// <summary>Docs that are missing.</summary>
    internal const string MergeDocsMissing = "Missing";

    /// <summary>The link to the pull request.</summary>
    /// <param name="number">The number.</param>
    /// <returns>"#12".</returns>
    internal static string MergePr(int number) => string.Create(CultureInfo.InvariantCulture, $"#{number}");

    /// <summary>The link's accessible name.</summary>
    /// <param name="number">The number.</param>
    /// <returns>"Open pull request #12".</returns>
    internal static string MergePrName(int number) => string.Create(CultureInfo.InvariantCulture, $"Open pull request #{number}");

    /// <summary>The checks, counted.</summary>
    /// <param name="passed">Passed.</param>
    /// <param name="failed">Failed.</param>
    /// <param name="pending">Pending.</param>
    /// <returns>"5 passed, 2 failed, 1 pending".</returns>
    internal static string MergeChecksLine(int passed, int failed, int pending) => string.Create(CultureInfo.InvariantCulture, $"{passed} passed, {failed} failed, {pending} pending");

    /// <summary>The review threads, counted.</summary>
    /// <param name="open">Open.</param>
    /// <param name="resolved">Resolved.</param>
    /// <returns>"1 open, 2 resolved".</returns>
    internal static string MergeReviewLine(int open, int resolved) => string.Create(CultureInfo.InvariantCulture, $"{open} open, {resolved} resolved");
}
