using System.Text.Json;
using Aw.Judge.Core.Ids;
using Aw.Judge.Features.ExplainDecision;
using Aw.Kernel;
using Aw.Kernel.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Judge.Host.Commands;

/// <summary>'judge why &lt;id&gt;': the decision row, or exit 1 'unknown decision: &lt;id&gt;'.</summary>
internal static class ExplainDecisionCommand
{
    internal const string Name = "why";

    internal static async Task<int> Run(string text, IServiceProvider services, JsonSerializerOptions json)
    {
        var unknown = $"unknown decision: {text}";
        if (!DecisionId.TryFrom(text, out var id))
        {
            return CommandOutput.Emit(1, "", unknown);   // a blank or over-long id names no decision
        }

        var service = services.GetRequiredService<IService<ExplainDecisionRequest, ExplainDecisionResponse>>();
        var result = await service.Handle(new ExplainDecisionRequest(id), CancellationToken.None).ConfigureAwait(false);
        if (result.IsError)
        {
            return CommandOutput.Emit(1, "", result.Error.Kind == ErrorKind.NotFound ? unknown : result.Error.Message);
        }

        return CommandOutput.Emit(0, CommandOutput.Json(DecisionOutput.From(result.Value.Decision), json));
    }
}
