using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Features.CreateItem;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.CreateItem;

/// <summary>A 'create_item' node makes its item through the create-item handler (agent-first PR 9): same event, same checks, as 'service:workflow'.</summary>
public sealed class WorkflowItemMakerTests
{
    private static readonly WorkflowRunId _run = WorkflowRunId.From("nightly@1:1");

    private readonly SliceHarness _h = new();
    private readonly InMemoryWorkflowRunStore _runs = new(new FakeTimeProvider(KnownInstants.Epoch));

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private WorkflowItemMaker Maker()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton(_h.Callers).AddSingleton<IWorkflowRunStore>(_runs).AddScoped(_ => new CreateItemService(_h.Profiles, _h.Held, _h.Store, _h.Events, _h.Ids, _h.Callers));
        return new WorkflowItemMaker(services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>());
    }

    private async Task Seed() =>
        _ = (await _runs.Insert(new WorkflowRun(_run, WorkflowId.From("WF-" + new string('1', 32)), 1, """{"kind":"manual"}""", WorkflowRunStatus.Running, Outputs: null, Error: null, ItemId: null, KnownInstants.Epoch, EndedAt: null), Token)).Value;

    private async Task Approve(params string[] repos)
    {
        var loaded = SliceHarness.Profile();
        _ = await _h.Profiles.Save(loaded with { Profile = loaded.Profile with { Repos = [.. repos.Select(r => new RepoEntry(r, "/repos/" + r))] } }, Token);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_item_is_made_with_the_title_body_repo_and_link_and_its_id_comes_back()
    {
        _ = await _h.HoldLease();
        await Approve("web");
        await Seed();

        var made = await Maker().Create(new ItemToCreate(_run, "  Fix login  ", "web", "details", "linear:issue/abc-1"), Token);

        Assert.True(made.IsSuccess, made.IsError ? made.Error.ToString() : null);
        var recorded = Assert.Single(_h.Store.Events);
        Assert.Equal(EventTypes.ItemCreated, recorded.Envelope.Type);
        Assert.Equal(made.Value, recorded.Item);
        Assert.Equal(made.Value, (await _runs.Get(_run, Token)).Value.ItemId?.Value);   // the run is linked to its item
        var payload = _h.Events.Payload<ItemCreated>(recorded).Value;
        Assert.Equal(("Fix login", "details", RepoName.From("web"), RecordKey.From("linear:issue/abc-1")), (payload.Title, payload.Body, payload.Repo, payload.Link));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_repo_the_profile_does_not_have_and_a_title_the_handler_refuses_record_nothing()
    {
        _ = await _h.HoldLease();
        await Approve("web");

        var unknown = await Maker().Create(new ItemToCreate(_run, "x", "api", Body: null, LinearIssue: null), Token);
        var blank = await Maker().Create(new ItemToCreate(_run, "   ", Repo: null, Body: null, LinearIssue: null), Token);

        Assert.Equal(SindriErrors.InvalidInput.Code, unknown.Error.Code);
        Assert.Equal(SindriErrors.InvalidInput.Code, blank.Error.Code);
        Assert.Empty(_h.Store.Events);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_repo_less_item_is_made_when_the_node_names_no_repo()
    {
        _ = await _h.HoldLease();
        await Seed();

        var made = await Maker().Create(new ItemToCreate(_run, "Loose end", Repo: null, Body: null, LinearIssue: null), Token);

        Assert.True(made.IsSuccess, made.IsError ? made.Error.ToString() : null);
        Assert.Null(_h.Events.Payload<ItemCreated>(Assert.Single(_h.Store.Events)).Value.Repo);
    }
}
