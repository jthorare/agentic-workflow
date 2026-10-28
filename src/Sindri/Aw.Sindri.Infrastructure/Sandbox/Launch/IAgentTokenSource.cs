using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Infrastructure.Sandbox.Launch;

/// <summary>Mints the agent GitHub App token a Step with 'sandbox.credentials.github: agent-app' gets (SND-LAUNCH-018 when it can't). SB-7 replaces <see cref="UnmintedAgentTokenSource"/> with the real mint.</summary>
internal interface IAgentTokenSource
{
    /// <summary>Mints (or refuses to mint) the token for one launch.</summary>
    /// <param name="launch">The launch.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>Success, or SND-LAUNCH-018 with its reason.</returns>
    public ValueTask<Result<Unit>> Mint(LaunchId launch, CancellationToken cancellationToken);
}
