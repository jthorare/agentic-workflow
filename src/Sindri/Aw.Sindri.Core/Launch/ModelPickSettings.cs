namespace Aw.Sindri.Core.Launch;

/// <summary>What the profile says about picking a model for one agent's step (model-pick spec §8).</summary>
/// <param name="PickOn">True when the pick is on (the profile says so and the owner's consent is recorded); false for off and for absent.</param>
/// <param name="Pin">The model every step of this agent (or state) must run on, or null.</param>
/// <param name="Cap">The strongest model a pick may reach, or null.</param>
/// <param name="Models">The agent's models cheapest first as the profile lists them, or null to use what the probe discovered.</param>
/// <param name="Default">The model the profile names as the agent's default, or null.</param>
/// <param name="DenyPaths">The profile's index.denyPaths globs: files that match are never sent to Jev.</param>
public sealed record ModelPickSettings(bool PickOn, string? Pin = null, string? Cap = null, IReadOnlyList<ModelFacts>? Models = null, string? Default = null, IReadOnlyList<string>? DenyPaths = null)
{
    /// <summary>Pick off, nothing pinned or capped, no list of its own: what an absent profile key means.</summary>
    public static ModelPickSettings Off { get; } = new(PickOn: false);
}
