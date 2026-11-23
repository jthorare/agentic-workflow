using Aw.Sindri.Core.Messages;

namespace Aw.Sindri.Features.SearchMessages;

/// <summary>Search hits (messages spec §9.8).</summary>
/// <param name="Hits">The matching lines, best first.</param>
/// <param name="Limited">True when the scope could not be searched in full (its limited state, §6.6).</param>
public sealed record SearchMessagesResponse(IReadOnlyList<MessageLine> Hits, bool Limited);
