using Aw.Context.Core.Engine;

namespace Aw.Context.Features.CheckPages;

/// <summary>The doctor check of the approved profile's pages; <paramref name="Scope"/> is the approved profile's, or null when none is approved.</summary>
/// <param name="Scope">The scope whose pages are checked.</param>
internal sealed record CheckPagesRequest(ContextScope? Scope);
