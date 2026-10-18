namespace Aw.Sindri.Core.Sandbox.Plan;

/// <summary>Which installed hooks run in the session.</summary>
/// <param name="Kept">Hook names that run, in inventory order, then any hook the inventory doesn't list.</param>
/// <param name="Omitted">Hook names left out of the session's settings (<see cref="HookTreatment.Drop"/>).</param>
/// <param name="Notes">Plan notes for the owner: one line per installed hook the inventory doesn't list (kept, the default).</param>
public sealed record PlanHooks(IReadOnlyList<string> Kept, IReadOnlyList<string> Omitted, IReadOnlyList<string> Notes);
