using Aw.Sindri.Core.Ids;
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
    private LaunchMode _mode = LaunchMode.Open;
    private SessionId? _resume;
    private InstanceAddress? _instance;
    private RunToken? _run;
    private EvaluatorLaunch? _evaluator;

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

    /// <summary>Says which workflow instance and Step run the launch belongs to.</summary>
    /// <param name="instance">The instance.</param>
    /// <param name="run">The Step run's token.</param>
    /// <returns>This builder.</returns>
    public LaunchRequestBuilder OnInstance(InstanceAddress instance, RunToken run)
    {
        _instance = instance;
        _run = run;
        return this;
    }

    /// <summary>Makes it the holdout evaluator's launch (LN-LCH2).</summary>
    /// <param name="value">What the evaluator runs against and may touch.</param>
    /// <returns>This builder.</returns>
    public LaunchRequestBuilder AsEvaluator(EvaluatorLaunch value)
    {
        _evaluator = value;
        return this;
    }

    /// <summary>Makes it a Resume of this session.</summary>
    /// <param name="value">The session id to resume.</param>
    /// <returns>This builder.</returns>
    public LaunchRequestBuilder Resuming(SessionId value)
    {
        _mode = LaunchMode.Resume;
        _resume = value;
        return this;
    }

    /// <summary>Builds the request.</summary>
    /// <returns>The request.</returns>
    public LaunchRequest Build() =>
        new(_launch, A.FirstItemId, _agent, _mode, _attended, "/work/repo", _instance, Worktree: null, _step, _run, _resume, _evaluator);
}
