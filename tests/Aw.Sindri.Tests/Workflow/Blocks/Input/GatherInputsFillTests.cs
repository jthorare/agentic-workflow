using System.Text.Json;
using Aw.Context.Core.Code;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core.History;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Map;
using Aw.Context.Core.ScopeGraph;
using Aw.Context.Core.Search;
using Aw.Context.Core.Subgraph;
using Aw.Context.Core.WholeMap;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Host;
using Aw.Sindri.Tests.ContextCalls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Workflow.Blocks.Input;

/// <summary>
/// The scoping recipe's gather Step names all three context inputs. Through the shipped composition and the real runner, each one fills its
/// section and its typed result for the instance's repo (the facades are the only stubs), so the Step sees what the recipe promises.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class GatherInputsFillTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "aw-gather-inputs-" + RealTime.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private Combined _combined = new(a => ContextInputFixtures.SmallSet([.. a]));

    private ServiceProvider Compose() => Composition.Compose(new ServiceCollection(), name => name == "AW_STATE_DIR" ? _root : null, new FakeTimeProvider())
        .AddScoped(_ => EngineProxy.Of(_combined))
        .AddSingleton<IApprovedProfile>(new ContextInputFixtures.Profiles(ContextInputFixtures.Profile()))
        .BuildServiceProvider();

    /// <summary>One stub answering all three lenses.</summary>
    private sealed class Combined(Func<IReadOnlyList<string>, Result<LensesSetView>> lenses) : IContextLenses, IContextHistory, IContextCode
    {
        public ContextLensesInputTests.StubLenses Lenses_ { get; } = new(lenses);

        public ContextHistoryInputTests.StubHistory History_ { get; } = new(() => ContextHistoryTextTests.View());

        public ContextCodeInputTests.StubCode Code_ { get; } = new(() => ContextCodeVerbsTests.View());

        private ContextLensesInputTests.StubLenses _lenses => Lenses_;

        private ContextHistoryInputTests.StubHistory _history => History_;

        private ContextCodeInputTests.StubCode _code => Code_;

        public ValueTask<Result<LensesSetView>> LensesOf(IReadOnlyList<string> anchors, MapRead read, LensRange range, ContextScope scope, CancellationToken cancellationToken) => _lenses.LensesOf(anchors, read, range, scope, cancellationToken);

        public ValueTask<Result<LensesSetView>> LensesOf(IReadOnlyList<string> anchors, MapRead read, int? depth, ContextScope scope, CancellationToken cancellationToken) => _lenses.LensesOf(anchors, read, depth, scope, cancellationToken);

        public ValueTask<Result<SubgraphView>> Subgraph(SubgraphQuery query, ContextScope scope, CancellationToken cancellationToken) => _lenses.Subgraph(query, scope, cancellationToken);

        public ValueTask<Result<LensesView>> Lenses(string anchor, bool draft, int? depth, ContextScope scope, CancellationToken cancellationToken) => _lenses.Lenses(anchor, draft, depth, scope, cancellationToken);

        public ValueTask<Result<LensesView>> Lenses(string anchor, MapRead read, int? depth, ContextScope scope, CancellationToken cancellationToken) => _lenses.Lenses(anchor, read, depth, scope, cancellationToken);

        public ValueTask<Result<LensesView>> Lenses(string anchor, MapRead read, LensRange range, ContextScope scope, CancellationToken cancellationToken) => _lenses.Lenses(anchor, read, range, scope, cancellationToken);

        public ValueTask<Result<ScopeGraphView>> ScopeGraph(ScopeGraphQuery query, ContextScope scope, CancellationToken cancellationToken) => _lenses.ScopeGraph(query, scope, cancellationToken);

        public ValueTask<Result<WholeMapView>> WholeMap(WholeMapQuery query, ContextScope scope, CancellationToken cancellationToken) => _lenses.WholeMap(query, scope, cancellationToken);

        public ValueTask<Result<MapSearchView>> Search(MapSearchQuery query, ContextScope scope, CancellationToken cancellationToken) => _lenses.Search(query, scope, cancellationToken);

        public ValueTask<Result<FileMapView>> FileMap(FileMapQuery query, ContextScope scope, CancellationToken cancellationToken) => _lenses.FileMap(query, scope, cancellationToken);

        public ValueTask<Result<HistoryView>> History(IReadOnlyList<string> anchors, HistoryWindow window, MapRead read, ContextScope scope, CancellationToken cancellationToken) => _history.History(anchors, window, read, scope, cancellationToken);

        public ValueTask<Result<CodeView>> Code(IReadOnlyList<string> anchors, bool draft, string? mapVersion, ContextScope scope, CancellationToken cancellationToken) => _code.Code(anchors, draft, mapVersion, scope, cancellationToken);
    }

    private static readonly string[] _anchors = ["feature:sharing"];

    private static readonly string[] _hops = ["state", "flow", "dependency", "component"];

    private static StepInputContext Context(string input) => new(
        BlockId.From(input), BlockId.From("step.scope.gather"),
        new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            ["anchors"] = JsonSerializer.SerializeToElement(_anchors),
            ["depth"] = JsonSerializer.SerializeToElement(2),
            ["hops"] = JsonSerializer.SerializeToElement(_hops),
            ["window"] = JsonSerializer.SerializeToElement(new { since = "2026-01-01" }),
        },
        new Dictionary<FactKey, JsonElement>(), ContextInputFixtures.Web);

    [Theory]
    [InlineData("input.context.lenses", "lenses")]
    [InlineData("input.context.history", "history")]
    [InlineData("input.context.code", "code")]
    public async Task Each_gather_input_fills_its_section_and_typed_result_for_the_instances_repo(string input, string section)
    {
        await using var provider = Compose();
        var runner = provider.GetRequiredService<IStepInputRunner>();

        var filled = await runner.Fill(Context(input), 1, TestContext.Current.CancellationToken);

        Assert.False(filled.IsError, filled.IsError ? filled.Error.Message : string.Empty);
        Assert.False(string.IsNullOrWhiteSpace(filled.Value.Texts.Sections[section]));
        Assert.True(filled.Value.Texts.Result.HasValue);
        Assert.True(filled.Value.Metadata.Result is not null);
    }

    [Fact]
    public async Task The_inputs_read_the_instances_repo_and_pass_the_recipes_depth_hops_and_window()
    {
        await using var provider = Compose();
        var runner = provider.GetRequiredService<IStepInputRunner>();

        foreach (var input in new[] { "input.context.lenses", "input.context.history", "input.context.code" })
        {
            _ = await runner.Fill(Context(input), 1, TestContext.Current.CancellationToken);
        }

        var (_, range, lensScope) = Assert.Single(_combined.Lenses_.Calls);
        Assert.Equal(2, range.Depth);
        Assert.Equal("state,flow,dependency,component", range.Hops);
        Assert.Equal("web", Assert.Single(lensScope.Repos).Name.Value);
        var (_, window, historyScope) = Assert.Single(_combined.History_.Calls);
        Assert.Equal(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), window.Since);
        Assert.Equal("web", Assert.Single(historyScope.Repos).Name.Value);
        var (_, _, _, codeScope) = Assert.Single(_combined.Code_.Calls);
        Assert.Equal("web", Assert.Single(codeScope.Repos).Name.Value);
    }

    [Fact]
    public async Task A_huge_map_fills_whole_and_the_Step_does_not_fail()
    {
        _combined = new Combined(a => ContextInputFixtures.SmallSet([.. Enumerable.Range(0, 20000).Select(i => "feature:f" + i.ToString(System.Globalization.CultureInfo.InvariantCulture))]));
        await using var provider = Compose();
        var runner = provider.GetRequiredService<IStepInputRunner>();

        var filled = await runner.Fill(Context("input.context.lenses"), 1, TestContext.Current.CancellationToken);

        Assert.False(filled.IsError, filled.IsError ? filled.Error.Message : string.Empty);
        Assert.DoesNotContain("more lines not shown", filled.Value.Texts.Sections["lenses"], StringComparison.Ordinal);
        Assert.Equal(20000, filled.Value.Texts.Result!.Value.GetProperty("anchors").GetArrayLength());
    }

    [Fact]
    public async Task The_shipped_runtimes_cover_every_input_of_the_scoping_recipe()
    {
        await using var provider = Compose();

        var runtimes = provider.GetRequiredService<StepInputRuntimes>();

        Assert.True(runtimes.Has(BlockId.From("input.context.lenses")));
        Assert.True(runtimes.Has(BlockId.From("input.context.history")));
        Assert.True(runtimes.Has(BlockId.From("input.context.code")));
    }

    /// <summary>The argument as the engine hands it over for '{ fact: fact:anchors }': the stored reference, not the content.</summary>
    private static JsonElement Reference(FactValue fact) => JsonSerializer.SerializeToElement(new { @ref = fact.Ref, sha256 = fact.Sha256, headSha = fact.HeadSha });

    private static StepInputContext FactContext(string input, FactValue anchors) => new(
        BlockId.From(input), BlockId.From("step.scope.gather"),
        new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["anchors"] = Reference(anchors) },
        new Dictionary<FactKey, JsonElement>(), ContextInputFixtures.Web);

    private static async Task<FactValue> Store(ServiceProvider provider, string content) =>
        (await provider.GetRequiredService<IFactStore>().Put(System.Text.Encoding.UTF8.GetBytes(content), TestContext.Current.CancellationToken)).Value;

    [Theory]
    [InlineData("input.context.lenses", "lenses", "[\"feature:sharing\"]")]
    [InlineData("input.context.history", "history", "[\"feature:sharing\"]")]
    [InlineData("input.context.code", "code", "{\"anchors\":[\"feature:sharing\"]}")]
    [InlineData("input.context.code", "code", "feature:sharing")]
    public async Task The_shipped_recipes_fact_anchors_argument_is_read_from_the_stored_fact_and_fills_the_section(string input, string section, string content)
    {
        await using var provider = Compose();
        var fact = await Store(provider, content);

        var filled = await provider.GetRequiredService<IStepInputRunner>().Fill(FactContext(input, fact), 1, TestContext.Current.CancellationToken);

        Assert.False(filled.IsError, filled.IsError ? filled.Error.Message : string.Empty);
        Assert.DoesNotContain("are unavailable for this Step", filled.Value.Texts.Sections[section], StringComparison.Ordinal);
        Assert.True(filled.Value.Texts.Result.HasValue);
        IReadOnlyList<IReadOnlyList<string>> calls = input switch
        {
            "input.context.lenses" => [.. _combined.Lenses_.Calls.Select(c => c.Anchors)],
            "input.context.history" => [.. _combined.History_.Calls.Select(c => c.Anchors)],
            _ => [.. _combined.Code_.Calls.Select(c => c.Anchors)],
        };
        Assert.Equal(["feature:sharing"], Assert.Single(calls));
    }

    [Fact]
    public async Task A_fact_anchors_argument_that_is_not_stored_is_an_unavailable_section_naming_the_fact()
    {
        await using var provider = Compose();
        var missing = new FactValue("fact:sha256:" + new string('a', 64), new string('a', 64), HeadSha: null);

        var filled = await provider.GetRequiredService<IStepInputRunner>().Fill(FactContext("input.context.lenses", missing), 1, TestContext.Current.CancellationToken);

        Assert.False(filled.IsError);
        Assert.Contains("unavailable", filled.Value.Texts.Sections["lenses"], StringComparison.Ordinal);
        Assert.Contains(missing.Ref, filled.Value.Texts.Sections["lenses"], StringComparison.Ordinal);
        Assert.False(filled.Value.Texts.Result.HasValue);
    }

    [Fact]
    public async Task A_stored_fact_whose_content_no_longer_matches_its_sha_fails_the_fill_with_the_input_changed_error()
    {
        await using var provider = Compose();
        var fact = await Store(provider, "[\"feature:sharing\"]");
        var file = Directory.EnumerateFiles(_root, fact.Sha256, SearchOption.AllDirectories).Single();
        await File.WriteAllTextAsync(file, "[\"feature:other\"]", TestContext.Current.CancellationToken);

        var filled = await provider.GetRequiredService<IStepInputRunner>().Fill(FactContext("input.context.lenses", fact), 1, TestContext.Current.CancellationToken);

        Assert.True(filled.IsError);
        Assert.Equal("SND-WF-018", filled.Error.Code);
        Assert.Empty(_combined.Lenses_.Calls);
    }
}
