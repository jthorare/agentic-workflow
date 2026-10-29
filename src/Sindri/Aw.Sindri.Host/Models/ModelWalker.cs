using System.Text.Json.Nodes;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Models;
using Aw.Judge.Core.Providers;
using Aw.Sindri.Core.Events;

namespace Aw.Sindri.Host.Models;

/// <summary>
/// One walk of a call's route (model routing spec §4 steps 3 to 6): the first harness that answers wins; a usage limit is remembered for the rest of the job and the walk goes on; any other miss goes on and is forgotten. Each harness
/// is a try in the call; a harness limited earlier in the job is skipped.
/// </summary>
/// <param name="limits">What the job's calls found limited.</param>
/// <param name="clock">Times the tries.</param>
internal sealed class ModelWalker(JobModelLimits limits, TimeProvider clock)
{
    private const string Decision = "answered";
    private const string TextKey = "text";
    private const string NoText = "no-text";

    internal async ValueTask<ModelAnswer?> Walk(ModelCall call, CancellationToken cancellationToken)
    {
        call.Pass = [];
        var question = Question(call.Request);
        foreach (var step in call.Route)
        {
            if (limits.IsLimited(call.Job, step.Harness))
            {
                Add(call, new ModelCallTry(ModelHarnesses.Name(step.Harness), step.Model, ModelCallOutcome.Skipped, "limited", Ms: 0, Source(step)));
            }
            else if (await Try(call, step, question, cancellationToken).ConfigureAwait(false) is { } answer)
            {
                return answer;
            }
        }

        return null;
    }

    private static QuestionRef Question(ModelRequest request) => new()
    {
        Name = "context-" + request.Purpose.ToString().ToLowerInvariant(),
        Outputs = [Decision],
        Prompt = request.Prompt,
        ContentClass = ContentClass.Code,
        ExtraProperties = new Dictionary<string, JsonObject>(StringComparer.Ordinal) { [TextKey] = new() { ["type"] = "string" } },
        Criteria = new Dictionary<string, string>(StringComparer.Ordinal) { [Decision] = "reply with the whole answer in the text field" },
        MinimalEnvironment = true,   // context calls carry repo text: no GH_TOKEN, AW_FORGE_TOKEN or cloud keys in the child (spec §4 "Environment")
    };

    private async ValueTask<ModelAnswer?> Try(ModelCall call, RoutedModel step, QuestionRef question, CancellationToken cancellationToken)
    {
        var name = ModelHarnesses.Name(step.Harness);
        var started = clock.GetTimestamp();
        var provider = call.Installed.First(i => i.Harness == step.Harness).Provider;
        var result = await provider.Decide(question with { Model = step.Model }, input: null, call.Request.Budget, cancellationToken).ConfigureAwait(false);
        var ms = (int)Math.Min(clock.GetElapsedTime(started).TotalMilliseconds, int.MaxValue);
        if (Text(result) is { } text)
        {
            Add(call, new ModelCallTry(name, step.Model, ModelCallOutcome.Answered, Reason: null, ms, Source(step)));
            return Answer(call.Request, (step, name), text, (result as ProviderDecided)?.Usage);
        }

        if (result is ProviderErrored { UsageLimit: true })
        {
            limits.Remember(call.Job, step.Harness);
            Add(call, new ModelCallTry(name, step.Model, ModelCallOutcome.Limit, "limit", ms, Source(step)));
            return null;
        }

        Add(call, new ModelCallTry(name, step.Model, ModelCallOutcome.Failed, result is ProviderDecided ? NoText : result.ReasonCode, ms, Source(step)));
        return null;
    }

    private static string? Text(ProviderResult result) =>
        result is ProviderDecided { Extra: { } extra } && extra[TextKey] is JsonValue value && value.TryGetValue<string>(out var text) && text.Length > 0 ? text : null;

    private static void Add(ModelCall call, ModelCallTry attempt)
    {
        call.Pass.Add(attempt);
        call.Tries.Add(attempt);
    }

    // The CLI's own token counts and model name when it gave them; else characters / 4 and the model asked for (spec 7.3).
    private static ModelAnswer Answer(ModelRequest request, (RoutedModel Step, string Name) at, string text, ProviderUsage? usage) => usage is null
        ? new ModelAnswer(text, Estimate(request.Prompt), Estimate(text), at.Step.Model) { Harness = at.Name, TokenBasis = TokenBasis.Estimated }
        : new ModelAnswer(text, (int)Math.Min(usage.InputTokens, int.MaxValue), (int)Math.Min(usage.OutputTokens, int.MaxValue), usage.Model ?? at.Step.Model) { Harness = at.Name, TokenBasis = TokenBasis.Reported };

    private static int Estimate(string text) => (text.Length + 3) / 4;

    private static string Source(RoutedModel step) => step.Source switch
    {
        ModelSource.Request => "request",
        ModelSource.KindModels => "kind.models",
        ModelSource.DefaultModels => "default.models",
        ModelSource.Names => "names",
        ModelSource.Builtin => "builtin",
        _ => throw new ArgumentOutOfRangeException(nameof(step)),
    };
}
