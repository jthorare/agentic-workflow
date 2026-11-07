using Aw.Context.Core.Audience;
using Aw.Context.Core.Engine;

namespace Aw.Sindri.Features;

/// <summary>The settings, scope and audiences of one read.</summary>
/// <param name="Settings">The checked settings.</param>
/// <param name="Scope">The scope of the caller.</param>
/// <param name="Audience">What the caller may read.</param>
internal sealed record Scoped(ContextSettings Settings, ContextScope Scope, AudienceScope Audience);
