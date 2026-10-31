using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Jobs;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>The three command chains the 'context-map-refresh' service starts jobs through: one dependency for all.</summary>
/// <param name="refresh">The RefreshContextMap chain.</param>
/// <param name="publish">The PublishContextMap chain.</param>
/// <param name="relink">The RelinkPinAliases chain.</param>
internal sealed class MapJobStarts(IService<RefreshContextMapRequest, JobStartedResponse> refresh, IService<PublishContextMapRequest, JobStartedResponse> publish, IService<RelinkPinAliasesRequest, JobStartedResponse> relink)
{
    /// <summary>Starts a refresh.</summary>
    internal ValueTask<Result<JobStartedResponse>> Refresh(CancellationToken cancellationToken) => refresh.Handle(new RefreshContextMapRequest(), cancellationToken);

    /// <summary>Starts a publish.</summary>
    internal ValueTask<Result<JobStartedResponse>> Publish(CancellationToken cancellationToken) => publish.Handle(new PublishContextMapRequest(), cancellationToken);

    /// <summary>Starts a relink of the records to the pins' aliases.</summary>
    internal ValueTask<Result<JobStartedResponse>> Relink(CancellationToken cancellationToken) => relink.Handle(new RelinkPinAliasesRequest(), cancellationToken);
}
