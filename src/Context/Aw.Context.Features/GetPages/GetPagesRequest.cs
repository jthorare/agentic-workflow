using Aw.Context.Core.Engine;
using Aw.Context.Core.Pages;

namespace Aw.Context.Features.GetPages;

/// <summary>One slice of the product's pages.</summary>
/// <param name="Query">The filter, the slice and which map.</param>
/// <param name="Scope">The profile's context section and repos, and the caller.</param>
internal sealed record GetPagesRequest(PagesQuery Query, ContextScope Scope);
