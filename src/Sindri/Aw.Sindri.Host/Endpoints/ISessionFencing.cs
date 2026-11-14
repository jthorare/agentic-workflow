using Aw.Sindri.Features.GetSession;
using Aw.Sindri.Features.ListSessions;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>
/// Writes the session reads for a session caller with the pane's question and the session's title inside the untrusted fence (Agents page, issue 3082): pane text and a start prompt's first line are
/// text a person or a program wrote. A port for the same reason as <see cref="IReviewFencing"/>: the fence lives in the MCP namespace, which already depends on this one (ARCH-CYCLE-001).
/// </summary>
internal interface ISessionFencing
{
    /// <summary>The list as a session reads it.</summary>
    /// <param name="list">The list.</param>
    /// <returns>The JSON answer with each row's question and title fenced.</returns>
    public IResult List(SessionList list);

    /// <summary>One session as a session reads it.</summary>
    /// <param name="view">The session.</param>
    /// <returns>The JSON answer with the row's question and title fenced.</returns>
    public IResult One(SessionView view);
}
