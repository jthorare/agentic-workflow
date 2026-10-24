using Aw.Judge.Core.Errors;
using Aw.Judge.Core.Ports;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.SetPromptSort;

internal sealed class SetPromptSortService(IJudgeConfigStore configs) : IService<SetPromptSortRequest, SetPromptSortResponse>
{
    public async ValueTask<Result<SetPromptSortResponse>> Handle(SetPromptSortRequest request, CancellationToken cancellationToken)
    {
        var loaded = await configs.Load(cancellationToken).ConfigureAwait(false);
        if (loaded.IsError)
        {
            return loaded.Error.Code == JudgeErrors.ConfigMalformed.Code
                ? JudgeErrors.ConfigMalformed.With($"{loaded.Error.Message}; fix or delete it, then set again")
                : loaded.Error;
        }

        var next = request.Scaffold is { } scaffold
            ? loaded.Value.SetScaffold(scaffold.Scaffold, scaffold.On)
            : Results.Success(loaded.Value.SetPromptSortEnabled(request.Enabled ?? true));
        if (next.IsError)
        {
            return next.Error;
        }

        var saved = await configs.Save(next.Value, cancellationToken).ConfigureAwait(false);
        return saved.IsError ? saved.Error : new SetPromptSortResponse(next.Value);
    }
}
