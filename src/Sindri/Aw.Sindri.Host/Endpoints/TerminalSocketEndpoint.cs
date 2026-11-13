using Aw.Sindri.Core;
using Aw.Sindri.Core.Auth;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Features.TerminalSocket;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>
/// GET /sindri/sessions/{provider}/{id}/terminal (agents spec §15.8): the terminal WebSocket. It holds no logic of its own: it upgrades the request and hands the socket to
/// <see cref="TerminalSocketSession"/>. The human token travels in the first message, never in the URL. A browser Origin is refused before the upgrade (SND-API-008),
/// and a plain request answers 400. It is a GET that is not a query: the route carries the command marker for the Origin refusal only.
/// </summary>
internal static class TerminalSocketEndpoint
{
    internal static RouteGroupBuilder MapTerminalSocket(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/sessions/{provider}/{id}/terminal", Terminal).WithMetadata(CommandRouteMetadata.Instance);
        return group;
    }

    // A method group like the event stream's: the socket is a long-lived stream, not one command, so there is no single IService call to make (AW1004).
    private static async Task<IResult> Terminal(HttpContext context, string provider, string id, ITerminalPaneSource source, IHumanTokenStore tokens)
    {
        // The clock comes from the container like the event stream's opener does, to stay inside AW1006's five parameters.
        var time = context.RequestServices.GetRequiredService<TimeProvider>();
        var session = SessionRoute.Ref(provider, id);
        if (session.IsError)
        {
            return session.ToHttpResult();
        }

        if (!context.WebSockets.IsWebSocketRequest)
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: SindriErrors.InvalidInput.Code, detail: "This route is a WebSocket: connect with ws://");
        }

        var held = (await tokens.Read(context.RequestAborted).ConfigureAwait(false)).Match(static token => token, static _ => null);
        using var socket = await context.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);
        using var channel = new WebSocketTerminalChannel(socket);
        var terminal = new TerminalSocketSession(source, token => held?.Matches(token) == true, time);
        await terminal.Run(session.Value, channel, context.RequestAborted).ConfigureAwait(false);
        return Results.Empty;
    }
}
