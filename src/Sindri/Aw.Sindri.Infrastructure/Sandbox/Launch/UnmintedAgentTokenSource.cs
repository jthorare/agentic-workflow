using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Infrastructure.Sandbox.Launch;

/// <summary>The stand-in until SB-7 ships the mint: there is no agent GitHub App key yet, so an 'agent-app' launch fails as a Step failure and never runs without its token.</summary>
internal sealed class UnmintedAgentTokenSource : IAgentTokenSource
{
    /// <inheritdoc/>
    public ValueTask<Result<Unit>> Mint(LaunchId launch, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<Unit>>(SandboxRefusal.For(SindriErrors.LaunchGitHubTokenFailed, SandboxReasons.NoKey, "the agent GitHub App isn't set up yet (SB-7)"));
}
