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
        _ = group.MapGet("/sessions/{provider}/{id}/terminal", static async (HttpContext context, string provider, string id, ITerminalPaneSource source, IHumanTokenStore tokens, TimeProvider time) =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: SindriErrors.InvalidInput.Code, detail: "This route is a WebSocket: connect with ws://");
            }

            var held = (await tokens.Read(context.RequestAborted).ConfigureAwait(false)).Match(static token => token, static _ => null);
            using var socket = await context.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);
            using var channel = new WebSocketTerminalChannel(socket);
            var session = new TerminalSocketSession(source, token => held?.Matches(token) == true, time);
            await session.Run(provider, id, channel, context.RequestAborted).ConfigureAwait(false);
            return Results.Empty;
        }).WithMetadata(CommandRouteMetadata.Instance);
        return group;
    }
}
