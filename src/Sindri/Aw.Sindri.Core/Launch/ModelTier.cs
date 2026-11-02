namespace Aw.Sindri.Core.Launch;

/// <summary>
/// The bands an agent's models fall into (model-pick spec §3): a model's tiers are the bands it sits in, so one model can be in several (the only model of an agent is cheap, standard and strong at once).
/// A recipe floor is exactly one of the three; a model clears a floor when it sits in that band or one above it.
/// </summary>
[Flags]
public enum ModelTier
{
    /// <summary>No band; the value of a floor that isn't set.</summary>
    None = 0,

    /// <summary>The first third of the agent's models, cheapest first.</summary>
    Cheap = 1,

    /// <summary>The middle models; every model when the thirds leave no middle.</summary>
    Standard = 2,

    /// <summary>The last third of the agent's models.</summary>
    Strong = 4,
}
