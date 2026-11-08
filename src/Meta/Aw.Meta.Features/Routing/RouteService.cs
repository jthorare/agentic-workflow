using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core.Ports;
using Aw.Meta.Core.Routing;
using Aw.Meta.Core.Usage;

namespace Aw.Meta.Features.Routing;

/// <summary>Suggests, and on request writes, which lanes move to another provider.</summary>
/// <param name="config">The team config.</param>
/// <param name="reader">Looks at the lanes and the meta.</param>
/// <param name="standings">Adds provider, model and usage windows.</param>
internal sealed class RouteService(IMetaConfig config, UsageReader reader, LaneStandingReader standings) : IService<RouteRequest, RouteResponse>
{
    public ValueTask<Result<RouteResponse>> Handle(RouteRequest request, CancellationToken cancellationToken) => throw new NotSupportedException($"{request}{config.GetType()}{reader.GetType()}{standings.GetType()}");
}
