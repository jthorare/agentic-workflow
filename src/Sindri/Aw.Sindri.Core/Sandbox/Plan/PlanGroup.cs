namespace Aw.Sindri.Core.Sandbox.Plan;

/// <summary>A named group of host patterns the session may reach.</summary>
/// <param name="Name">The group name.</param>
/// <param name="Hosts">Host names, or '*.' patterns.</param>
public sealed record PlanGroup(string Name, IReadOnlyList<string> Hosts);
