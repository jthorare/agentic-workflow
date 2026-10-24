using System.Net;
using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Features.ListInstances;
using Aw.Sindri.Host.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.ListInstances;

/// <summary>GET /sindri/workflow/instances binds every filter as text (API review api-r1-5): the service parses root and needsMe, so a bad one is the Result error shape, not the binder's 400.</summary>
[Lane(TestLane.Unit)]
public sealed class ListInstancesEndpointTests
{
    private static async Task<(HttpResponseMessage Response, string Body, ListInstancesRequest? Seen)> Get(string query)
    {
        ListInstancesRequest? seen = null;
        var builder = WebApplication.CreateSlimBuilder();
        _ = builder.WebHost.UseTestServer();
        _ = builder.Services.AddSingleton<IService<ListInstancesRequest, InstanceList>>(new Recording(r => seen = r));
        await using var app = builder.Build();
        _ = app.MapGroup("/sindri").MapListInstances();
        await app.StartAsync(TestContext.Current.CancellationToken);

        using var client = app.GetTestClient();
        var response = await client.GetAsync($"/sindri/workflow/instances{query}", TestContext.Current.CancellationToken);
        return (response, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), seen);
    }

    [Fact]
    public async Task Every_filter_is_bound_by_name_as_text()
    {
        var (response, _, seen) = await Get("?needsMe=true&item=ITM-1&state=parked&recipe=workflow.step&urgency=incident&subject=fact%3Aspec&root=false&open=true&query=Q&address=T-1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            new ListInstancesRequest("true", ItemKey.From("ITM-1"), "parked", "Q", "T-1", "workflow.step", "incident", "fact:spec", "false", "true"),
            seen);
    }

    [Fact]
    public async Task No_filter_binds_nothing()
    {
        var (_, _, seen) = await Get(string.Empty);

        Assert.Equal(new ListInstancesRequest(), seen);
    }

    [Theory]
    [InlineData("?root=maybe", "root='maybe' isn't true or false")]
    [InlineData("?needsMe=1", "needsMe='1' isn't true or false")]
    [InlineData("?open=maybe", "open='maybe' isn't true or false")]
    public async Task A_value_that_is_not_true_or_false_reaches_the_service_so_the_answer_is_its_error(string query, string message)
    {
        var service = new ListInstancesService(new NoDirectory(), new RecordingInstances([]), new EndedInstanceViews(new FixedReader([])), new FixedItemViews(), new FixedCaller(current: null), rowBuilder: new InstanceRows(new FakeTimeProvider()));
        var builder = WebApplication.CreateSlimBuilder();
        _ = builder.WebHost.UseTestServer();
        _ = builder.Services.AddSingleton<IService<ListInstancesRequest, InstanceList>>(service);
        await using var app = builder.Build();
        _ = app.MapGroup("/sindri").MapListInstances();
        await app.StartAsync(TestContext.Current.CancellationToken);

        using var client = app.GetTestClient();
        var response = await client.GetAsync($"/sindri/workflow/instances{query}", TestContext.Current.CancellationToken);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("SND-CLI-003", problem.RootElement.GetProperty("title").GetString());
        Assert.Contains(message, problem.RootElement.GetProperty("detail").GetString(), StringComparison.Ordinal);
    }

    private sealed class Recording(Action<ListInstancesRequest> seen) : IService<ListInstancesRequest, InstanceList>
    {
        public ValueTask<Result<InstanceList>> Handle(ListInstancesRequest request, CancellationToken cancellationToken)
        {
            seen(request);
            return ValueTask.FromResult(Results.Success(new InstanceList([], new ListCounts(0, 0, 0, 0, 0), InstanceList.EmptyText, DateTimeOffset.UnixEpoch)));
        }
    }
}
