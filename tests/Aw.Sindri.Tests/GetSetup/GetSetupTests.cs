using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Features.GetSetup;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Server;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.GetSetup;

/// <summary>GET /setup: the six steps in their fixed order, each from its own source, and a source that can't be read fails only its own step.</summary>
public sealed class GetSetupTests
{
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 19, 9, 30, 0, TimeSpan.Zero));

    private readonly ListLogger<GetSetupService> _log = new();

    private GetSetupService Service(InMemorySetupFacts facts) => new(facts, new ProjectorProgress(_clock), _log);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public async Task GetSetup_reports_every_step_in_order()
    {
        var service = Service(InMemorySetupFacts.AllDone());

        var result = await service.Handle(A.GetSetupRequest().Build(), Token);

        Assert.True(result.IsSuccess);
        Assert.Equal([SetupStepId.Repo, SetupStepId.Profile, SetupStepId.Recipes, SetupStepId.Mcp, SetupStepId.Map, SetupStepId.Onyx], result.Value.Steps.Select(static s => s.Id));
        Assert.All(result.Value.Steps, static s => Assert.Equal(SetupState.Done, s.State));
        Assert.Equal(new ProjectorProgress(_clock).Current.AsOf, result.Value.AsOf);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Titles_are_the_fixed_words_and_the_steps_that_count_repos_carry_a_line_each()
    {
        var steps = (await Service(InMemorySetupFacts.AllDone()).Handle(A.GetSetupRequest().Build(), Token)).Value.Steps;

        Assert.Equal(["Add your repo", "Approve the profile", "Approve the workflows", "Connect your agents", "Build the map", "Set up Onyx"], steps.Select(static s => s.Title), StringComparer.Ordinal);
        Assert.Equal([1, 0, 0, 1, 1, 0], steps.Select(static s => s.Items.Count));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_fresh_box_asks_for_a_repo_and_everything_else_waits()
    {
        var steps = (await Service(new InMemorySetupFacts { RecipesAnswer = new RecipesFact(26, 0) }).Handle(A.GetSetupRequest().Build(), Token)).Value.Steps;

        Assert.Equal([SetupState.NeedsYou, SetupState.Todo, SetupState.Todo, SetupState.Todo, SetupState.Todo, SetupState.Todo], steps.Select(static s => s.State));
        Assert.Equal([SetupActions.AddRepo, null, null, null, null, SetupActions.SetupOnyx], steps.Select(static s => s.Action), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_source_that_throws_fails_only_its_own_step_and_the_answer_is_still_served()
    {
        var facts = new ThrowingOnyx(InMemorySetupFacts.AllDone());

        var result = await new GetSetupService(facts, new ProjectorProgress(_clock), _log).Handle(A.GetSetupRequest().Build(), Token);

        Assert.True(result.IsSuccess);
        var failed = Assert.Single(result.Value.Steps, static s => s.State == SetupState.Failed);
        Assert.Equal((SetupStepId.Onyx, SindriErrors.ApiInternal.Code), (failed.Id, failed.Error?.Code));
        Assert.Equal(5, result.Value.Steps.Count(static s => s.State == SetupState.Done));
        var line = Assert.Single(_log.Lines);
        Assert.Contains("onyx", line, StringComparison.Ordinal);
        Assert.Contains(nameof(InvalidOperationException), line, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_source_cancelled_by_its_own_timeout_fails_only_its_step_while_the_request_is_live()
    {
        var facts = new ThrowingOnyx(InMemorySetupFacts.AllDone(), new TaskCanceledException("the probe timed out"));

        var result = await new GetSetupService(facts, new ProjectorProgress(_clock), _log).Handle(A.GetSetupRequest().Build(), Token);

        Assert.True(result.IsSuccess);
        var failed = Assert.Single(result.Value.Steps, static s => s.State == SetupState.Failed);
        Assert.Equal((SetupStepId.Onyx, SindriErrors.ApiInternal.Code), (failed.Id, failed.Error?.Code));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_source_cancelled_while_the_request_itself_is_cancelled_propagates()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var facts = new ThrowingOnyx(InMemorySetupFacts.AllDone(), new OperationCanceledException(cts.Token));

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await new GetSetupService(facts, new ProjectorProgress(_clock), _log).Handle(A.GetSetupRequest().Build(), cts.Token));
    }

    private sealed class ThrowingOnyx(ISetupFacts inner, Exception? failure = null) : ISetupFacts
    {
        public ValueTask<Result<RepoFact>> Repos(CancellationToken cancellationToken) => inner.Repos(cancellationToken);

        public ValueTask<Result<ProfileFact>> Profile(CancellationToken cancellationToken) => inner.Profile(cancellationToken);

        public ValueTask<Result<RecipesFact>> Recipes(CancellationToken cancellationToken) => inner.Recipes(cancellationToken);

        public ValueTask<Result<McpFact>> Mcp(CancellationToken cancellationToken) => inner.Mcp(cancellationToken);

        public ValueTask<Result<MapFact>> Map(CancellationToken cancellationToken) => inner.Map(cancellationToken);

        public ValueTask<Result<OnyxFact>> Onyx(CancellationToken cancellationToken) => throw failure ?? new InvalidOperationException("docker would not start");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_source_that_cannot_be_read_fails_only_its_own_step_and_names_its_code()
    {
        var facts = InMemorySetupFacts.AllDone();
        facts.RecipesAnswer = SindriErrors.LedgerUnavailable.With("the ledger is locked");

        var steps = (await Service(facts).Handle(A.GetSetupRequest().Build(), Token)).Value.Steps;

        var failed = Assert.Single(steps, static s => s.State == SetupState.Failed);
        Assert.Equal(SetupStepId.Recipes, failed.Id);
        Assert.Equal(SindriErrors.LedgerUnavailable.Code, failed.Error?.Code);
        Assert.DoesNotContain(SindriErrors.LedgerUnavailable.Code, failed.Detail, StringComparison.Ordinal);
        Assert.All(steps.Where(static s => s.Id != SetupStepId.Recipes), static s => Assert.Equal(SetupState.Done, s.State));
    }
}
