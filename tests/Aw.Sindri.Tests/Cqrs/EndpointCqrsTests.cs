using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Host;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Catalog;
using Aw.Sindri.Tests.Ledger;
using Aw.Sindri.Tests.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Cqrs;

/// <summary>ARCH-CQRS-001 and the catalog's route rule over the real endpoint table, and the rule proven red on a fixture host.</summary>
public sealed class EndpointCqrsTests : IDisposable
{
    private readonly TempHome _home = new();

    public void Dispose() => _home.Dispose();

    [Fact, Trait("Lane", "integration")]
    public async Task Every_get_resolves_a_query_and_every_post_a_command()
    {
        await using var app = Host();
        var endpoints = Endpoints(app);

        var violations = EndpointCqrsRules.Check(endpoints, app.Services.GetRequiredService<ICommandCatalog>());

        Assert.True(violations.Count == 0, CatalogRules.Lines(violations));
        Assert.Contains("GET /sindri/commands", endpoints.Endpoints.OfType<RouteEndpoint>().SelectMany(EndpointCqrsRules.Routes), StringComparer.Ordinal);
        var mcp = endpoints.Endpoints.OfType<RouteEndpoint>().Where(e => (e.RoutePattern.RawText ?? string.Empty).StartsWith("/sindri/mcp", StringComparison.Ordinal)).ToList();
        Assert.NotEmpty(mcp);
        Assert.All(mcp, e => Assert.True(EndpointCqrsRules.IsMcp(e), e.RoutePattern.RawText));   // why: the exemption covers /sindri/mcp itself, no route under it
    }

    [Fact, Trait("Lane", "integration")]
    public async Task EndpointCqrs_the_subgraph_is_a_get_and_a_query_and_no_post_serves_a_query()
    {
        await using var app = Host();
        var routes = Endpoints(app).Endpoints.OfType<RouteEndpoint>().SelectMany(EndpointCqrsRules.Routes).ToList();
        var queryPosts = Endpoints(app).Endpoints.OfType<RouteEndpoint>()
            .Where(e => EndpointCqrsRules.Routes(e).Any(r => r.StartsWith("POST ", StringComparison.Ordinal)) && EndpointCqrsRules.Request(e) is { } request
                && request.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IQuery<>)))
            .SelectMany(EndpointCqrsRules.Routes).ToList();

        Assert.Contains("GET /sindri/context/subgraph", routes, StringComparer.Ordinal);
        Assert.Contains("QUERY /sindri/context/subgraph", routes, StringComparer.Ordinal);
        Assert.DoesNotContain("POST /sindri/context/subgraph", routes, StringComparer.Ordinal);
        Assert.Empty(queryPosts);   // why: queries are GET or QUERY, with no carve-out list
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Cqrs_rule_allows_a_query_on_QUERY_and_flags_a_command_on_QUERY()
    {
        await using var app = WebApplication.CreateSlimBuilder().Build();
        _ = app.MapMethods("/sindri/fixture/query-ok", ["QUERY"], ([FromServices] IService<FixtureQuery, string> _) => Results.Ok());
        _ = app.MapMethods("/sindri/fixture/query-command", ["QUERY"], ([FromServices] IService<FixtureApply, CommandAccepted> _) => Results.Ok());
        _ = app.MapPost("/sindri/fixture/post-query", ([FromServices] IService<FixtureQuery, string> _) => Results.Ok());

        var violations = EndpointCqrsRules.Check(Endpoints(app), new InMemoryCommandCatalog(IssueRow()));

        Assert.Equal(
            [
                "ARCH-CQRS-001: QUERY /sindri/fixture/query-command serves FixtureApply, which isn't an IQuery<>; a QUERY is a query",
                "ARCH-CQRS-001: POST /sindri/fixture/post-query serves FixtureQuery, which isn't an ICommand<> or IIssue<>; a POST is a command",
            ],
            violations);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Every_ready_row_route_is_mapped()
    {
        await using var app = Host();
        var mapped = Endpoints(app).Endpoints.OfType<RouteEndpoint>().SelectMany(EndpointCqrsRules.Routes).ToHashSet(StringComparer.Ordinal);
        var shipped = app.Services.GetRequiredService<ICommandCatalog>().Rows
            .Where(row => row.Route is not null && !row.Ui.StartsWith("pending:", StringComparison.Ordinal)).ToList();

        Assert.NotEmpty(shipped);
        Assert.All(shipped, row => Assert.Contains(row.Method + " " + EndpointCqrsRules.Normalize(row.Route!), mapped));   // why: the filter above keeps only rows with a route
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Cqrs_rule_flags_a_get_command_a_post_query_and_an_issue_route_outside_the_carve_outs()
    {
        await using var app = WebApplication.CreateSlimBuilder().Build();
        _ = app.MapGet("/sindri/fixture/get-command", ([FromServices] IService<FixtureApply, CommandAccepted> _) => Results.Ok());
        _ = app.MapPost("/sindri/fixture/post-query", ([FromServices] IService<FixtureQuery, string> _) => Results.Ok());
        _ = app.MapPost("/sindri/fixture/issue", ([FromServices] IService<FixtureIssue, string> _) => Results.Ok());
        _ = app.MapPost("/sindri/fixture/nothing", () => Results.Ok());
        _ = app.MapGet("/sindri/fixture/query", ([FromServices] IService<FixtureQuery, string> _) => Results.Ok());
        _ = app.MapPost("/sindri/workflow/challenges", ([FromServices] IService<FixtureIssue, string> _) => Results.Ok());
        _ = app.MapPost("/sindri/workflow/instances/{instanceId}/nonce", (string instanceId, [FromServices] IService<FixtureIssue, string> _) => Results.Ok());
        var catalog = new InMemoryCommandCatalog(IssueRow());

        var violations = EndpointCqrsRules.Check(Endpoints(app), catalog);

        Assert.Equal(
            [
                "ARCH-CQRS-001: GET /sindri/fixture/get-command serves FixtureApply, which isn't an IQuery<>; a GET is a query",
                "ARCH-CQRS-001: POST /sindri/fixture/post-query serves FixtureQuery, which isn't an ICommand<> or IIssue<>; a POST is a command",
                "ARCH-CQRS-001: POST /sindri/fixture/issue serves the issue FixtureIssue outside the carve-out routes",
                "ARCH-CQRS-001: POST /sindri/fixture/nothing resolves no IService<TRequest, _>; bind one service and call it",
            ],
            violations);
        Assert.Contains("aren't all of class 'issue'", Assert.Single(
            EndpointCqrsRules.Check(Endpoints(app), new InMemoryCommandCatalog(IssueRow() with { Class = ActionClass.Desk })),
            v => v.Contains("challenges", StringComparison.Ordinal)), StringComparison.Ordinal);
    }

    private static CompositeEndpointDataSource Endpoints(IEndpointRouteBuilder app) => new(app.DataSources);

    private static CommandRow IssueRow() =>
        new(ActionId.From("FixtureIssue"), typeof(FixtureIssue), Variant: null, ActionClass.Issue, ServiceGrants: [], NeedsTerminal: false,
            SelfAudited: false, Destructive: false, ReadsViews: false, Shortcut: null, "POST", "/sindri/workflow/challenges",
            CliForm.Parse("sindri fixture issue").Value, "ready");

    private WebApplication Host()
    {
        var app = LoopbackBinding.Build([], n => n == "AW_SINDRI_HOME" ? _home.Path : null, TimeProvider.System, NoTemporal.Use);
        _ = Pipeline.Configure(app);
        return app;
    }

    internal sealed record FixtureQuery : IQuery<string>;

    internal sealed record FixtureIssue : IIssue<string>;
}
