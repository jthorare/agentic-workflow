using Aw.Judge.Core.Errors;
using Aw.Judge.Core.Ports;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.SetConfig;

internal sealed class SetConfigService(IJudgeConfigStore configs) : IService<SetConfigRequest, SetConfigResponse>
{
    public async ValueTask<Result<SetConfigResponse>> Handle(SetConfigRequest request, CancellationToken cancellationToken)
    {
        var loaded = await configs.Load(cancellationToken).ConfigureAwait(false);
        if (loaded.IsError)
        {
            return loaded.Error.Code == JudgeErrors.ConfigMalformed.Code
                ? JudgeErrors.ConfigMalformed.With($"{loaded.Error.Message}; fix or delete it, then set again")
                : loaded.Error;
        }

        var next = request.Enabled is { } enabled
            ? loaded.Value.SetEnabled(request.Question, enabled)
            : loaded.Value.SetThreshold(request.Question, request.Threshold ?? double.NaN);
        if (next.IsError)
        {
            return next.Error;
        }

        var saved = await configs.Save(next.Value, cancellationToken).ConfigureAwait(false);
        return saved.IsError ? saved.Error : new SetConfigResponse(next.Value);
    }
}
