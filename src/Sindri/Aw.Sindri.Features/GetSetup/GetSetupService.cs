using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Core.Views;
using Microsoft.Extensions.Logging;

namespace Aw.Sindri.Features.GetSetup;

/// <summary>
/// Where "set up Sindri once" stands: the six steps in their fixed order (repo, profile, recipes, mcp, map, onyx), each decided by <see cref="SetupSteps"/> from the fact its own
/// source gave. A pure read that starts nothing. A source that can't be read makes only its own step "failed" with a plain sentence, so the answer is never an error.
/// </summary>
/// <param name="facts">The sources.</param>
/// <param name="status">How fresh the views are.</param>
/// <param name="logger">Where a source that threw is recorded.</param>
internal sealed partial class GetSetupService(ISetupFacts facts, IProjectorStatus status, ILogger<GetSetupService> logger) : IService<GetSetupRequest, GetSetupResponse>
{
    public async ValueTask<Result<GetSetupResponse>> Handle(GetSetupRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var steps = await new SetupDeriver(facts, (step, e) => LogSourceFailed(logger, step, e.GetType().Name)).Derive(cancellationToken).ConfigureAwait(false);
        return new GetSetupResponse(steps, status.Current.AsOf);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "setup: the {Step} source threw {ExceptionType}; that step is reported failed")]
    private static partial void LogSourceFailed(ILogger logger, string step, string exceptionType);
}
