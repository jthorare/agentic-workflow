using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.CodeIndexReads;

/// <summary>The edges into a symbol (GET /sindri/code-index/callers?ref=&amp;depth=&amp;repo=&amp;implementations=).</summary>
/// <param name="Ref">The symbol's ref, or 'path#name' with <paramref name="Repo"/>.</param>
/// <param name="Depth">1 to 8 (default 1).</param>
/// <param name="Repo">The repo, when the ref does not start with it.</param>
/// <param name="Implementations">True to follow 'inherits' edges from an interface or base member to its implementations.</param>
public sealed record GetCodeCallersRequest(string Ref, int Depth = 1, string? Repo = null, bool Implementations = false) : IQuery<CodeCallsView>;
