using System.Text.RegularExpressions;
using Aw.Kernel;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Core.Profile;

/// <summary>The checks on 'workflow.agent.models', 'pin', 'cap' and 'pick' (model-pick spec §8).</summary>
public static partial class ModelKeyRules
{
    private const string IdHint = "letters, digits, '.', '_' or '-', 1 to 64 characters";

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]{0,63}\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex IdShape { get; }

    /// <summary>The checks the profile alone settles: the id patterns, a non-empty model list and 'pick' being on or off.</summary>
    /// <param name="agent">The 'workflow.agent' block, or null.</param>
    /// <returns>SND-PROFILE-002 naming the key path, or null.</returns>
    public static Error? Shape(WorkflowAgentSettings? agent) =>
        agent is null ? null : PickShape(agent.Pick) ?? ModelsShape(agent.Models) ?? PinShape(agent.Pin) ?? CapShape(agent.Cap);

    /// <summary>The checks that need the box: agent ids against the catalog and pin states against the recipes, plus 'default' being in 'list'.</summary>
    /// <param name="agent">The 'workflow.agent' block, or null.</param>
    /// <param name="agentIds">The catalog's agent ids, or null to skip the agent-id check.</param>
    /// <param name="states">The states of the embedded and approved recipes, or null to skip the state check.</param>
    /// <returns>SND-MODEL-001 with the nearest id, or null.</returns>
    public static Error? Check(WorkflowAgentSettings? agent, IReadOnlyCollection<AgentId>? agentIds, IReadOnlyList<RecipeStateRef>? states) =>
        agent is null ? null : ModelsCheck(agent.Models, agentIds) ?? PinCheck(agent.Pin, agentIds, states) ?? CapCheck(agent.Cap, agentIds);

    /// <summary>What each pin and cap matches, one line each: 'pin claude-code/questions = haiku: matches workflow.scoping state questions, workflow.design state questions'.</summary>
    /// <param name="agent">The 'workflow.agent' block, or null.</param>
    /// <param name="states">The states of the embedded and approved recipes.</param>
    /// <returns>The lines, pins first then caps, each in key order.</returns>
    public static IReadOnlyList<string> Matches(WorkflowAgentSettings? agent, IReadOnlyList<RecipeStateRef> states)
    {
        ArgumentNullException.ThrowIfNull(states);
        return
        [
            .. Sorted(agent?.Pin).Select(p => $"pin {p.Key} = {p.Value}: matches {PinMatch(p.Key, states)}"),
            .. Sorted(agent?.Cap).Select(static p => $"cap {p.Key} = {p.Value}: matches every pick for {p.Key}"),
        ];
    }

    /// <summary>The model a pin fixes for a step: the 'agent/stateId' entry when there is one, else the plain 'agent' entry.</summary>
    /// <param name="agent">The 'workflow.agent' block, or null.</param>
    /// <param name="agentId">The agent the step runs on.</param>
    /// <param name="state">The state the step belongs to (a state name).</param>
    /// <returns>The pinned model id, or null when nothing pins this step.</returns>
    public static string? PinFor(WorkflowAgentSettings? agent, AgentId agentId, string state)
    {
        if (agent?.Pin is not { } pin)
        {
            return null;
        }

        return pin.TryGetValue($"{agentId.Value}/{state}", out var byState) ? byState : pin.TryGetValue(agentId.Value, out var byAgent) ? byAgent : null;
    }

    private static string PinMatch(string key, IReadOnlyList<RecipeStateRef> states)
    {
        var (id, state) = Split(key);
        if (state is null)
        {
            return $"every step of {id}";
        }

        var matched = states.Where(s => string.Equals(s.State, state, StringComparison.Ordinal)).OrderBy(static s => s.Recipe, StringComparer.Ordinal).Select(s => $"{s.Recipe} state {s.State}");
        return string.Join(", ", matched) is { Length: > 0 } text ? text : "no state";
    }

    private static Error? PickShape(string? pick) =>
        pick is null || string.Equals(pick, WorkflowAgentSettings.On, StringComparison.Ordinal) || string.Equals(pick, WorkflowAgentSettings.Off, StringComparison.Ordinal) ? null : Invalid("workflow.agent.pick", $"'{pick}' is not on or off");

    private static Error? ModelsShape(IReadOnlyDictionary<string, AgentModels>? models) =>
        Sorted(models).Select(static p => ModelsEntryShape(p.Key, p.Value)).FirstOrDefault(static e => e is not null);

    private static Error? ModelsEntryShape(string key, AgentModels? models)
    {
        var path = $"workflow.agent.models.{key}";
        if (!AgentId.TryParse(key, provider: null, out _))
        {
            return Invalid(path, "the key is not an agent id: a lowercase letter, then lowercase letters, digits or '-', 2 to 32 characters");
        }

        if (models?.List is not { Count: > 0 })
        {
            return Invalid(path + ".list", "must name at least one model id");
        }

        return BadModel(path + ".list", models.List.FirstOrDefault(static m => !IsModelId(m))) ?? (IsModelId(models.Default) ? null : BadModel(path + ".default", models.Default));
    }

    private static Error? PinShape(IReadOnlyDictionary<string, string>? pin) =>
        Sorted(pin).Select(static p => PinEntryShape(p.Key, p.Value)).FirstOrDefault(static e => e is not null);

    private static Error? PinEntryShape(string key, string model)
    {
        var (id, state) = Split(key);
        var path = $"workflow.agent.pin.{key}";
        if (!AgentId.TryParse(id, provider: null, out _) || (state is not null && !IsModelId(state)))
        {
            return Invalid(path, "the key must be an agent id, or 'agent/stateId'");
        }

        return IsModelId(model) ? null : BadModel(path, model);
    }

    private static Error? CapShape(IReadOnlyDictionary<string, string>? cap) =>
        Sorted(cap).Select(static p => CapEntryShape(p.Key, p.Value)).FirstOrDefault(static e => e is not null);

    private static Error? CapEntryShape(string key, string model)
    {
        var path = $"workflow.agent.cap.{key}";
        if (!AgentId.TryParse(key, provider: null, out _))
        {
            return Invalid(path, "the key must be an agent id");
        }

        return IsModelId(model) ? null : BadModel(path, model);
    }

    private static Error? BadModel(string path, string? model) =>
        model is null ? null : Invalid(path, $"'{model}' is not a model id: {IdHint}");

    private static Error? ModelsCheck(IReadOnlyDictionary<string, AgentModels>? models, IReadOnlyCollection<AgentId>? agentIds) =>
        Sorted(models).Select(p => AgentProblem($"workflow.agent.models.{p.Key}", p.Key, agentIds) ?? DefaultProblem(p.Key, p.Value)).FirstOrDefault(static e => e is not null);

    private static Error? DefaultProblem(string key, AgentModels? models) =>
        models is null || models.List.Contains(models.Default, StringComparer.Ordinal)
            ? null
            : Model($"workflow.agent.models.{key}: default `{models.Default}` is not in list ({string.Join(", ", models.List)})");

    private static Error? PinCheck(IReadOnlyDictionary<string, string>? pin, IReadOnlyCollection<AgentId>? agentIds, IReadOnlyList<RecipeStateRef>? states) =>
        Sorted(pin).Select(p => PinKeyProblem(p.Key, agentIds, states)).FirstOrDefault(static e => e is not null);

    private static Error? PinKeyProblem(string key, IReadOnlyCollection<AgentId>? agentIds, IReadOnlyList<RecipeStateRef>? states)
    {
        var (id, state) = Split(key);
        var path = $"workflow.agent.pin.{key}";
        if (AgentProblem(path, id, agentIds) is { } missing)
        {
            return missing;
        }

        return state is null || states is null || states.Any(s => string.Equals(s.State, state, StringComparison.Ordinal))
            ? null
            : Model($"{path}: no state `{state}` in any recipe{Suggest(state, states.Select(static s => s.State))}");
    }

    private static Error? CapCheck(IReadOnlyDictionary<string, string>? cap, IReadOnlyCollection<AgentId>? agentIds) =>
        Sorted(cap).Select(p => AgentProblem($"workflow.agent.cap.{p.Key}", p.Key, agentIds)).FirstOrDefault(static e => e is not null);

    private static Error? AgentProblem(string path, string id, IReadOnlyCollection<AgentId>? agentIds) =>
        agentIds is null || agentIds.Any(a => string.Equals(a.Value, id, StringComparison.Ordinal))
            ? null
            : Model($"{path}: no agent `{id}` in the launcher catalog{Suggest(id, agentIds.Select(static a => a.Value))}");

    private static string Suggest(string wanted, IEnumerable<string> known) =>
        Nearest.Of(wanted, known) is { } near ? $"; did you mean `{near}`?" : string.Empty;

    private static bool IsModelId(string? value) => value is not null && IdShape.IsMatch(value);

    private static (string Id, string? State) Split(string key)
    {
        var slash = key.IndexOf('/', StringComparison.Ordinal);
        return slash < 0 ? (key, null) : (key[..slash], key[(slash + 1)..]);
    }

    private static IEnumerable<KeyValuePair<string, T>> Sorted<T>(IReadOnlyDictionary<string, T>? map) =>
        (map ?? new Dictionary<string, T>(StringComparer.Ordinal)).OrderBy(static p => p.Key, StringComparer.Ordinal);

    private static Error Invalid(string keyPath, string problem) => SindriErrors.ProfileInvalid.With($"{keyPath}: {problem}");

    private static Error Model(string message) => SindriErrors.ModelKeyInvalid.With(message);
}
