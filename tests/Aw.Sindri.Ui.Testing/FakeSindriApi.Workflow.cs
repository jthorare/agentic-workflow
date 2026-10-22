using Aw.Kernel;
using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The workflow reads of the fake: each answers the API's goldens (<see cref="WorkflowAnswers"/>), the same text the handler serves.</summary>
internal sealed partial class FakeSindriApi
{
    /// <summary>When set, every workflow read answers this instead of the golden (a refusal, an odd body).</summary>
    public RouteAnswer? WorkflowAnswer { get; set; }

    /// <summary>When set, the answer of the instance list alone (the detail route keeps the goldens).</summary>
    public RouteAnswer? InstancesBody { get; set; }

    /// <summary>When set, the answer of an instance read, or null for the golden's own (the list route keeps the goldens).</summary>
    public Func<string, RouteAnswer?>? InstanceBody { get; set; }

    private int _instancesCalls;
    private readonly List<InstanceQuery> _instanceQueries = [];

    /// <summary>Every instance-list query made, in order: the Workflows page and the strip send the same root, open query, so a query can't tell which reader made a call: count the page's reads on the page (WorkflowsViewModel.Drawn).</summary>
    public IReadOnlyList<InstanceQuery> InstanceQueries { get { lock (_instanceQueries) { return [.. _instanceQueries]; } } }

    /// <summary>How many times the instance list was read.</summary>
    public int InstancesCalls => Volatile.Read(ref _instancesCalls);

    /// <summary>
    /// Held open until set, per query: the answer is decided when the request is made and arrives when the gate is set, so a test can have
    /// an older read finish after a newer one (the fake otherwise answers at once, in request order).
    /// </summary>
    public Dictionary<InstanceQuery, TaskCompletionSource> InstancesGates { get; } = [];

    public async Task<Result<InstanceList>> Instances(Uri api, InstanceQuery query, CancellationToken cancellationToken)
    {
        _ = Interlocked.Increment(ref _instancesCalls);
        LastInstancesQuery = query;
        lock (_instanceQueries)
        {
            _instanceQueries.Add(query);
        }

        var answer = Answer<InstanceList>(api, WorkflowRoutes.Instances(query), InstancesAnswer(query));
        if (InstancesGates.TryGetValue(query, out var gate))
        {
            await gate.Task.WaitAsync(cancellationToken).ConfigureAwait(true);
        }

        return answer;
    }

    public Task<Result<InstanceDetail>> Instance(Uri api, string id, CancellationToken cancellationToken)
    {
        LastInstanceId = id;
        return Task.FromResult(Answer<InstanceDetail>(api, WorkflowRoutes.Instance(id), InstanceAnswer(id)));
    }

    public Task<Result<FactView>> InstanceFact(Uri api, string id, string key, CancellationToken cancellationToken) =>
        Task.FromResult(Answer<FactView>(api, WorkflowRoutes.Fact(id, key), FactAnswer(id, key)));

    public async Task<Result<RecipeView>> Recipe(Uri api, string recipe, string? hash, CancellationToken cancellationToken)
    {
        LastRecipeRead = (recipe, hash);
        await WaitFor(RecipeGate, cancellationToken).ConfigureAwait(false);   // the gate is read when the call starts, so a test can hand each call its own
        return Answer<RecipeView>(api, WorkflowRoutes.Recipe(recipe, hash), RecipeAnswer(recipe, hash));
    }

    /// <summary>Held open until set: a recipe read that started while it was set waits for it.</summary>
    public TaskCompletionSource? RecipeGate { get; set; }

    /// <summary>The recipe and hash of the latest recipe read; null before the first.</summary>
    public (string Recipe, string? Hash)? LastRecipeRead { get; private set; }

    /// <summary>What GET /workflow/instances answers for a query.</summary>
    /// <param name="query">The query.</param>
    /// <returns>The answer.</returns>
    internal RouteAnswer InstancesAnswer(InstanceQuery query)
    {
        var answer = InstancesBody ?? WorkflowAnswer ?? WorkflowAnswers.Instances(query);
        return InstancesRewrite is { } rewrite ? answer with { Body = rewrite(answer.Body) } : answer;
    }

    /// <summary>When set, the body of every instance-list answer passes through it (a test turns the golden's rows into the line workflow's).</summary>
    public Func<string, string>? InstancesRewrite { get; set; }

    /// <summary>What GET /workflow/instances/{id} answers.</summary>
    /// <param name="id">The id.</param>
    /// <returns>The answer.</returns>
    internal RouteAnswer InstanceAnswer(string id) => InstanceBody?.Invoke(id) ?? WorkflowAnswer ?? WorkflowAnswers.Instance(id);

    /// <summary>What GET /workflow/instances/{id}/facts/{key} answers.</summary>
    /// <param name="id">The id.</param>
    /// <param name="key">The key.</param>
    /// <returns>The answer.</returns>
    internal RouteAnswer FactAnswer(string id, string key) => WorkflowAnswer ?? WorkflowAnswers.Fact(id, key);

    /// <summary>What GET /workflow/recipes/{recipe} answers.</summary>
    /// <param name="recipe">The recipe id.</param>
    /// <param name="hash">The hash named, or null.</param>
    /// <returns>The answer.</returns>
    internal RouteAnswer RecipeAnswer(string recipe, string? hash) => WorkflowAnswer ?? WorkflowAnswers.Recipe(recipe, hash);

    // LANE ANCHOR U3': U3' keeps its own fake in FakeSindriApi.Routes.cs; it adds nothing here. Insert immediately above this marker, never between the markers.
    // LANE ANCHOR U8: new workflow answers (commands, complete) and their gates, answered from WorkflowAnswers. Insert immediately below this marker, never between the markers.

    /// <summary>The id of the latest instance read; null before the first.</summary>
    public string? LastInstanceId { get; private set; }

    /// <summary>The query of the latest instance-list read; null before the first.</summary>
    public InstanceQuery? LastInstancesQuery { get; private set; }
}
