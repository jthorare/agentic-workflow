using System.Text.Json;
using Aw.Judge.Core.Errors;
using Aw.Judge.Core.Ids;
using Aw.Judge.Features.ApproveDecision;
using Aw.Kernel;
using Aw.Kernel.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Judge.Host.Commands;

/// <summary>'judge approve &lt;id&gt;': the decision row with approved_at set (the first approval's time); unknown → exit 1 'unknown decision: &lt;id&gt;'.</summary>
internal static class ApproveDecisionCommand
{
    internal const string Name = "approve";

    internal static async Task<int> Run(string text, IServiceProvider services, JsonSerializerOptions json)
    {
        var unknown = $"unknown decision: {text}";
        if (!DecisionId.TryFrom(text, out var id))
        {
            return CommandOutput.Emit(1, "", unknown);   // a blank or over-long id names no decision
        }

        var service = services.GetRequiredService<IService<ApproveDecisionRequest, ApproveDecisionResponse>>();
        var result = await service.Handle(new ApproveDecisionRequest(id), CancellationToken.None).ConfigureAwait(false);
        if (result.IsError)
        {
            return result.Error.Code == JudgeErrors.UnreadableDecision.Code
                ? CommandOutput.Emit(2, "", result.Error.Message)
                : CommandOutput.Emit(1, "", result.Error.Kind == ErrorKind.NotFound ? unknown : result.Error.Message);
        }

        return CommandOutput.Emit(0, CommandOutput.Json(DecisionOutput.From(result.Value.Decision), json));
    }
}
