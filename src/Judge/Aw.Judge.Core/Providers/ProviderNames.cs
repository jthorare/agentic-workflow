using System.Diagnostics.CodeAnalysis;
using Aw.Judge.Core.Errors;
using Aw.Kernel;

namespace Aw.Judge.Core.Providers;

/// <summary>The providers' wire names, as the CLI flag, the config file and the decision rows spell them.</summary>
public static class ProviderNames
{
    /// <summary>The wire name, e.g. 'claude-cli'.</summary>
    /// <param name="value">The provider.</param>
    /// <returns>The name.</returns>
    public static string Name(ProviderName value) => value switch
    {
        ProviderName.Rules => "rules",
        ProviderName.ClaudeCli => "claude-cli",
        ProviderName.CodexCli => "codex-cli",
        ProviderName.CursorCli => "cursor-cli",
        ProviderName.Jev => "jev",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "unknown provider"),
    };

    /// <summary>Reads a wire name, exactly (no trimming, case-sensitive), as the TS isProviderName does.</summary>
    /// <param name="text">The name.</param>
    /// <param name="value">The provider, when the name is one.</param>
    /// <returns>Whether it is a provider name.</returns>
    public static bool TryParse([NotNullWhen(true)] string? text, out ProviderName value)
    {
        (var ok, value) = text switch
        {
            "rules" => (true, ProviderName.Rules),
            "claude-cli" => (true, ProviderName.ClaudeCli),
            "codex-cli" => (true, ProviderName.CodexCli),
            "cursor-cli" => (true, ProviderName.CursorCli),
            "jev" => (true, ProviderName.Jev),
            _ => (false, default),
        };
        return ok;
    }

    /// <summary>
    /// Reads a comma-separated provider list (the providers allowlist): names trimmed, empty entries skipped, duplicates
    /// dropped in first-seen order.
    /// </summary>
    /// <param name="raw">The list, e.g. 'jev, claude-cli'.</param>
    /// <returns>The providers, or AW-JUDGE-010 when the list is empty or names an unknown provider.</returns>
    public static Result<IReadOnlyList<ProviderName>> ParseList(string raw)
    {
        ArgumentNullException.ThrowIfNull(raw);
        var names = raw.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
        if (names.Count == 0)
        {
            return Results.Failure<IReadOnlyList<ProviderName>>(JudgeErrors.ProvidersFlag.With("--providers list is empty"));
        }

        var bad = names.Where(n => !TryParse(n, out _)).ToList();
        if (bad.Count > 0)
        {
            return Results.Failure<IReadOnlyList<ProviderName>>(JudgeErrors.ProvidersFlag.With($"unknown provider(s): {string.Join(", ", bad)}"));
        }

        IReadOnlyList<ProviderName> allowed = [.. names.Select(n => TryParse(n, out var p) ? p : default).Distinct()];
        return Results.Success(allowed);
    }
}
