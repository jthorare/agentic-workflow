using Aw.Kernel;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Core.Launches;

/// <summary>What an agent has spent today (screens spec E-28): the read port OpenItem's budget check uses. The usage readers implement it (API P10a unit 11).</summary>
public interface ILaunchSpend
{
    /// <summary>The agent's spend today, in the local day.</summary>
    /// <param name="target">The agent.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The spend, or null when the agent has no usage reader.</returns>
    public ValueTask<Result<AgentSpend?>> Today(AgentId target, CancellationToken cancellationToken);

    /// <summary>
    /// The agent's spend as far as it is known, answered without waiting and without a token. <see cref="SpendPeek.Landed"/> is false while the day's first read is in
    /// flight (the call starts the shared read when none runs); landed with a null spend means the agent has no usage reader.
    /// </summary>
    /// <param name="target">The agent.</param>
    /// <returns>What is known now.</returns>
    public SpendPeek Peek(AgentId target);
}
