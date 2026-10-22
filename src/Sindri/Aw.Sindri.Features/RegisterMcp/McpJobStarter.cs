using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Features.RegisterMcp;

/// <summary>Starts the job through the RegisterMcp command, which only starts it.</summary>
/// <param name="start">The command.</param>
internal sealed class McpJobStarter(IService<RegisterMcpJobRequest, JobStartedResponse> start) : IMcpJobStarter
{
    public async ValueTask<Result<Unit>> Start(RepoName repo, CancellationToken cancellationToken)
    {
        var started = await start.Handle(new RegisterMcpJobRequest(repo.Value), cancellationToken).ConfigureAwait(false);
        return started.IsError ? started.Error : Unit.Value;
    }
}
