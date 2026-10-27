using Aw.Context.Core.Models;

namespace Aw.Context.Core.Engine;

/// <summary>
/// The model each agent CLI runs for each tier ('context.conceptScan.models'). Only Claude Code has defaults, two aliases its `--model` takes;
/// Codex and Cursor keep their own default for both tiers until the profile names a model, because a wrong name makes the CLI fail.
/// </summary>
/// <param name="Claude">`claude --model`.</param>
/// <param name="Codex">`codex exec --model`.</param>
/// <param name="Cursor">`cursor-agent --model`.</param>
public sealed record ConceptScanModels(CliModels Claude, CliModels Codex, CliModels Cursor)
{
    /// <summary>Claude: haiku and sonnet; Codex and Cursor: none.</summary>
    public static ConceptScanModels Default { get; } = new(new CliModels("haiku", "sonnet"), new CliModels(Cheap: null, Strong: null), new CliModels(Cheap: null, Strong: null));

    /// <summary>The models to ask for at a tier, by CLI key ('claude', 'codex', 'cursor'); a CLI with no name is left out.</summary>
    /// <param name="tier">The tier.</param>
    /// <returns>The names.</returns>
    public IReadOnlyDictionary<string, string> For(ModelTier tier)
    {
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, cli) in new[] { ("claude", Claude), ("codex", Codex), ("cursor", Cursor) })
        {
            if ((tier == ModelTier.Strong ? cli.Strong : cli.Cheap) is { } name)
            {
                names[key] = name;
            }
        }

        return names;
    }
}
