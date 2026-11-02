using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Control;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// The control-plane probe the in-process test CLI uses: the product's probe, asked a second time when the first answers SND-API-002 (its 2 s budget is tight on a loaded CI runner, #1328).
/// The product's budget is unchanged; only these tests wait for a slow runner.
/// </summary>
/// <param name="inner">The product's probe.</param>
internal sealed class RetryOncePlaneProbe(IControlPlaneProbe inner) : IControlPlaneProbe
{
    public async ValueTask<Result<(ControlEndpoint Endpoint, Ping Ping)>> Check(CancellationToken cancellationToken)
    {
        var first = await inner.Check(cancellationToken);
        return first.IsError && string.Equals(first.Error.Code, SindriErrors.ApiTimeout.Code, StringComparison.Ordinal)
            ? await inner.Check(cancellationToken)
            : first;
    }
}
