using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Reviews;

namespace Aw.Sindri.Features.GetReviewPage;

/// <summary>What a page read is for: the PR, the owner's login and review block, the stored side of the PR and the link the forge takes.</summary>
/// <param name="Pull">The PR.</param>
/// <param name="Login">review.login.</param>
/// <param name="Review">The approved profile's review block.</param>
/// <param name="Stored">What Context stores about the PR.</param>
/// <param name="Link">The PR's link on the forge.</param>
internal sealed record ReviewPageAsk(ReviewPullRef Pull, string Login, PrReviewSettings Review, ReviewPageStored Stored, string Link);
