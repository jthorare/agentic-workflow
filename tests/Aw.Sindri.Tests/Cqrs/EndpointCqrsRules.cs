using System.Reflection;
using System.Text.RegularExpressions;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;

namespace Aw.Sindri.Tests.Cqrs;

/// <summary>
/// ARCH-CQRS-001 (API spec §2.5) over the endpoint data source, since minimal-API lambdas are invisible to ArchUnitNET:
/// every GET and every QUERY (the HTTP method for a read with a body) resolves an IService whose request is an IQuery; every POST one whose request is an ICommand, or an IIssue
/// on one of the carve-out routes with catalog rows of class 'issue'. Pure: each violation is one line.
/// The MCP endpoint (/sindri/mcp, API spec §8) is a transport, not a route: the SDK maps it, and each of its tools calls
/// one query service (Host/Mcp/McpToolTable), so it's skipped here.
/// </summary>
internal static partial class EndpointCqrsRules
{
    public const string Rule = "ARCH-CQRS-001";

    /// <summary>The routes an IIssue may take (API spec §2.5), with parameter names ignored.</summary>
    public static readonly IReadOnlyList<string> CarveOuts =
    [
        "POST /sindri/workflow/instances/{}/nonce",
        "POST /sindri/workflow/challenges",
        "POST /sindri/sessions/{}/heartbeat",
        "POST /sindri/sessions/{}/exit",
        "POST /sindri/launches/{}/limit-hit",   // the wrap's pids limit hit (sandbox spec §3.3), proven by the launch secret like exit
    ];

    public static IReadOnlyList<string> Check(EndpointDataSource endpoints, ICommandCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(catalog);
        return [.. endpoints.Endpoints.OfType<RouteEndpoint>()
            .Where(endpoint => !IsMcp(endpoint))   // the MCP transport's own route only; its tools are proven to take the query chain (ContextMcpTests)
            .SelectMany(endpoint => Violations(endpoint, catalog))];
    }

    /// <summary>The one route the MCP SDK maps: exactly /sindri/mcp (any method), nothing under it.</summary>
    /// <param name="endpoint">An endpoint.</param>
    /// <returns>True for the MCP route.</returns>
    public static bool IsMcp(RouteEndpoint endpoint) =>
        string.Equals((endpoint.RoutePattern.RawText ?? string.Empty).TrimEnd('/'), Host.Mcp.McpEndpoint.Path, StringComparison.Ordinal);

    /// <summary>The request type of the IService&lt;TRequest, _&gt; the endpoint's handler takes, or null.</summary>
    public static Type? Request(RouteEndpoint endpoint)
    {
        if (endpoint.Metadata.GetMetadata<Host.Endpoints.JobRouteMetadata>() is { } job)
        {
            return job.Request;   // MapJob's request delegate: the route takes the job request's chained start service
        }

        var parameters = endpoint.Metadata.OfType<IParameterBindingMetadata>().Select(p => p.ParameterInfo.ParameterType)
            .Concat(endpoint.Metadata.OfType<MethodInfo>().SelectMany(m => m.GetParameters()).Select(p => p.ParameterType));
        return parameters.FirstOrDefault(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IService<,>))?.GenericTypeArguments[0];
    }

    /// <summary>'METHOD /route' with parameter names dropped ('{itemId}' → '{}').</summary>
    public static IEnumerable<string> Routes(RouteEndpoint endpoint)
    {
        var route = Normalize(endpoint.RoutePattern.RawText ?? string.Empty);
        var methods = endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? ["*"];
        return methods.Select(method => method + " " + route);
    }

    /// <summary>A route with each '{parameter}' written as '{}', so a catalog row's route compares with the mapped pattern.</summary>
    public static string Normalize(string route) => Parameter.Replace(route, "{}");

    private static IEnumerable<string> Violations(RouteEndpoint endpoint, ICommandCatalog catalog)
    {
        var request = Request(endpoint);
        foreach (var route in Routes(endpoint))
        {
            var problem = request is null ? "resolves no IService<TRequest, _>; bind one service and call it"
                : route.StartsWith("GET ", StringComparison.Ordinal) ? (Is(request, typeof(IQuery<>)) ? null : $"serves {request.Name}, which isn't an IQuery<>; a GET is a query")
                : route.StartsWith("POST ", StringComparison.Ordinal) ? Post(route, request, catalog)
                : route.StartsWith("QUERY ", StringComparison.Ordinal) ? (Is(request, typeof(IQuery<>)) ? null : $"serves {request.Name}, which isn't an IQuery<>; a QUERY is a query")
                : "is neither a GET or QUERY (query) nor a POST (command)";
            if (problem is not null)
            {
                yield return $"{Rule}: {route} {problem}";
            }
        }
    }

    private static string? Post(string route, Type request, ICommandCatalog catalog)
    {
        if (Is(request, typeof(ICommand<>)))
        {
            return null;
        }

        if (!Is(request, typeof(IIssue<>)))
        {
            return $"serves {request.Name}, which isn't an ICommand<> or IIssue<>; a POST is a command";
        }

        var rows = catalog.For(request);
        return !CarveOuts.Contains(route, StringComparer.Ordinal) ? $"serves the issue {request.Name} outside the carve-out routes"
            : rows.Count == 0 || rows.Any(row => row.Class != ActionClass.Issue) ? $"serves {request.Name}, whose catalog rows aren't all of class 'issue'"
            : null;
    }

    private static bool Is(Type request, Type marker) =>
        request.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == marker);

    [GeneratedRegex("\\{[^}]*\\}", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Parameter { get; }
}
