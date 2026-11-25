using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Reviews;

namespace Aw.Sindri.Features.GetReviewPage;

/// <summary>
/// The PR review page (PR review page spec §6.2, §6.3, §9). Path segments are checked against GitHub's name rules first (400), then the login (<c language="text">review.login</c>, else the GitHub connection's; SND-REVIEW-007) and the synced repos (SND-REVIEW-004).
/// The forge is read by <see cref="ReviewPageReader"/> (a head that moves is read again), the page is built by <see cref="ReviewPageBuilder"/> and cached by <see cref="ReviewPageCache"/>;
/// when the forge can't be reached the newest cached page answers, and with none cached the answer is SND-REVIEW-503. A PR the forge does not have is SND-REVIEW-404.
/// </summary>
/// <param name="approvals">The approved profile.</param>
/// <param name="store">The stored record, claims and doc map.</param>
/// <param name="reader">The forge reads.</param>
/// <param name="facts">The facts the page is built from.</param>
/// <param name="cache">The page snapshots and list summaries.</param>
/// <param name="account">The GitHub connection, whose login stands in when 'review.login' is not set.</param>
internal sealed class GetReviewPageService(IApprovedProfile approvals, IReviewPageStore store, ReviewPageReader reader, ReviewPageFactsReader facts, ReviewPageCache cache, IGitHubAccount account) : IService<GetReviewPageRequest, ReviewPageModel>
{
    public async ValueTask<Result<ReviewPageModel>> Handle(GetReviewPageRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var pull = new ReviewPullRef(request.Owner, request.Repo, request.Number);
        if (!pull.IsValid)
        {
            return SindriErrors.InvalidInput.With(string.Create(CultureInfo.InvariantCulture, $"not a GitHub pull request name: {request.Owner}/{request.Repo}#{request.Number}"));
        }

        var approved = await approvals.ForRead(cancellationToken).ConfigureAwait(false);
        if (approved.IsError)
        {
            return approved.Error;
        }

        var review = approved.Value?.Profile.Review;
        var login = await Login(review, cancellationToken).ConfigureAwait(false);
        if (login is null)
        {
            return SindriErrors.ReviewNoLogin.With("Set your GitHub login on the Reviews tab first.");
        }

        var stored = await store.Read(pull, cancellationToken).ConfigureAwait(false);
        if (stored.IsError)
        {
            return stored.Error;
        }

        return await Page(new ReviewPageAsk(pull, login, review ?? new PrReviewSettings(), stored.Value, stored.Value.Url ?? string.Create(CultureInfo.InvariantCulture, $"https://github.com/{pull.Owner}/{pull.Repo}/pull/{pull.Number}")), cancellationToken).ConfigureAwait(false);
    }

    // why: 'review.login' is the owner's override; without it the GitHub connection's login stands in
    private async ValueTask<string?> Login(PrReviewSettings? review, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(review?.Login))
        {
            return review.Login;
        }

        var connection = await account.Current(cancellationToken).ConfigureAwait(false);
        return connection.IsSuccess && !string.IsNullOrWhiteSpace(connection.Value.Login) ? connection.Value.Login : null;
    }

    private async ValueTask<Result<ReviewPageModel>> Page(ReviewPageAsk ask, CancellationToken cancellationToken)
    {
        var read = await reader.Read(ask.Link, cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            // why: a refused credential or host still serves the cache, but says so; "offline" alone would send the owner looking at their network
            return read.Error.Kind == ErrorKind.NotFound
                ? SindriErrors.ReviewPrNotFound.With("PR not found")
                : await cache.Offline(ask.Pull, read.Error.Kind == ErrorKind.Forbidden ? read.Error.Code : null, cancellationToken).ConfigureAwait(false);
        }

        var made = await facts.Read(ask, read.Value, cancellationToken).ConfigureAwait(false);
        var page = ReviewPageBuilder.Build(made);
        await cache.Save(ask.Pull, page, made, cancellationToken).ConfigureAwait(false);
        return page;
    }
}
