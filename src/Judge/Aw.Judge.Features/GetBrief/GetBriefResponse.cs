using Aw.Judge.Core.Briefs;

namespace Aw.Judge.Features.GetBrief;

/// <summary>The GetBrief response.</summary>
/// <param name="Brief">The brief the tool call saved.</param>
public sealed record GetBriefResponse(Brief Brief);
