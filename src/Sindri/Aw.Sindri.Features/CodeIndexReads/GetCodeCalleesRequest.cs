using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.CodeIndexReads;

/// <summary>The edges out of a symbol (GET /sindri/code-index/callees?ref=&amp;depth=&amp;repo=).</summary>
/// <param name="Ref">The symbol's ref, or 'path#name' with <paramref name="Repo"/>.</param>
/// <param name="Depth">1 to 8 (default 1).</param>
/// <param name="Repo">The repo, when the ref does not start with it.</param>
public sealed record GetCodeCalleesRequest(string Ref, int Depth = 1, string? Repo = null) : IQuery<CodeCallsView>;
