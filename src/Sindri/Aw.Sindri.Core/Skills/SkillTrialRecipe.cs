using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Skills;

/// <summary>
/// The one-state recipe 'sindri skill run' runs a block in (skill flows spec §7): a single active state around 'step.skill.&lt;id&gt;' whose inputs are start facts, one terminal state per outcome and one for a failed Step.
/// Its id is 'workflow.skill-trial-&lt;block&gt;', which is how the launch knows it is a trial (<see cref="IsTrial(RecipeId)"/>: no push grant, 'trial' on launch.started). Its version comes from the manifest hash and the inputs given, so the same block and
/// inputs register as the same recipe and an edited manifest as a new version, never SND-RECIPE-007.
/// </summary>
public static class SkillTrialRecipe
{
    /// <summary>The id prefix of every trial recipe.</summary>
    public const string IdPrefix = "workflow.skill-trial-";

    private const string EndedPrefix = "ended-";

    /// <summary>The fact the trial's start carries when 'skill run --skills &lt;dir&gt;' sent a folder: the path the launch seeds the skill from.</summary>
    public const string SkillsFolderFact = "fact:skillTrialFolder";

    /// <summary>Whether <paramref name="recipe"/> is a trial recipe.</summary>
    /// <param name="recipe">The recipe id.</param>
    /// <returns>True for 'workflow.skill-trial-…'.</returns>
    public static bool IsTrial(RecipeId recipe) => recipe.Value.StartsWith(IdPrefix, StringComparison.Ordinal);

    /// <summary>Whether <paramref name="instance"/> is a trial run, from its address ('&lt;item&gt;/skill-trial-&lt;block&gt;/&lt;attempt&gt;', or a child of one).</summary>
    /// <param name="instance">The instance address.</param>
    /// <returns>True for a trial's.</returns>
    public static bool IsTrial(Workflow.Engine.InstanceAddress instance) =>
        instance.Value.Split('/') is [_, var recipe, ..] && recipe.StartsWith(IdPrefix["workflow.".Length..], StringComparison.Ordinal);

    /// <summary>The fact an input arrives in: 'fact:&lt;key&gt;'.</summary>
    /// <param name="inputKey">The input's key.</param>
    /// <returns>The fact key; an error when the key can't be a fact key.</returns>
    public static Result<FactKey> FactOf(string inputKey)
    {
        ArgumentNullException.ThrowIfNull(inputKey);
        return FactKey.TryFrom("fact:" + inputKey, out var key)
            ? key
            : SindriErrors.InvalidInput.With($"input '{inputKey}' can't be a fact key (fix: start the key with a lower-case letter and use letters and digits only)");
    }

    /// <summary>Builds the trial recipe of a block for the inputs given.</summary>
    /// <param name="block">The loaded block.</param>
    /// <param name="provided">The input keys the run gives a value for.</param>
    /// <param name="foldered">True when the run carries a skills folder (<see cref="SkillsFolderFact"/>), which the recipe then declares as an input.</param>
    /// <returns>The recipe's id, version and text; an error when the id or an input key is not legal.</returns>
    public static Result<SkillTrialPlan> Build(LoadedSkillBlock block, IReadOnlyCollection<string> provided, bool foldered = false)
    {
        ArgumentNullException.ThrowIfNull(block);
        ArgumentNullException.ThrowIfNull(provided);
        if (!RecipeId.TryFrom(IdPrefix + block.Manifest.Name, out var id))
        {
            return SindriErrors.InvalidInput.With($"block '{block.Manifest.Name}' is too long to have a trial recipe (fix: shorten its id in block.yaml)");
        }

        var given = block.Manifest.Inputs.Where(i => provided.Contains(i.Key, StringComparer.Ordinal)).ToList();
        foreach (var input in given)
        {
            if (FactOf(input.Key) is { IsError: true } bad)
            {
                return bad.Error;
            }
        }

        var version = VersionOf(block.ManifestHash, foldered ? [.. given.Select(static i => i.Key), SkillsFolderFact] : given.Select(static i => i.Key));
        return new SkillTrialPlan(id, version, Text(block, id, version, given, foldered));
    }

    private static int VersionOf(string manifestHash, IEnumerable<string> keys)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(manifestHash + "|" + string.Join(',', keys.Order(StringComparer.Ordinal))));
        return (int)(BitConverter.ToUInt32(bytes, 0) & 0x7fffffff) | 1;   // positive, and never 0
    }

    private static string Text(LoadedSkillBlock block, RecipeId id, int version, List<SkillInput> given, bool foldered)
    {
        var manifest = block.Manifest;
        var outcomes = block.Descriptor.Outcomes.Select(static o => o.Value).ToList();
        var text = new StringBuilder();
        _ = text.Append("schema: 1\n")
            .Append(CultureInfo.InvariantCulture, $"id: {id.Value}\nversion: {version}\n")
            .Append("description: ").Append(Q($"Trial of the {manifest.Name} skill block: 'sindri skill run' starts it on its own."))
            .Append("\ndisplayName: ").Append(Q($"Try the {manifest.Name} block")).Append('\n');
        _ = text.Append(given.Count == 0 && !foldered ? "inputs: []\n" : "inputs:\n");
        foreach (var input in given)
        {
            _ = text.Append("  - { key: fact:").Append(input.Key).Append(", label: ").Append(Q(input.Label)).Append(", type: text, kind: text }\n");
        }

        if (foldered)
        {
            _ = text.Append("  - { key: ").Append(SkillsFolderFact).Append(", label: \"Skills folder\", type: text, kind: text }\n");
        }

        _ = text.Append("initial: run\nstates:\n  run: { kind: active, step: { use: step.skill.").Append(manifest.Name).Append(", with: {");
        _ = text.AppendJoin(',', given.Select(static i => $" {i.Key}: {{ fact: fact:{i.Key} }}"));
        _ = text.Append(" } }, timeout: { after: ").Append(Duration(manifest.MaxDuration)).Append(", then: park }, next: ").Append(Q($"Run the {manifest.Name} skill.")).Append(" }\n");
        foreach (var outcome in outcomes)
        {
            // why: a terminal's outcome is the recipe's closed set (RCP-OUTCOME-001); the block's own outcome lives in the state's name (OutcomeOf)
            var ends = string.Equals(outcome, manifest.Outcomes.Success, StringComparison.Ordinal) ? "resolved" : "unresolved";
            _ = text.Append("  ").Append(StateOf(outcome)).Append(": { kind: terminal, outcome: ").Append(ends).Append(" }\n");
        }

        _ = text.Append("  failed: { kind: terminal, outcome: failed }\ntransitions:\n");
        var produced = string.Join(", ", block.Descriptor.Outputs.Select(static o => o.Fact.Value));
        foreach (var outcome in outcomes)
        {
            var produces = string.Equals(outcome, manifest.Outcomes.Success, StringComparison.Ordinal) && produced.Length > 0 ? $", produces: [{produced}]" : string.Empty;
            _ = text.Append("  - { from: run, to: ").Append(StateOf(outcome)).Append(", on: step.completed, when: { outcome: ").Append(outcome).Append(" }").Append(produces).Append(" }\n");
        }

        _ = text.Append("  - { from: run, to: failed, on: step.failed }\n")
            .Append("evals: [evals/workflow.skill-trial/trial.jsonl]\n");   // why: approving needs a declared eval (SND-RECIPE-008); a trial is the owner's own request, so the file is a name only
        return text.ToString();
    }

    /// <summary>The block outcome a trial ended with, from its terminal state: 'ended-needs-changes' is 'needsChanges'.</summary>
    /// <param name="state">The instance's state.</param>
    /// <returns>The outcome; null for 'failed' or a state that isn't an outcome's.</returns>
    public static string? OutcomeOf(string state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!state.StartsWith(EndedPrefix, StringComparison.Ordinal) || state.Length == EndedPrefix.Length)
        {
            return null;
        }

        var words = state[EndedPrefix.Length..].Split('-');
        return words[0] + string.Concat(words.Skip(1).Where(static w => w.Length > 0).Select(static w => char.ToUpperInvariant(w[0]) + w[1..]));
    }

    /// <summary>The terminal state of an outcome: 'needsChanges' ends in 'ended-needs-changes'.</summary>
    private static string StateOf(string outcome) =>
        EndedPrefix + string.Concat(outcome.Select(static (c, i) => char.IsUpper(c) ? (i == 0 ? string.Empty : "-") + char.ToLowerInvariant(c) : c.ToString()));

    private static string Duration(TimeSpan span) =>
        span.TotalHours >= 1 && span.TotalMinutes % 60 == 0 ? string.Create(CultureInfo.InvariantCulture, $"{(long)span.TotalHours}h") : string.Create(CultureInfo.InvariantCulture, $"{Math.Max(1, (long)Math.Ceiling(span.TotalMinutes))}m");

    /// <summary>A double-quoted YAML scalar: JSON's string escapes are valid YAML ones.</summary>
    private static string Q(string value) => "\"" + JavaScriptEncoder.Default.Encode(value) + "\"";
}
