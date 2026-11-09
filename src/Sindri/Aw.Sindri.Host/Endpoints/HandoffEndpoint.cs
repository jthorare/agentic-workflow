using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Kernel;
using Aw.Sindri.Core.Auth;
using Aw.Sindri.Features.CreateHandoffItem;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>
/// POST /sindri/handoff (#2258 PR 12): a local app or script hands Sindri work, and the same item a claimed inbox offer makes comes out. Loopback peers only (403), the per-start
/// token in X-Sindri-Token (401), at most 30 a minute (429, counted after the token is checked) and a body of at most 64 KB (413). Send 'X-Sindri-Caller: human' with the token.
/// </summary>
internal static class HandoffEndpoint
{
    internal const int MaxBytes = 65_536;
    private const string TokenHeader = "X-Sindri-Token";

    internal static RouteGroupBuilder MapHandoff(this RouteGroupBuilder group)
    {
        _ = group.MapPost("/handoff", static async (HttpRequest http, IHandoffPeer peer, HeldHumanToken token, HandoffRateLimit limit, JsonSerializerOptions json,
            IService<CreateHandoffItemRequest, HandoffItemCreatedResponse> service, CancellationToken ct) =>
        {
            if (Refusal(http, peer, token, limit) is { } refused)
            {
                return refused;
            }

            var body = await Read(http.Body, ct).ConfigureAwait(false);
            if (body is null)
            {
                return Problem(SindriErrors.HandoffTooLarge, StatusCodes.Status413PayloadTooLarge);
            }

            return Parse(body, json) is { } request
                ? (await service.Handle(request, ct).ConfigureAwait(false)).ToHttpResult()
                : Problem(SindriErrors.InvalidInput, StatusCodes.Status400BadRequest, "the body isn't a handoff: {\"title\", \"source\", ...} (fix: see sindri handoff send)");
        });
        return group;
    }

    private static Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult? Refusal(HttpRequest http, IHandoffPeer peer, HeldHumanToken token, HandoffRateLimit limit)
    {
        if (peer.Of(http.HttpContext) is not { } address || !IPAddress.IsLoopback(address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address))
        {
            return Problem(SindriErrors.AuthRefused, StatusCodes.Status403Forbidden, "POST /sindri/handoff takes callers on this machine only");
        }

        var presented = http.Headers.TryGetValue(TokenHeader, out var values) ? values.ToString() : null;
        if (token.Current is not { } held || !held.Matches(presented))
        {
            return Problem(SindriErrors.AuthTokenRequired, StatusCodes.Status401Unauthorized, "send this start's token in X-Sindri-Token (fix: restart the app or rerun the command)");
        }

        return limit.TryTake() ? null : Problem(SindriErrors.HandoffRateLimited, StatusCodes.Status429TooManyRequests);
    }

    /// <summary>The body's bytes, or null when it is over the cap.</summary>
    private static async Task<byte[]?> Read(Stream body, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        int read;
        while ((read = await body.ReadAsync(chunk, ct).ConfigureAwait(false)) > 0)
        {
            if (buffer.Length + read > MaxBytes)
            {
                return null;
            }

            await buffer.WriteAsync(chunk.AsMemory(0, read), ct).ConfigureAwait(false);
        }

        return buffer.ToArray();
    }

    private static CreateHandoffItemRequest? Parse(byte[] body, JsonSerializerOptions json)
    {
        try
        {
            return JsonSerializer.Deserialize(body, (JsonTypeInfo<CreateHandoffItemRequest>)json.GetTypeInfo(typeof(CreateHandoffItemRequest)));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // TypedResults directly, not HttpResults: this folder must not depend on the root namespace (ARCH-CYCLE-001), and these errors carry no extensions.
    private static Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult Problem(ErrorDefinition error, int status, string? detail = null) =>
        TypedResults.Problem(title: error.Code, detail: detail ?? error.Title, statusCode: status);
}
