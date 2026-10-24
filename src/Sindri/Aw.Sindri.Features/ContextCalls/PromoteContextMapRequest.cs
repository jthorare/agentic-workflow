using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// Make the latest draft the current map (context spec §8.7): a human or a session may do it, with no typed challenge (removed per owner q93,
/// 2026-10-23). A draft with undecided review items, or none, is SND-CONTEXT-046.
/// </summary>
/// <param name="Draft">The draft the caller expects ('v14@0a1b2c3'), or null for whatever is latest; another latest draft is SND-CONTEXT-046.</param>
public sealed record PromoteContextMapRequest(string? Draft = null) : ICommand<PromoteContextMapResponse>;
