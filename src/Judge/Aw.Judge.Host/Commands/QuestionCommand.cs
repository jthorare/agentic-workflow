using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Judge.Core.Evaluation;
using Aw.Judge.Core.Providers;
using Aw.Judge.Core.Questions;
using Aw.Judge.Features.AskQuestion;
using Aw.Kernel.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aw.Judge.Host.Commands;

/// <summary>
/// 'judge &lt;question&gt; &lt; input.json', for any first word the table doesn't route: stdin is the question's input,
/// stdout one envelope. Decided exits 0 (<see cref="DecidedEnvelope"/>), escalated 2 (<see cref="EscalatedEnvelope"/>),
/// except ask-check (hook contract: done-gate.sh branches on it): 0 for ask or an escalation, 2 for continue. An
/// unknown question or any argument is usage (64).
/// </summary>
internal static class QuestionCommand
{
    internal static async Task<int> Run(
        string name, IReadOnlyList<string> args, IReadOnlyList<ProviderName>? allowed, IServiceProvider services, JsonSerializerOptions json)
    {
        if (!QuestionCatalog.Names.Contains(name))
        {
            return CommandOutput.Usage(CommandTable.Synopsis, $"unknown question '{name}'");
        }

        if (args.Count != 0)
        {
            return CommandOutput.Usage($"{name} < input.json", "a question takes no arguments");
        }

        var input = await CommandOutput.ReadText(CancellationToken.None).ConfigureAwait(false);
        var request = new AskQuestionRequest(name, input)
        {
            SessionId = services.GetRequiredService<CallingSession>().Id,
            AllowedProviders = allowed,
        };
        var service = services.GetRequiredService<IService<AskQuestionRequest, AskQuestionResponse>>();
        var result = await service.Handle(request, CancellationToken.None).ConfigureAwait(false);
        if (result.IsError)
        {
            return CommandOutput.Emit(1, "", result.Error.Message);
        }

        if (result.Value.ConfigWarning is { } warning)
        {
            JudgeLog.UsingDefaultConfig(services.GetRequiredService<ILoggerFactory>().CreateLogger(JudgeLog.Category), warning);
        }

        return CommandOutput.Emit(ExitCode(name, result.Value.Outcome), Envelope(result.Value.Outcome, json));
    }

    internal static int ExitCode(string question, EvaluationOutcome outcome) => (question, outcome) switch
    {
        (AskCheckQuestion.Name, DecidedOutcome { Decision: AskCheckQuestion.Continue }) => 2,
        (AskCheckQuestion.Name, _) => 0,
        (_, DecidedOutcome) => 0,
        _ => 2,
    };

    private static string Envelope(EvaluationOutcome outcome, JsonSerializerOptions json) => outcome switch
    {
        DecidedOutcome d => WithExtra(
            CommandOutput.Json(new DecidedEnvelope(d.Decision, d.Confidence, ProviderNames.Name(d.Model), d.ReasonCode, d.Id.Value), json),
            d.Extra,
            json),
        _ => CommandOutput.Json(EscalatedEnvelope.For(outcome.ReasonCode), json),
    };

    // The question's declared extra fields (chosenIndex, reasons) go beside the envelope's own.
    private static string WithExtra(string envelope, JsonObject? extra, JsonSerializerOptions json)
    {
        if (extra is null)
        {
            return envelope;
        }

        // why: the envelope is the JSON object just serialized from a record
        var merged = JsonNode.Parse(envelope)!.AsObject();
        foreach (var (key, value) in extra)
        {
            merged[key] = value?.DeepClone();
        }

        return merged.ToJsonString(json);
    }
}
