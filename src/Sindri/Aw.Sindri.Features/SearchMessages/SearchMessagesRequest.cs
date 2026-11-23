using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.SearchMessages;

/// <summary>Search (GET /sindri/messages/search?q=&amp;scope=, 'sindri messages search'; messages spec §8.2, §9.8). Release 1 serves the local scope only.</summary>
/// <param name="Query">The query, with its operators.</param>
/// <param name="Scope">Where to look.</param>
public sealed record SearchMessagesRequest(string Query, MessageSearchScope Scope) : IQuery<SearchMessagesResponse>;
