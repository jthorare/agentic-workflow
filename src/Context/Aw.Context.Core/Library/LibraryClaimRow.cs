using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Library;

/// <summary>A stored claim and the PR record it belongs to.</summary>
/// <param name="Pr">The PR record.</param>
/// <param name="PrAudience">The PR's audience; null is visible to no one.</param>
/// <param name="PrState">The PR's state.</param>
/// <param name="Ref">The ref as written.</param>
public sealed record LibraryClaimRow(RecordKey Pr, AudienceKey? PrAudience, RecordState? PrState, string Ref);
