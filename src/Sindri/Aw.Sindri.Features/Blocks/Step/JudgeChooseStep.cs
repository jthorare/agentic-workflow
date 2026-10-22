using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Sindri.Core.Judge;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Blocks.Step;

/// <summary>
/// 'step.judge.choose' on the block runtime (DT spec 3.1, 8.2): asks the judge one choice question over the facts the recipe names, redacted and capped, and reports the choice as the outcome. An escalation for
/// 'below-threshold', 'no-provider-decided' or 'rule-timeout' is 'uncertain'; invalid input, a disabled question, a judge error or a timeout fails the Step, and the route sends that to its desk twin. 'fact:decision'
/// is {"choice":…,"reason":…,"confidence":…,"confidenceKind":"probability"|"self-reported","probabilities":…,"provider":…,"decisionId":…}, on every outcome. The providers are the runner's resolved list (the approved profile's, narrowed by the recipe).
/// </summary>
/// <param name="judge">The judge's choose question.</param>
/// <param name="scrubber">Redacts the facts' text before it leaves for the judge.</param>
internal sealed class JudgeChooseStep(IJudgeChoice judge, ITextScrubber scrubber) : IBlockStep, IBlockStepId
{
    internal const double DefaultThreshold = 0.6;

    internal const int FactTextCap = 16_000;

    private const string Name = "step.judge.choose";
    private const string Uncertain = "uncertain";

    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From(Name);

    /// <inheritdoc/>
    public async ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        var (request, failure) = Request(context);
        if (failure is not null || request is null)
        {
            return BlockStepResult.Failed(failure ?? $"{Name} has no request");
        }

        var answer = await judge.Choose(request, cancellationToken).ConfigureAwait(false);
        return answer.Status switch
        {
            ChooseStatus.Decided when answer.Choice is { } choice && request.Choices.Any(c => string.Equals(c.Name, choice, StringComparison.Ordinal)) => Decision(choice, answer),
            ChooseStatus.Decided => BlockStepResult.Failed($"{Name}: the judge chose '{answer.Choice}', which is not one of the choices"),
            ChooseStatus.Escalated => Decision(Uncertain, answer),
            ChooseStatus.Failed => BlockStepResult.Failed($"{Name}: {answer.Reason}"),
            _ => BlockStepResult.Failed($"{Name}: {answer.Reason}"),
        };
    }

    private static BlockStepResult Decision(string outcome, ChooseAnswer answer)
    {
        var probabilities = answer.Probabilities is { Count: > 0 } map
            ? new JsonObject(map.Select(static p => KeyValuePair.Create(p.Key, (JsonNode?)JsonValue.Create(p.Value))))
            : null;
        return StepFacts.Done(outcome, ("fact:decision", new JsonObject
        {
            ["choice"] = outcome,
            ["reason"] = answer.Reason,
            ["confidence"] = answer.Confidence,
            ["confidenceKind"] = ConfidenceKind(answer.Provider),
            ["probabilities"] = probabilities,
            ["provider"] = answer.Provider,
            ["decisionId"] = answer.DecisionRef,
        }));
    }

    private (ChooseRequest? Request, string? Failure) Request(StepContext context)
    {
        if (Text(context, "prompt") is not { Length: > 0 } prompt)
        {
            return (null, $"{Name} needs a 'prompt'");
        }

        var keys = Strings(context, "facts");
        if (keys.Count == 0)
        {
            return (null, $"{Name} needs 'facts' to read");
        }

        if (StepFacts.Missing(context, [.. keys]) is { Count: > 0 } missing)
        {
            return (null, $"{Name} needs {string.Join(", ", missing)}, which the instance does not hold");
        }

        var (choices, choicesFailure) = Choices(Strings(context, "choices"));
        var (threshold, thresholdFailure) = Threshold(context);
        if ((choicesFailure ?? thresholdFailure) is { } failure)
        {
            return (null, failure);
        }

        var providers = context.Arguments.ContainsKey("providers") ? Strings(context, "providers") : JudgeProviders.Default;
        var facts = keys.ToDictionary(static k => k, k => Cap(scrubber.Scrub(FactText(StepFacts.Read(context, k)))), StringComparer.Ordinal);
        return (new ChooseRequest(prompt, choices, facts, threshold, providers), null);
    }

    // A choice is '<outcome>: <criterion>' (RCP-CHOICE-*): the validator holds the recipe to it, so this is the run-time guard for a hand-edited one.
    private static (List<ChooseOption> Choices, string? Failure) Choices(List<string> entries)
    {
        var choices = new List<ChooseOption>();
        foreach (var entry in entries)
        {
            var at = entry.IndexOf(": ", StringComparison.Ordinal);
            if (at <= 0)
            {
                return ([], $"{Name}: the choice '{entry}' is not '<outcome>: <criterion>'");
            }

            choices.Add(new ChooseOption(entry[..at].Trim(), entry[(at + 2)..].Trim()));
        }

        return choices.Count == 0 ? ([], $"{Name} needs 'choices'") : (choices, null);
    }

    private static (double Threshold, string? Failure) Threshold(StepContext context)
    {
        var threshold = context.Arguments.TryGetValue("threshold", out var given) && given.ValueKind == JsonValueKind.Number ? given.GetDouble() : DefaultThreshold;
        return threshold is > 0 and <= 1
            ? (threshold, null)
            : (threshold, $"{Name}: the threshold {threshold.ToString(CultureInfo.InvariantCulture)} must be above 0 and at most 1");
    }

    // Jev sends probabilities; an agent CLI's confidence is its own word (DT spec 3.1, 3.4).
    private static string? ConfidenceKind(string? provider) => provider switch
    {
        null => null,
        JudgeProviders.Jev => "probability",
        _ => "self-reported",
    };

    private static string FactText(JsonNode? fact) => fact switch
    {
        null => string.Empty,
        JsonValue value when value.TryGetValue<string>(out var text) => text,
        _ => fact.ToJsonString(),
    };

    private static string Cap(string text) => text.Length <= FactTextCap ? text : text[..FactTextCap];

    private static string? Text(StepContext context, string name) =>
        context.Arguments.TryGetValue(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static List<string> Strings(StepContext context, string name) =>
        context.Arguments.TryGetValue(name, out var value) && value.ValueKind == JsonValueKind.Array
            ? [.. value.EnumerateArray().Where(static e => e.ValueKind == JsonValueKind.String).Select(static e => e.GetString() ?? string.Empty)]
            : [];
}
