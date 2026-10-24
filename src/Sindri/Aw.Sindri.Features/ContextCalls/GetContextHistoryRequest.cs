using Aw.Context.Core.History;
using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>The history lens of one or more anchors (GET /sindri/context/history?anchor=&amp;since=&amp;before=; 'sindri context history').</summary>
/// <param name="Anchors">Node keys, names or aliases, repeats included.</param>
/// <param name="Since">The earliest instant kept (RFC 3339 or a date), or null.</param>
/// <param name="Before">The world as of this instant (inclusive), or null for today.</param>
/// <param name="Draft">True to read the draft map.</param>
/// <param name="MapVersion">A retained map version, or null for the current one; not valid with <paramref name="Draft"/>.</param>
public sealed record GetContextHistoryRequest(IReadOnlyList<string> Anchors, string? Since = null, string? Before = null, bool Draft = false, string? MapVersion = null) : IQuery<HistoryView>;
