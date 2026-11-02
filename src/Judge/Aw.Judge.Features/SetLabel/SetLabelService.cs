using Aw.Judge.Core.Errors;
using Aw.Judge.Core.Labels;
using Aw.Judge.Core.Ports;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.SetLabel;

internal sealed class SetLabelService(IEvalStore items, TimeProvider clock) : IService<SetLabelRequest, SetLabelResponse>
{
    public async ValueTask<Result<SetLabelResponse>> Handle(SetLabelRequest request, CancellationToken cancellationToken)
    {
        var found = await items.FindItem(request.Item, cancellationToken).ConfigureAwait(false);
        if (found.IsError)
        {
            return found.Error;
        }

        var allowed = (LabelQuestions.Outputs(found.Value.Question) ?? []).Append(LabelQuestions.Skip).ToList();
        if (!allowed.Contains(request.Label, StringComparer.Ordinal))
        {
            return JudgeErrors.Invalid.With($"label must be one of: {string.Join(", ", allowed)}");
        }

        var recorded = await items.RecordLabel(request.Item, LabelSource.Override, request.Label, clock.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        return recorded.IsError ? recorded.Error : new SetLabelResponse(request.Item, request.Label);
    }
}
