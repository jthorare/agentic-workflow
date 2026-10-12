using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Recipes;

/// <summary>A state's timeout (spec §3.4): '{ after, then: park | escalate | retry }'.</summary>
/// <param name="After">How long the state may run.</param>
/// <param name="Then">What happens when it runs past that.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TimeoutSpec(RecipeDuration After, TimeoutAction Then);
