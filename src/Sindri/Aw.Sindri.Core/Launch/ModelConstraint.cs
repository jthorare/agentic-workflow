namespace Aw.Sindri.Core.Launch;

/// <summary>The rule that took a model out of the candidates (model-pick spec §4.1), in the order the rules apply.</summary>
public enum ModelConstraint
{
    /// <summary>The model is below the step's floor.</summary>
    Floor,

    /// <summary>The model is above the profile's cap.</summary>
    Cap,

    /// <summary>The profile pins another model.</summary>
    Pin,

    /// <summary>The challenger may not run on the drafter's model.</summary>
    Drafter,

    /// <summary>The challenger shares the drafter's agent and the drafter's model was not recorded.</summary>
    DrafterUnknown,

    /// <summary>An escalation skips the model that just failed and every model at or below it.</summary>
    Failed,
}
