using Aw.Context.Core.Engine;

namespace Aw.Context.Features.ScanConcepts;

/// <summary>Scans the latest draft map for product concepts and stores them as hints on that draft.</summary>
/// <param name="Scope">The profile's context section and repos, and the caller.</param>
/// <param name="ExpectedVersion">The draft the caller means, or null for whichever is latest.</param>
internal sealed record ScanConceptsRequest(ContextScope Scope, int? ExpectedVersion = null);
