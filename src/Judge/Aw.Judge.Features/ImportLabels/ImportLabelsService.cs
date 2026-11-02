using System.Text.Json;
using Aw.Judge.Core.Evaluation;
using Aw.Judge.Core.Ids;
using Aw.Judge.Core.Labels;
using Aw.Judge.Core.Ports;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.ImportLabels;

internal sealed class ImportLabelsService(IEvalStore items, TimeProvider clock, JsonSerializerOptions json) : IService<ImportLabelsRequest, ImportLabelsResponse>
{
    public async ValueTask<Result<ImportLabelsResponse>> Handle(ImportLabelsRequest request, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var decided = await items.DecidedSince(request.Question, now - request.Since, cancellationToken).ConfigureAwait(false);
        if (decided.IsError)
        {
            return decided.Error;
        }

        var imported = 0;
        var unparsable = 0;
        foreach (var decision in decided.Value)
        {
            if (!InputJson.TryParse(decision.InputJson, out _))
            {
                unparsable++;
                continue;
            }

            if (!LabelQuestions.Fits(decision.Question, decision.InputJson, json))
            {
                continue;
            }

            var added = await items.AddItem(Item(decision, now), cancellationToken).ConfigureAwait(false);
            if (added.IsError)
            {
                return added.Error;
            }

            imported += added.Value ? 1 : 0;
        }

        return new ImportLabelsResponse(imported, unparsable);
    }

    private static EvalItem Item(DecidedInput decision, DateTimeOffset now) =>
        new(EvalItemId.From("item-" + decision.Id.Value), decision.Question, decision.InputJson, "decision:" + decision.Id.Value, decision.Answer, now);
}
