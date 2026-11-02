using Aw.Sindri.Core.Workflow.Engine.Triggers;

namespace Aw.Sindri.Core.Events;

/// <summary>
/// The one plain sentence for why a step got its model (model-pick spec §6.1, "Why, in plain words"). Pure: 'sindri launch picks'
/// prints it, the route serves it and the app draws it, so the words are in one place.
/// </summary>
public static class PickWhy
{
    /// <summary>The sentence while only 'model.picking' exists for a launch.</summary>
    public const string Asking = "Asking Jev.";

    /// <summary>The reason a 'default' pick carries when model choice is off (workflow.agent.pick: off).</summary>
    public const string PickOff = "pick-off";

    private const string Cheapest = "so Sindri picked the cheapest model allowed.";

    /// <summary>The sentence for a recorded pick.</summary>
    /// <param name="pick">The pick.</param>
    /// <returns>The sentence: the escalation's when the pick is a retry, else the one for its by and reason, with the unusual note added when the pick says so.</returns>
    public static string Sentence(ModelPicked pick)
    {
        ArgumentNullException.ThrowIfNull(pick);
        var sentence = pick.EscalatedFrom is { } failed ? Retried(pick, failed) : Chosen(pick);
        return pick.Unusual ? $"{sentence} Unusual: {pick.Model ?? "the model"} failed most of its recent runs on this step." : sentence;
    }

    private static string Retried(ModelPicked pick, string failed) =>
        $"Retried on {pick.Model ?? "another model"} after {failed} {(string.Equals(pick.EscalatedFromKind, AgentFailureKinds.Refused, StringComparison.Ordinal) ? "was refused" : "failed")}.";

    private static string Chosen(ModelPicked pick) => pick.By switch
    {
        ModelPickedBy.Jev => "Picked by Jev: the cheapest model it expects to pass.",
        ModelPickedBy.Constraint when string.Equals(pick.Reason, "step", StringComparison.Ordinal) => "The recipe names this step's model.",
        ModelPickedBy.Constraint => "Only one model was allowed here" + Rule(pick.Reason) + ".",
        ModelPickedBy.Rules => Fallback(pick.Reason),
        ModelPickedBy.Default => string.Equals(pick.Reason, PickOff, StringComparison.Ordinal)
            ? "Model choice is off, so this ran on the agent's default."
            : "This agent reports no models, so it ran on its own default.",
        _ => "Picked by " + pick.By + ".",
    };

    // The rule that left one model, when the pick names it.
    private static string Rule(string? reason) => reason switch
    {
        "cap" => " (your cap)",
        "pin" => " (your pin)",
        "floor" => " (the step's floor)",
        "challenger" => " (the challenger rule)",
        "only-model" => " (the agent offers no other)",
        _ => string.Empty,
    };

    private static string Fallback(string? reason) => reason switch
    {
        "no-api-key" => "Jev isn't set up on this machine, " + Cheapest,
        "timeout" => "Jev didn't answer in 20 seconds, " + Cheapest,
        "below-threshold" or "unknown-key" => "Jev wasn't sure, " + Cheapest,
        "jev-input-rejected" => "Jev couldn't take this much input, " + Cheapest,
        null or "rules" => "Jev wasn't asked, " + Cheapest,
        _ => "Jev couldn't be reached, " + Cheapest,
    };
}
