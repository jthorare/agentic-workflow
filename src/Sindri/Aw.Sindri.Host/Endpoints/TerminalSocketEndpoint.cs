using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Auth;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Features.TerminalSocket;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>
/// GET /sindri/sessions/{provider}/{id}/terminal (agents spec §15.8): the terminal WebSocket. It looks up the session's tmux pane first, so a bad ref (400), a session
/// without a pane (SND-AGENT-015, 409) or a gone one (SND-AGENT-016, 404) answers as plain HTTP before the upgrade. Then it upgrades and hands the socket to
/// <see cref="TerminalSocketSession"/>. The human token travels in the first message, never in the URL. A browser Origin is refused before the upgrade (SND-API-008),
/// and a plain request answers 400. It is a GET whose one service call is the pane lookup query; the route carries the command marker for the Origin refusal only.
/// </summary>
internal static class TerminalSocketEndpoint
{
    internal static RouteGroupBuilder MapTerminalSocket(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/sessions/{provider}/{id}/terminal", Terminal).WithMetadata(CommandRouteMetadata.Instance);
        return group;
    }

    // A method group like the event stream's: after its one service call (the pane lookup, AW1004) the socket is a long-lived stream, not a reply.
    private static async Task<IResult> Terminal(HttpContext context, string provider, string id, IService<FindTerminalPaneRequest, FindTerminalPaneResponse> find, IHumanTokenStore tokens)
    {
        var session = SessionRoute.Ref(provider, id);
        if (session.IsError)
        {
            return Refused(session.Error);
        }

        if (!context.WebSockets.IsWebSocketRequest)
        {
            return Refused(SindriErrors.InvalidInput.With("This route is a WebSocket: connect with ws://"));
        }

        var pane = await find.Handle(new FindTerminalPaneRequest(session.Value), context.RequestAborted).ConfigureAwait(false);
        if (pane.IsError)
        {
            return Refused(pane.Error);
        }

        // The pane source and the clock come from the container like the event stream's opener does, to stay inside AW1006's five parameters.
        var source = context.RequestServices.GetRequiredService<ITerminalPaneSource>();
        var time = context.RequestServices.GetRequiredService<TimeProvider>();
        var held = (await tokens.Read(context.RequestAborted).ConfigureAwait(false)).Match(static token => token, static _ => null);
        using var socket = await context.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);
        using var channel = new WebSocketTerminalChannel(socket);
        var terminal = new TerminalSocketSession(source, token => held?.Matches(token) == true, time);
        await terminal.Run(session.Value, channel, context.RequestAborted).ConfigureAwait(false);
        return TypedResults.Empty;
    }

    // Local, like the event stream opener's: a method-group endpoint that reached for the Host root's HttpResults closed a namespace cycle (ARCH-CYCLE-001).
    private static Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult Refused(Error error) => TypedResults.Problem(title: error.Code, detail: error.Message, statusCode: StatusOf(error.Kind));

    private static int StatusOf(ErrorKind kind)
    {
        if (kind == ErrorKind.Invalid)
        {
            return StatusCodes.Status400BadRequest;
        }

        if (kind == ErrorKind.NotFound)
        {
            return StatusCodes.Status404NotFound;
        }

        return kind == ErrorKind.Conflict ? StatusCodes.Status409Conflict : StatusCodes.Status500InternalServerError;
    }
}
