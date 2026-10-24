using Aw.Judge.Core.Config;

namespace Aw.Judge.Core.PromptSort;

/// <summary>The prompt sorter's settings: config.json's 'promptSort' block over the defaults (shadow mode: every scaffold off).</summary>
/// <param name="Enabled">Whether the sorter runs.</param>
/// <param name="Budget">The time Jev gets.</param>
/// <param name="CooldownPrompts">Prompts between two firings of one scaffold.</param>
/// <param name="Scaffolds">Each scaffold's switch.</param>
public sealed record PromptSortSettings(bool Enabled, TimeSpan Budget, int CooldownPrompts, IReadOnlyDictionary<string, bool> Scaffolds)
{
    /// <summary>The defaults: on, 1000 ms, a cooldown of 5 prompts, every scaffold off.</summary>
    public static PromptSortSettings Default { get; } = From(stored: null);

    /// <summary>Resolves the stored overrides over the defaults.</summary>
    /// <param name="stored">config.json's promptSort block, or null.</param>
    /// <returns>The settings.</returns>
    public static PromptSortSettings From(PromptSortConfig? stored)
    {
        var scaffolds = ProviderNames.Scaffolds.ToDictionary(static id => id, static _ => false, StringComparer.Ordinal);
        foreach (var (id, on) in stored?.Scaffolds ?? new Dictionary<string, bool>(StringComparer.Ordinal))
        {
            scaffolds[id] = on;
        }

        return new PromptSortSettings(stored?.Enabled ?? true, TimeSpan.FromMilliseconds(stored?.BudgetMs ?? 1000), (int)(stored?.CooldownPrompts ?? 5), scaffolds);
    }
}
