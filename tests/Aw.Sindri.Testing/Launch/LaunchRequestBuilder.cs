using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Testing.Launch;

/// <summary>Builds a LaunchRequest with deterministic defaults: the first launch of the first item, claude-code, an attended Open.</summary>
public sealed class LaunchRequestBuilder
{
    private LaunchId _launch = A.FirstLaunchId;
    private AgentId _agent = AgentId.From("claude-code");
    private bool _attended = true;
    private StepArgs? _step;

    /// <summary>Sets the launch id.</summary>
    /// <param name="value">The id.</param>
    /// <returns>This builder.</returns>
    public LaunchRequestBuilder WithLaunch(LaunchId value)
    {
        _launch = value;
        return this;
    }

    /// <summary>Sets the agent.</summary>
    /// <param name="value">The catalog id.</param>
    /// <returns>This builder.</returns>
    public LaunchRequestBuilder WithAgent(AgentId value)
    {
        _agent = value;
        return this;
    }

    /// <summary>Makes it an unattended Step launch with these arguments.</summary>
    /// <param name="value">The Step's arguments.</param>
    /// <returns>This builder.</returns>
    public LaunchRequestBuilder Unattended(StepArgs value)
    {
        _attended = false;
        _step = value;
        return this;
    }

    /// <summary>Builds the request.</summary>
    /// <returns>The request.</returns>
    public LaunchRequest Build() =>
        new(_launch, A.FirstItemId, _agent, LaunchMode.Open, _attended, "/work/repo", Instance: null, Worktree: null, _step, Run: null);
}
