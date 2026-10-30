namespace Aw.Context.Core.Models;

/// <summary>How much one prompt to the labeling route's harnesses may hold (spec §6.2): the smallest budget among them, so a pack sized for it fits whichever harness answers.</summary>
/// <param name="Chars">The prompt characters.</param>
/// <param name="MaxItems">The most clusters in a pack.</param>
public sealed record PromptBudget(int Chars, int MaxItems)
{
    /// <summary>Claude Code and Codex: 60,000 characters, 50 clusters (74 would fit; 50 keeps a bad answer small).</summary>
    public static PromptBudget Wide { get; } = new(60_000, 50);

    /// <summary>Cursor: its CLI takes the prompt as an argument and cuts it at 30,000 characters, so a pack is 30 clusters at most.</summary>
    public static PromptBudget Narrow { get; } = new(30_000, 30);
}
