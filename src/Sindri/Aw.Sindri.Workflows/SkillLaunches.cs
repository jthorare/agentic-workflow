using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Skills;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>Makes the <see cref="SkillLaunch"/> of a Step that runs a skill block (skill flows spec §3.3): the loaded block, its inputs from the Step's 'with:' values, and whose invocation form the seed takes.</summary>
internal static class SkillLaunches
{
    /// <summary>The instance fact a trial run ('sindri skill run') stores: the folder given to '--skills'. A trial is a run of a \'workflow.skill-trial-*\' recipe; with the fact set it reads the skill from that folder by path.</summary>
    internal static readonly FactKey TrialFact = FactKey.From(SkillTrialRecipe.SkillsFolderFact);

    /// <summary>The launch of the Step's skill block.</summary>
    /// <param name="run">The run the launch is for; one with no 'with:' values runs no skill block.</param>
    /// <param name="block">The Step's block.</param>
    /// <param name="agent">The agent the Step runs on.</param>
    /// <param name="inputs">The loaded blocks, and the stored facts.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The launch; null when the Step runs no skill block; an error when the block isn't loaded or a fact can't be read.</returns>
    internal static async ValueTask<Result<SkillLaunch?>> Of(AgentLaunchRun run, BlockId block, AgentId agent, AgentLaunchInputs inputs, CancellationToken cancellationToken)
    {
        if (run.SkillWith is not { } with)
        {
            return (SkillLaunch?)null;
        }

        var (trial, trialFolder) = await TrialDirectory(run, inputs, cancellationToken).ConfigureAwait(false);
        var blocks = trialFolder is { } folder ? inputs.SkillBlocks.SelectMany(b => b.LoadFolder(folder).Loaded) : inputs.SkillBlocks.SelectMany(static b => b.Loaded);
        var loaded = blocks.FirstOrDefault(b => b.Descriptor.Id == block);
        if (loaded is null)
        {
            return SindriErrors.RequestInvalid.With($"the skill block {block.Value} is not loaded (fix: check 'sindri skills' for the reason, then restart serve)");
        }

        Dictionary<string, string> values = new(StringComparer.Ordinal);
        foreach (var input in loaded.Manifest.Inputs)
        {
            if (!with.TryGetValue(input.Key, out var given))
            {
                continue;
            }

            var text = await ValueOf(given, run, inputs, cancellationToken).ConfigureAwait(false);
            if (text.IsError)
            {
                return text.Error;
            }

            if (text.Value is { } value)
            {
                values[input.Key] = value;
            }
        }

        var target = agent.Value.StartsWith("codex", StringComparison.Ordinal) ? SkillTarget.Codex : SkillTarget.ClaudeCode;
        return loaded.LaunchWith(target, values, trial) with { TrialSkillsDirectory = trialFolder };
    }

    // A literal as the text it prints; a fact as its stored text (a JSON string unwrapped), scrubbed; a profile value is no input.
    private static async ValueTask<Result<string?>> ValueOf(Core.Workflow.Recipes.WithValue given, AgentLaunchRun run, AgentLaunchInputs inputs, CancellationToken cancellationToken)
    {
        if (given.Literal is { } literal)
        {
            return literal.ValueKind == JsonValueKind.String ? literal.GetString() : literal.GetRawText();
        }

        if (given.Fact is not { } fact || run.Facts is null || !run.Facts.TryGetValue(fact, out var stored))
        {
            return (string?)null;
        }

        var read = await inputs.Contents.Read(stored, cancellationToken).ConfigureAwait(false);
        return read.IsError ? read.Error : Unwrapped(inputs.Scrubber.Scrub(System.Text.Encoding.UTF8.GetString(read.Value.Span)));
    }

    internal static string Unwrapped(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            return document.RootElement.ValueKind == JsonValueKind.String ? document.RootElement.GetString() ?? string.Empty : text;
        }
        catch (JsonException)
        {
            return text;   // boundary: stored text that isn't JSON is the text itself
        }
    }

    private static async ValueTask<(bool Trial, string? Folder)> TrialDirectory(AgentLaunchRun run, AgentLaunchInputs inputs, CancellationToken cancellationToken)
    {
        var trial = run.Recipe is { } recipe && SkillTrialRecipe.IsTrial(recipe);
        if (!trial || run.Facts is null || !run.Facts.TryGetValue(TrialFact, out var stored))
        {
            return (trial, null);
        }

        var read = await inputs.Contents.Read(stored, cancellationToken).ConfigureAwait(false);
        return read.IsError || Unwrapped(System.Text.Encoding.UTF8.GetString(read.Value.Span)) is not { Length: > 0 } folder ? (trial, null) : (trial, folder);
    }
}
