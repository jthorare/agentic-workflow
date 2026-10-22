using System.Text;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Desk;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing.Forge;
using Aw.Sindri.Testing.Workflow.Registry;
using Aw.Sindri.Tests.Ledger;
using Aw.Sindri.Tests.Workflow.Graph;

namespace Aw.Sindri.Tests.PrWatch;

/// <summary>
/// The real watched-pulls adapter (PR babysitter spec §6.2) over instances that wait on 'pr.changed' and hold a 'fact:pr' stored in the home's fact files:
/// the contract, and the cases the adapter alone has (an instance that waits on something else, or holds no 'fact:pr').
/// </summary>
[Lane(TestLane.Integration)]
public sealed class InstanceWatchedPullsContractTests : WatchedPullsContract, IDisposable
{
    private readonly TempHome _home = new();

    public void Dispose() => _home.Dispose();

    protected override WatchedPullsUnderTest Create()
    {
        var book = new WaitingInstances(new HomeFactStore(new WorkflowsHome(_home.Path)));
        return new(book.Adapter, book.Watch, book.Unwatch);
    }

    [Fact]
    public async Task An_instance_waiting_on_something_else_is_not_listed()
    {
        var book = new WaitingInstances(new HomeFactStore(new WorkflowsHome(_home.Path)));
        book.Add("ITM-1", "https://forge.test/acme/widgets/pull/1", state: "building");

        var listed = await book.Adapter.List(TestContext.Current.CancellationToken);

        Assert.Empty(listed.Value);
    }

    [Fact]
    public async Task An_instance_without_a_fact_pr_is_not_listed()
    {
        var book = new WaitingInstances(new HomeFactStore(new WorkflowsHome(_home.Path)));
        book.Add("ITM-1", link: null, state: "waiting");

        var listed = await book.Adapter.List(TestContext.Current.CancellationToken);

        Assert.Empty(listed.Value);
    }

    [Fact]
    public async Task A_fact_pr_stored_as_bare_text_is_read_as_the_link()
    {
        var book = new WaitingInstances(new HomeFactStore(new WorkflowsHome(_home.Path)));
        book.Add("ITM-1", "https://forge.test/acme/widgets/pull/1", state: "waiting", json: false);

        var listed = await book.Adapter.List(TestContext.Current.CancellationToken);

        Assert.Equal(new[] { new WatchedPull("https://forge.test/acme/widgets/pull/1", "ITM-1") }, listed.Value);
    }

    /// <summary>Instances in memory: a recipe that waits on 'pr.changed' in 'waiting', and the adapter over them.</summary>
    private sealed class WaitingInstances : IWorkflowInstances, IInstanceReader, IApprovedRecipes
    {
        private static readonly RecipeId _id = RecipeId.From("workflow.watch");
        private static readonly RecipeVersion _version = new(_id, 1, RegistryData.Hash("watch"));
        private readonly Recipe _recipe = GraphRecipe.Of(
            "waiting",
            [("building", GraphRecipe.Active()), ("waiting", GraphRecipe.WaitForEvent("pr.changed")), ("done", GraphRecipe.Terminal())],
            [GraphRecipe.Go("building", "waiting"), GraphRecipe.Go("waiting", "done", "wait.satisfied")],
            id: "workflow.watch");
        private readonly HomeFactStore _facts;
        private readonly List<(InstanceView View, string? Link)> _instances = [];

        public WaitingInstances(HomeFactStore facts)
        {
            _facts = facts;
            Adapter = new InstanceWatchedPulls(this, this, this, facts);
        }

        public InstanceWatchedPulls Adapter { get; }

        public void Watch(string link, string item) => Add(item, link, state: "waiting");

        public void Unwatch(string link)
        {
            for (var i = 0; i < _instances.Count; i++)
            {
                if (string.Equals(_instances[i].Link, link, StringComparison.Ordinal))
                {
                    _instances[i] = (_instances[i].View with { State = "done", Outcome = OutcomeName.From("resolved") }, link);
                }
            }
        }

        public void Add(string item, string? link, string state, bool json = true)
        {
            var address = InstanceAddress.Root(ItemKey.From(item), _id, _instances.Count + 1);
            var view = new InstanceView(address, _version, state, Detail: null, DateTimeOffset.FromUnixTimeMilliseconds(1_000), string.Empty, [], Question: null, Task: null, Run: null, Children: [], Outcome: null)
            {
                Id = WorkflowInstanceId.Of(address),
                ItemId = ItemKey.From(item),
            };
            if (link is not null)
            {
                var text = json ? "\"" + link + "\"" : link;
                var stored = _facts.Put(Encoding.UTF8.GetBytes(text), CancellationToken.None).AsTask().GetAwaiter().GetResult(); // why: the contract's hands are synchronous
                view = view with { FactShas = new Dictionary<string, string>(StringComparer.Ordinal) { ["fact:pr"] = stored.Value.Sha256 } };
            }

            _instances.Add((view, link));
        }

        public ValueTask<Result<IReadOnlyList<InstanceRow>>> List(string? query, bool ended, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Results.Success<IReadOnlyList<InstanceRow>>([.. _instances.Select(static i => new InstanceRow(i.View.Address.Value, i.View.State, NeedsYou: false))]));

        public ValueTask<Result<IReadOnlyList<InstanceRow>>> List(InstanceFilter filter, bool ended, CancellationToken cancellationToken) => List((string?)null, ended, cancellationToken);

        public ValueTask<Result<InstanceView>> Get(InstanceAddress address, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Results.Success(_instances.First(i => i.View.Address == address).View));

        public ValueTask<Result<ApprovedRecipe>> Latest(RecipeId id, CancellationToken cancellationToken) => Found();

        public ValueTask<Result<ApprovedRecipe>> Find(RecipeId id, RecipeHash hash, CancellationToken cancellationToken) => Found();

        public ValueTask<Result<ApprovalCheck>> Check(RecipeVersion recipe, CancellationToken cancellationToken) => ValueTask.FromResult(Results.Success(ApprovalCheck.Holds));

        private ValueTask<Result<ApprovedRecipe>> Found() => ValueTask.FromResult(Results.Success(new ApprovedRecipe(_version, _recipe)));
    }
}
