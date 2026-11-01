namespace Aw.Context.Core.Engine;

/// <summary>The profile names of the harnesses, and the default order they are tried in.</summary>
public static class ModelHarnesses
{
    /// <summary>The order the harnesses are tried in when the profile names none: claude-code, codex, cursor.</summary>
    public static IReadOnlyList<ModelHarness> DefaultOrder { get; } = [ModelHarness.ClaudeCode, ModelHarness.Codex, ModelHarness.Cursor];

    /// <summary>The name the profile and the ledger use.</summary>
    /// <param name="harness">The harness.</param>
    /// <returns>'claude-code', 'codex' or 'cursor'.</returns>
    public static string Name(ModelHarness harness) => harness switch
    {
        ModelHarness.ClaudeCode => "claude-code",
        ModelHarness.Codex => "codex",
        ModelHarness.Cursor => "cursor",
        _ => throw new ArgumentOutOfRangeException(nameof(harness)),
    };

    /// <summary>The harness a profile name stands for.</summary>
    /// <param name="name">The name as written.</param>
    /// <param name="harness">The harness, when the name is one.</param>
    /// <returns>Whether it is one.</returns>
    public static bool TryParse(string? name, out ModelHarness harness)
    {
        foreach (var candidate in DefaultOrder)
        {
            if (string.Equals(name, Name(candidate), StringComparison.Ordinal))
            {
                harness = candidate;
                return true;
            }
        }

        harness = default;
        return false;
    }

    /// <summary>The key a <see cref="Models.ModelRequest"/> uses for the harness in its per-CLI model map: 'claude', 'codex' or 'cursor'.</summary>
    /// <param name="harness">The harness.</param>
    /// <returns>The request key.</returns>
    public static string RequestKey(ModelHarness harness) => harness == ModelHarness.ClaudeCode ? "claude" : Name(harness);

    /// <summary>How much one prompt to the harness may hold (spec §6.2): Cursor's CLI takes the prompt as an argument and cuts it at 30,000 characters; the others take 60,000.</summary>
    /// <param name="harness">The harness.</param>
    /// <returns>The prompt characters and the most clusters in a pack.</returns>
    public static Models.PromptBudget PromptBudget(ModelHarness harness) => harness == ModelHarness.Cursor ? Models.PromptBudget.Narrow : Models.PromptBudget.Wide;
}
