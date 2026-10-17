using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Features.OpenItem;

/// <summary>The agent an open will run (<see cref="OpenTarget"/>).</summary>
/// <param name="Target">The agent.</param>
/// <param name="ChosenBy">'click' or 'default'.</param>
/// <param name="Warnings">What the person should know ('budget-soft').</param>
internal sealed record OpenDecision(AgentId Target, string ChosenBy, IReadOnlyList<string> Warnings);
