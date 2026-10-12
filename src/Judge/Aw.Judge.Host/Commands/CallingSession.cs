using Aw.Judge.Core.Ids;

namespace Aw.Judge.Host.Commands;

/// <summary>The session that called the judge (AW_SESSION_ID), read once by Compose.</summary>
/// <param name="Id">The session, or null when the variable is unset or isn't a valid session id.</param>
internal sealed record CallingSession(SessionId? Id)
{
    /// <summary>Reads AW_SESSION_ID's value.</summary>
    internal static CallingSession From(string? value) =>
        new(value is not null && SessionId.TryFrom(value, out var id) ? id : null);
}
