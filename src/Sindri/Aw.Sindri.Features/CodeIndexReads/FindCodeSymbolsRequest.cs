using Aw.CodeIndex.Core.Facade;
using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.CodeIndexReads;

/// <summary>A symbol search (GET /sindri/code-index/symbols?query=&amp;repo=&amp;match=&amp;concept=): by name, path, path#name or ref; exact unless the match is prefix.</summary>
/// <param name="Query">The text.</param>
/// <param name="Repo">One repo, or null for every repo in scope.</param>
/// <param name="Match">'exact' (the default) or 'prefix'.</param>
/// <param name="Concept">A pin id (or 'concept:&lt;id&gt;') to search inside, or null for the whole index.</param>
public sealed record FindCodeSymbolsRequest(string Query, string? Repo = null, string? Match = null, string? Concept = null) : IQuery<FindSymbolsAnswer>;
