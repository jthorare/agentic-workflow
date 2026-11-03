using Aw.Judge.Core.Errors;
using Aw.Judge.Core.Ports;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.SetJevImages;

internal sealed class SetJevImagesService(IJudgeConfigStore configs) : IService<SetJevImagesRequest, SetJevImagesResponse>
{
    public async ValueTask<Result<SetJevImagesResponse>> Handle(SetJevImagesRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var loaded = await configs.Load(cancellationToken).ConfigureAwait(false);
        if (loaded.IsError)
        {
            return loaded.Error.Code == JudgeErrors.ConfigMalformed.Code
                ? JudgeErrors.ConfigMalformed.With($"{loaded.Error.Message}; fix or delete it, then set again")
                : loaded.Error;
        }

        var next = loaded.Value.SetJevImages(request.On);
        var saved = await configs.Save(next, cancellationToken).ConfigureAwait(false);
        return saved.IsError ? saved.Error : new SetJevImagesResponse(next);
    }
}
