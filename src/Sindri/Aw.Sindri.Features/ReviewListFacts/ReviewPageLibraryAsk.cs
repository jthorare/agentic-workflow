using Aw.Context.Core.Audience;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Records;

namespace Aw.Sindri.Features.ReviewListFacts;

/// <summary>What a library read of one PR is for.</summary>
/// <param name="Settings">The checked settings.</param>
/// <param name="Scope">The scope of the current caller.</param>
/// <param name="Audience">What the caller may read.</param>
/// <param name="Slug">The PR's repo, owner/name.</param>
/// <param name="Record">The PR's stored record; null when it has none.</param>
internal sealed record ReviewPageLibraryAsk(ContextSettings Settings, ContextScope Scope, AudienceScope Audience, string Slug, StoredRecord? Record);
