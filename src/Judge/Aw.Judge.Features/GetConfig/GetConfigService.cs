using Aw.Judge.Core.Config;
using Aw.Judge.Core.Ports;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.GetConfig;

internal sealed class GetConfigService(IJudgeConfigStore configs) : IService<GetConfigRequest, GetConfigResponse>
{
    public async ValueTask<Result<GetConfigResponse>> Handle(GetConfigRequest request, CancellationToken cancellationToken)
    {
        var reading = ConfigReading.From(await configs.Load(cancellationToken).ConfigureAwait(false));
        return reading.IsError ? reading.Error : new GetConfigResponse(reading.Value.Config, reading.Value.Warning);
    }
}
