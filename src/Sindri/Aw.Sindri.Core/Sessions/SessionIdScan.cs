using Aw.Sindri.Core.Ids;


namespace Aw.Sindri.Core.Sessions;

/// <summary>What 'session wrap' found when it looked for the agent's session id.</summary>
/// <param name="Id">The one id found, or null.</param>
/// <param name="Ambiguous">True when more than one candidate remained: no id is used (launcher spec §4).</param>
public sealed record SessionIdScan(SessionId? Id, bool Ambiguous)
{
    /// <summary>Nothing found yet.</summary>
    public static SessionIdScan None { get; } = new(Id: null, Ambiguous: false);
}
