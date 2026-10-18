using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Core.Sessions;

/// <summary>A session's name, '&lt;provider&gt;/&lt;id&gt;' (collector spec §2).</summary>
/// <param name="Provider">The agent.</param>
/// <param name="Id">The session id.</param>
public readonly record struct SessionRef(AgentId Provider, SessionId Id)
{
    /// <summary>The text form, 'claude-code/3970893f-…'.</summary>
    public override string ToString() => Provider.Value + "/" + Id.Value;
}
