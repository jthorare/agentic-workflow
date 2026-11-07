using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Features.SendReviewFix;

/// <summary>What a fix task did (PR review page spec §8.2): always 200 with these fields.</summary>
/// <param name="ItemId">The item made in Work.</param>
/// <param name="Posted">True when the thread was posted; false for a half-done send and for a contributor's PR (item only).</param>
/// <param name="PostCode">SND-REVIEW-006 on a half-done send; null otherwise.</param>
/// <param name="PostError">The half-done text; null otherwise.</param>
/// <param name="Went">babysitter, session or waiting.</param>
/// <param name="Session">"&lt;provider&gt;, &lt;working directory&gt;" when <paramref name="Went"/> is session; null otherwise.</param>
public sealed record SendReviewFixResponse(ItemId ItemId, bool Posted, string? PostCode, string? PostError, string Went, string? Session);
