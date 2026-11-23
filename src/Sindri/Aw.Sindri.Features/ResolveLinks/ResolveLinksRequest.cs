using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.ResolveLinks;

/// <summary>Resolves links in message text to rows (messages spec §8.2, §9.3, #3021): at most 20 urls. A read with a body, so a query (the spec writes POST; the route is api's to confirm in M1g).</summary>
/// <param name="Urls">The urls, at most 20.</param>
public sealed record ResolveLinksRequest(IReadOnlyList<Uri> Urls) : IQuery<ResolveLinksResponse>;
