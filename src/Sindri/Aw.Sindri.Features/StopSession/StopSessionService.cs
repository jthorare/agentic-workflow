using System.Text.RegularExpressions;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Features.StopSession;

/// <summary>
/// Ends a one-off session Sindri started (agents spec §15.2): 'kill-session' on 'sindri-&lt;actId&gt;', only when a start recorded that act id. The id may be the tmux name, or the provider's session id of
/// a row whose tmux facts name such a session on Sindri's own socket. Anything else is SND-AGENT-019. A session that is already gone counts as stopped.
/// </summary>
/// <param name="book">The stored starts.</param>
/// <param name="host">The tmux host that ends the session.</param>
/// <param name="callers">The caller: only the person may stop.</param>
/// <param name="snapshot">The collector's last pass, to find the tmux name of a provider session id.</param>
internal sealed partial class StopSessionService(IStartedSessions book, ISessionHost host, ICallerContextAccessor callers, ISessionSnapshot snapshot) : IService<StopSessionRequest, StoppedSession>
{
    public async ValueTask<Result<StoppedSession>> Handle(StopSessionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (callers.Current is not { Class: CallerClass.Human })
        {
            return SindriErrors.AuthRefused.With("stopping a session is for the person: use the app or 'sindri agents stop' in a terminal");
        }

        var name = TmuxName(request.Ref);
        var match = name is null ? null : OneOff.Match(name);
        if (match is not { Success: true } || !ActId.TryFrom(match.Groups["act"].Value, out var act) || await book.Find(act, cancellationToken).ConfigureAwait(false) is null)
        {
            return SindriErrors.AgentNotStartedHere.With("only a session started with 'sindri agents start' (or the app) can be stopped here");
        }

        var stopped = await host.Stop(new HostRef(HostKind.Tmux, "sindri-" + act.Value), cancellationToken).ConfigureAwait(false);
        return stopped.IsError ? stopped.Error : new StoppedSession(SessionId.From("sindri-" + act.Value), act, "ended");
    }

    private string? TmuxName(SessionRef session)
    {
        if (OneOff.IsMatch(session.Id.Value))
        {
            return session.Id.Value;
        }

        var found = snapshot.Current is { } pass ? SessionRows.Find(pass, session) : null;
        return found?.Host.Tmux is { SocketKind: "sindri", Session: { Length: > 0 } tmux } ? tmux : null;
    }

    [GeneratedRegex(@"\Asindri-(?<act>[a-z0-9]{8,32})\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex OneOff { get; }
}
