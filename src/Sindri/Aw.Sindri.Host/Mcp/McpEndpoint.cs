using System.Reflection;
using Aw.Sindri.Core;
using ModelContextProtocol.Protocol;

namespace Aw.Sindri.Host.Mcp;

/// <summary>
/// The one Sindri MCP endpoint, /sindri/mcp (context spec §8.5, API spec §8, README X8): Streamable HTTP on the API port
/// in stateless mode, so a Sindri restart doesn't break a session, behind the LoopbackGuard. It holds no logic: every
/// tool is one call to a service through the same chain as the HTTP routes. API P8 adds its sindri_* tools to
/// <see cref="McpToolTable"/>.
/// </summary>
internal static partial class McpEndpoint
{
    /// <summary>The endpoint's path.</summary>
    internal const string Path = "/sindri/mcp";

    /// <summary>The server's name in the initialize answer.</summary>
    internal const string ServerName = "sindri";

    /// <summary>This build's version (the assembly's informational version), as the initialize answer and context_sources report it.</summary>
    internal static string Build { get; } =
        typeof(McpEndpoint).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";

    /// <summary>Registers the MCP server with its tools.</summary>
    /// <param name="services">The host's services.</param>
    /// <returns>The services.</returns>
    internal static IServiceCollection AddSindriMcp(this IServiceCollection services)
    {
        _ = services.AddHttpContextAccessor()
            .AddMcpServer(o => o.ServerInfo = new Implementation { Name = ServerName, Version = Build })
            .WithHttpTransport(o => o.Stateless = true)   // the 2.2.0 default; set so the intent is in code
            .WithListToolsHandler(McpToolTable.List)
            .WithCallToolHandler(McpToolTable.Call);
        return services;
    }

    /// <summary>
    /// Before the caller middleware: an MCP request is always a session (API §6), so every X-Sindri-* header is
    /// dropped but X-Sindri-Repo and X-Sindri-Launch (a Sindri child carries its launch, q100), and 'X-Sindri-Client: mcp' is set. MCP clients send no Origin,
    /// so any Origin is refused here (403, SND-API-008), loopback ones included.
    /// </summary>
    /// <param name="app">The app.</param>
    /// <returns>The app.</returns>
    internal static WebApplication UseMcpCaller(this WebApplication app)
    {
        _ = app.Use(async (context, next) =>
        {
            if (context.GetEndpoint()?.Metadata.GetMetadata<McpRouteMetadata>() is null)   // routing already ran: '/sindri/mcp/' and '/Sindri/MCP' match too
            {
                await next(context).ConfigureAwait(false);
                return;
            }

            var headers = context.Request.Headers;
            if (headers.ContainsKey("Origin"))
            {
                LogRefused(context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(McpEndpoint)), context.Request.Method, headers.Origin.ToString());
                await ApiProblems.Write(context, SindriErrors.OriginRefused.Code, StatusCodes.Status403Forbidden,
                    $"{context.Request.Path} refuses an Origin header: MCP clients send none (fix: connect with an MCP client, registered by 'sindri mcp register')").ConfigureAwait(false);
                return;
            }

            foreach (var name in headers.Keys.Where(IsStripped).ToList())   // why: nothing but the repo and the launch may ride an MCP call (a launch secret, a read-your-writes sequence, a token)
            {
                _ = headers.Remove(name);
            }

            headers[SindriHeaders.Client] = "mcp";
            await next(context).ConfigureAwait(false);
        });
        return app;
    }

    private static bool IsStripped(string name) =>
        name.StartsWith("X-Sindri-", StringComparison.OrdinalIgnoreCase)
        && !string.Equals(name, SindriHeaders.Repo, StringComparison.OrdinalIgnoreCase) && !string.Equals(name, SindriHeaders.Launch, StringComparison.OrdinalIgnoreCase);

    /// <summary>Maps the endpoint.</summary>
    /// <param name="app">The app.</param>
    /// <returns>The app.</returns>
    internal static WebApplication MapSindriMcp(this WebApplication app)
    {
        _ = app.MapMcp(Path).WithMetadata(McpRouteMetadata.Instance);
        return app;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "SND-API-008 {Method} /sindri/mcp refused: Origin '{Origin}'")]
    private static partial void LogRefused(ILogger logger, string method, string origin);
}
