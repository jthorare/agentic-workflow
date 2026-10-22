using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Blocks.Step;

/// <summary>
/// Builds the descriptor-only Steps of the decision-tree blocks (DT spec §4, §5, §8): version 1, desk-capable, no
/// parameters, with the runtime and the human-only flag the plan names. AgentStep("step.x", caps).Reads(…).Reports(…).Writes(…).
/// </summary>
internal sealed class StepSpec
{
    private readonly BlockId _id;
    private readonly BlockRuntime _runtime;
    private readonly bool _humanOnly;
    private readonly Capability _capabilities;
    private FactKey[] _inputs = [];
    private OutcomeName[] _outcomes = [];
    private ParameterSpec[] _parameters = [];
    private TimeSpan? _maxDuration;

    private StepSpec(string id, BlockRuntime runtime, bool humanOnly, Capability capabilities)
    {
        _id = BlockId.From(id);
        _runtime = runtime;
        _humanOnly = humanOnly;
        _capabilities = capabilities;
    }

    /// <summary>A Step an agent session runs.</summary>
    /// <param name="id">The 'step.…' id.</param>
    /// <param name="capabilities">What it may reach.</param>
    /// <returns>The builder.</returns>
    public static StepSpec AgentStep(string id, Capability capabilities) => new(id, BlockRuntime.Agent, humanOnly: false, capabilities);

    /// <summary>A Step the engine's block runtime runs (a human-only Step while it is on the desk).</summary>
    /// <param name="id">The 'step.…' id.</param>
    /// <param name="capabilities">What it may reach.</param>
    /// <returns>The builder.</returns>
    public static StepSpec BlockStep(string id, Capability capabilities) => new(id, BlockRuntime.Block, humanOnly: false, capabilities);

    /// <summary>A Step the desk runs.</summary>
    /// <param name="id">The 'step.…' id.</param>
    /// <param name="capabilities">What it may reach.</param>
    /// <returns>The builder.</returns>
    public static StepSpec DeskStep(string id, Capability capabilities) => new(id, BlockRuntime.Desk, humanOnly: false, capabilities);

    /// <summary>A Step only a human may report (DT spec §8.5).</summary>
    /// <param name="id">The 'step.…' id.</param>
    /// <param name="capabilities">What it may reach.</param>
    /// <returns>The builder.</returns>
    public static StepSpec HumanDeskStep(string id, Capability capabilities) => new(id, BlockRuntime.Desk, humanOnly: true, capabilities);

    /// <summary>A fact required on the named outcomes.</summary>
    /// <param name="fact">The fact key.</param>
    /// <param name="requiredOn">The outcomes it is required on.</param>
    /// <returns>The output.</returns>
    public static OutputSpec On(string fact, params string[] requiredOn) => new(FactKey.From(fact), [.. requiredOn.Select(OutcomeName.From)]);

    /// <summary>A fact required on every outcome the Step reports.</summary>
    /// <param name="fact">The fact key.</param>
    /// <returns>The output.</returns>
    public static OutputSpec Every(string fact) => new(FactKey.From(fact), RequiredOn: [], Every: true);

    /// <summary>A text 'with:' parameter (a literal, a fact or a profile key).</summary>
    /// <param name="name">The parameter's name.</param>
    /// <param name="required">Whether a recipe must give it.</param>
    /// <returns>The parameter.</returns>
    public static ParameterSpec Text(string name, bool required = false) => new(name, ParameterType.Text, required, IsCapabilityField: false);

    /// <summary>The facts the Step reads.</summary>
    /// <param name="facts">The fact keys.</param>
    /// <returns>The builder.</returns>
    public StepSpec Reads(params string[] facts)
    {
        _inputs = [.. facts.Select(FactKey.From)];
        return this;
    }

    /// <summary>The 'with:' parameters the Step takes.</summary>
    /// <param name="parameters">The parameters.</param>
    /// <returns>The builder.</returns>
    public StepSpec Takes(params ParameterSpec[] parameters)
    {
        _parameters = parameters;
        return this;
    }

    /// <summary>The outcomes the Step reports.</summary>
    /// <param name="outcomes">The outcome names.</param>
    /// <returns>The builder.</returns>
    public StepSpec Reports(params string[] outcomes)
    {
        _outcomes = [.. outcomes.Select(OutcomeName.From)];
        return this;
    }

    /// <summary>
    /// A long block Step (line spec LN-WF7): the engine ends it as step.failed 'timeout' only after <paramref name="maxDuration"/>, not after the two-minute default. The activity heartbeats, so a
    /// cancel or a takeover still reaches it. The state's own timeout must be longer (RCP-TIME-003).
    /// </summary>
    /// <param name="maxDuration">The longest the Step may run; the profile's 'workflow.steps.longMax' (default 2h) is the cap.</param>
    /// <returns>The builder.</returns>
    public StepSpec Long(TimeSpan maxDuration)
    {
        _maxDuration = maxDuration;
        return this;
    }

    /// <summary>The facts the Step writes, and the descriptor.</summary>
    /// <param name="outputs">The outputs.</param>
    /// <returns>The descriptor.</returns>
    public BlockDescriptor Writes(params OutputSpec[] outputs) => new(
        _id, BlockKind.Step, Version: 1, _inputs, outputs, _outcomes, _capabilities, Parameters: _parameters, Rung: null, DeskCapable: true,
        HumanOnly: _humanOnly ? true : null, Runtime: _runtime, MaxDuration: _maxDuration);
}
