using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Infrastructure.Sandbox.Launch;

/// <summary>Mints the agent GitHub App token a Step with 'sandbox.credentials.github: agent-app' gets (SND-LAUNCH-018 when it can't).</summary>
internal interface IAgentTokenSource
{
    /// <summary>Mints (or refuses to mint) a token for one launch, narrowed to one repository.</summary>
    /// <param name="launch">The launch.</param>
    /// <param name="repo">The repository the token is for.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The token, or SND-LAUNCH-018 with its reason.</returns>
    public ValueTask<Result<AgentToken>> Mint(LaunchId launch, RepoName repo, CancellationToken cancellationToken);
}
