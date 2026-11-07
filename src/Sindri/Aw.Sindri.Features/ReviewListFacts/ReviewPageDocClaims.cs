using Aw.Context.Core.Library;
using Aw.Context.Core.Sources;

namespace Aw.Sindri.Features.ReviewListFacts;

/// <summary>Who claims what in one doc: the items this PR claims, every claim on the doc, and this PR's own key (its claims are not "claimed by" others).</summary>
/// <param name="ClaimedHere">The ids of the items this PR claims.</param>
/// <param name="OnDoc">The claims on the doc from open and merged PRs.</param>
/// <param name="Self">This PR's record key.</param>
internal sealed record ReviewPageDocClaims(IReadOnlySet<string> ClaimedHere, IReadOnlyList<LibraryClaim> OnDoc, RecordKey Self);
