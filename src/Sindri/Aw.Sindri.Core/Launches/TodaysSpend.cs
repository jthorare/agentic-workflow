namespace Aw.Sindri.Core.Launches;

/// <summary>What the agents have spent today as far as it is known now (#3418).</summary>
/// <param name="Used">Spend per agent id; an id without an entry has no usage reader or is counting.</param>
/// <param name="Counting">Ids whose first spend read of the day hasn't landed.</param>
public sealed record TodaysSpend(IReadOnlyDictionary<string, AgentSpend> Used, IReadOnlySet<string> Counting)
{
    /// <summary>No spend known and nothing counting.</summary>
    public static TodaysSpend None { get; } = new(new Dictionary<string, AgentSpend>(StringComparer.Ordinal), new HashSet<string>(StringComparer.Ordinal));
}
