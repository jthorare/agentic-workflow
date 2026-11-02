using System.Text.Json;
using Aw.Judge.Core.Errors;
using Aw.Judge.Core.Ids;
using Aw.Judge.Features.UndoDecision;
using Aw.Kernel;
using Aw.Kernel.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Judge.Host.Commands;

/// <summary>'judge undo &lt;id&gt;': the decision row after undoing it; unknown → exit 1 'unknown decision: &lt;id&gt;'.</summary>
internal static class UndoDecisionCommand
{
    internal const string Name = "undo";

    internal static async Task<int> Run(string text, IServiceProvider services, JsonSerializerOptions json)
    {
        var unknown = $"unknown decision: {text}";
        if (!DecisionId.TryFrom(text, out var id))
        {
            return CommandOutput.Emit(1, "", unknown);
        }

        var service = services.GetRequiredService<IService<UndoDecisionRequest, UndoDecisionResponse>>();
        var result = await service.Handle(new UndoDecisionRequest(id), CancellationToken.None).ConfigureAwait(false);
        if (result.IsError)
        {
            return result.Error.Code == JudgeErrors.UnreadableDecision.Code
                ? CommandOutput.Emit(2, "", result.Error.Message)
                : CommandOutput.Emit(1, "", result.Error.Kind == ErrorKind.NotFound ? unknown : result.Error.Message);
        }

        return CommandOutput.Emit(0, CommandOutput.Json(DecisionOutput.From(result.Value.Decision), json));
    }
}
