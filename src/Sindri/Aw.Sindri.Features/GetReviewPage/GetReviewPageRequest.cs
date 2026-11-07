using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Reviews;

namespace Aw.Sindri.Features.GetReviewPage;

/// <summary>The PR review page (GET /sindri/reviews/{owner}/{repo}/{number}; PR review page spec §6.2).</summary>
/// <param name="Owner">The account in the path.</param>
/// <param name="Repo">The repository in the path.</param>
/// <param name="Number">The PR number in the path.</param>
public sealed record GetReviewPageRequest(string Owner, string Repo, int Number) : IQuery<ReviewPageModel>;
