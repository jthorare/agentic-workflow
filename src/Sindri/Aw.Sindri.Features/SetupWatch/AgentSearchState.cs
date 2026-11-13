namespace Aw.Sindri.Features.SetupWatch;

/// <summary>What the agent search remembers between ticks (the search is scoped, one per tick): that no agent was found, on which PATH, and how many ticks ago.</summary>
internal sealed class AgentSearchState
{
    /// <summary>Gets or sets a value indicating whether the last search found no agent.</summary>
    internal bool Missed { get; set; }

    /// <summary>Gets or sets the PATH of the last search that found no agent.</summary>
    internal string? MissedPath { get; set; }

    /// <summary>Gets or sets the number of ticks since that search.</summary>
    internal int TicksSince { get; set; }
}
