namespace Aw.Sindri.Features.ListOutbox;

/// <summary>The effects, one per emission, oldest first; empty prints 'no commands' (engine spec §6.3).</summary>
/// <param name="Commands">The effects.</param>
public sealed record ListOutboxResponse(IReadOnlyList<OutboxCommandView> Commands);
