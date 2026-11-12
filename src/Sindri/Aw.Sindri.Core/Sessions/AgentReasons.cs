namespace Aw.Sindri.Core.Sessions;

/// <summary>The Agents page's reasons (Agents page spec §3.2): a pure function from what the collector read to the ordered reasons that hold now.</summary>
public static class AgentReasons
{
    /// <summary>The reasons that hold for one row, lowest class first; the first is the primary. Empty when none holds.</summary>
    /// <param name="inputs">What was read for the row.</param>
    /// <returns>The reasons, ordered by class and then by when they began.</returns>
    public static IReadOnlyList<AgentReason> Evaluate(AgentReasonInputs inputs) => throw new NotSupportedException();
}
