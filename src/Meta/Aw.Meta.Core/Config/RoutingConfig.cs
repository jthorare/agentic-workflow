using System.Text.Json.Serialization;

namespace Aw.Meta.Core.Config;

/// <summary>
/// Which model each provider offers, which models are equivalent across providers, and when a lane should move.
/// Every field is optional; with no equivalents the meta never suggests a switch.
/// </summary>
public sealed record RoutingConfig
{
    /// <summary>Creates a routing config.</summary>
    /// <param name="models">The models each catalog agent offers, by agent id.</param>
    /// <param name="equivalents">Model to the model of equal performance on another provider.</param>
    /// <param name="lanes">Where each lane is assigned to run, by lane name.</param>
    /// <param name="defaultModel">The model a lane runs when its entry names none.</param>
    /// <param name="switchWhenLimited">Whether a lane stopped at a usage limit should move.</param>
    /// <param name="minRemainingPct">A lane whose provider shows less than this share left should move; 0 turns the check off.</param>
    [JsonConstructor]
    public RoutingConfig(
        IReadOnlyDictionary<string, IReadOnlyList<string>>? models = null,
        IReadOnlyDictionary<string, string>? equivalents = null,
        IReadOnlyDictionary<string, LaneRoute>? lanes = null,
        string? defaultModel = null,
        bool switchWhenLimited = true,
        int minRemainingPct = 10)
    {
        Models = models ?? new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        Equivalents = equivalents ?? new Dictionary<string, string>(StringComparer.Ordinal);
        Lanes = lanes ?? new Dictionary<string, LaneRoute>(StringComparer.Ordinal);
        DefaultModel = defaultModel;
        SwitchWhenLimited = switchWhenLimited;
        MinRemainingPct = minRemainingPct;
    }

    /// <summary>The models each catalog agent offers, by agent id.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Models { get; init; }

    /// <summary>Model to the model of equal performance on another provider.</summary>
    public IReadOnlyDictionary<string, string> Equivalents { get; init; }

    /// <summary>Where each lane is assigned to run, by lane name.</summary>
    public IReadOnlyDictionary<string, LaneRoute> Lanes { get; init; }

    /// <summary>The model a lane runs when its entry names none.</summary>
    public string? DefaultModel { get; init; }

    /// <summary>Whether a lane stopped at a usage limit should move.</summary>
    public bool SwitchWhenLimited { get; init; }

    /// <summary>A lane whose provider shows less than this share left should move; 0 turns the check off.</summary>
    public int MinRemainingPct { get; init; }

    /// <summary>The agent that lists a model.</summary>
    /// <param name="model">The model name.</param>
    /// <returns>The agent id, or null when no agent lists it.</returns>
    public string? AgentOf(string model) =>
        Models.FirstOrDefault(m => m.Value.Contains(model, StringComparer.Ordinal)).Key;

    /// <summary>The model of equal performance on another provider: the listed pair, else the one model that maps to this one.</summary>
    /// <param name="model">The model name.</param>
    /// <returns>The other model, or null when there is none (or several map here).</returns>
    public string? EquivalentOf(string model)
    {
        if (Equivalents.TryGetValue(model, out var direct))
        {
            return direct;
        }

        var back = Equivalents.Where(e => string.Equals(e.Value, model, StringComparison.Ordinal)).Select(static e => e.Key).Take(2).ToList();
        return back.Count == 1 ? back[0] : null;
    }
}
