namespace Aw.Sindri.Core.Reviews;

/// <summary>The conversation panel.</summary>
/// <param name="Threads">The threads.</param>
public sealed record ConversationData(IReadOnlyList<ReviewPageThread> Threads) : ReviewPanelData;
