using Aw.Judge.Core.Briefs;

namespace Aw.Judge.Features.SaveBrief;

/// <summary>The SaveBrief response.</summary>
/// <param name="Brief">The brief as saved: unmapped, stamped with the save time.</param>
public sealed record SaveBriefResponse(Brief Brief);
