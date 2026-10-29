using System.Text.RegularExpressions;
using Aw.Context.Core.Models;

namespace Aw.Context.Core.Engine;

/// <summary>
/// What "cheap" and "strong" mean on each harness ('context.models.names'). A null name is one the profile didn't write; the built-in table
/// (<see cref="BuiltIn"/>) fills it.
/// </summary>
/// <param name="ClaudeCode">'names.claude-code'.</param>
/// <param name="Codex">'names.codex'.</param>
/// <param name="Cursor">'names.cursor'.</param>
public sealed partial record ModelNames(CliModels ClaudeCode, CliModels Codex, CliModels Cursor)
{
    /// <summary>Nothing written.</summary>
    public static ModelNames None { get; } = new(new CliModels(Cheap: null, Strong: null), new CliModels(Cheap: null, Strong: null), new CliModels(Cheap: null, Strong: null));

    /// <summary>The names a profile that says nothing gets: haiku and sonnet, gpt-6-luna and gpt-5.5, gpt-5.6-luna-none-fast and gpt-5 (spec 3.3).</summary>
    public static ModelNames BuiltIn { get; } = new(
        new CliModels(BuiltInName(ModelHarness.ClaudeCode, ModelTier.Cheap), BuiltInName(ModelHarness.ClaudeCode, ModelTier.Strong)),
        new CliModels(BuiltInName(ModelHarness.Codex, ModelTier.Cheap), BuiltInName(ModelHarness.Codex, ModelTier.Strong)),
        new CliModels(BuiltInName(ModelHarness.Cursor, ModelTier.Cheap), BuiltInName(ModelHarness.Cursor, ModelTier.Strong)));

    /// <summary>The name the built-in table gives a harness at a tier.</summary>
    /// <param name="harness">The harness.</param>
    /// <param name="tier">The tier.</param>
    /// <returns>The model name.</returns>
    public static string BuiltInName(ModelHarness harness, ModelTier tier) => (harness, tier) switch
    {
        (ModelHarness.ClaudeCode, ModelTier.Strong) => "sonnet",
        (ModelHarness.ClaudeCode, _) => "haiku",
        (ModelHarness.Codex, ModelTier.Strong) => "gpt-5.5",
        (ModelHarness.Codex, _) => "gpt-6-luna",
        (ModelHarness.Cursor, ModelTier.Strong) => "gpt-5",
        (ModelHarness.Cursor, _) => "gpt-5.6-luna-none-fast",
        _ => throw new ArgumentOutOfRangeException(nameof(harness)),
    };

    /// <summary>The name the profile wrote for a harness at a tier.</summary>
    /// <param name="harness">The harness.</param>
    /// <param name="tier">The tier.</param>
    /// <returns>The name, or null when the profile wrote none.</returns>
    public string? Written(ModelHarness harness, ModelTier tier)
    {
        var names = Of(harness);
        return tier == ModelTier.Strong ? names.Strong : names.Cheap;
    }

    /// <summary>The names of one harness.</summary>
    /// <param name="harness">The harness.</param>
    /// <returns>Its cheap and strong names.</returns>
    public CliModels Of(ModelHarness harness) => harness switch
    {
        ModelHarness.ClaudeCode => ClaudeCode,
        ModelHarness.Codex => Codex,
        ModelHarness.Cursor => Cursor,
        _ => throw new ArgumentOutOfRangeException(nameof(harness)),
    };

    /// <summary>Whether text is a model name a CLI's --model may be given: 1 to 100 letters, digits and . _ : / @ + -, not starting with -, and no '..'.</summary>
    /// <param name="name">The name.</param>
    /// <returns>True when it is.</returns>
    public static bool IsValid(string? name) => name is not null && Shape.IsMatch(name);

    [GeneratedRegex("^(?!.*\\.\\.)[A-Za-z0-9][A-Za-z0-9._:/@+-]{0,99}\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }
}
