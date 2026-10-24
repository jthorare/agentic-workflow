using System.Text;
using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.ReadInjection;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.GetInstance;

namespace Aw.Sindri.Tests.ReadInjection;

/// <summary>'ReadInjection' (plan S3): the cached text of the open run, a refill on a miss that must match the record (SND-WF-017 otherwise), no open run (SND-WF-005), and the fact route's scope rule.</summary>
[Lane(TestLane.Unit)]
public sealed class ReadInjectionServiceTests
{
    private const string Item = "ITM-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string Run = "1.1";
    private static readonly BlockId _input = BlockId.From("input.test.a");
    private static readonly BlockId _step = BlockId.From("step.test");

    private sealed class Sources(InjectionSource source) : IInjectionSources
    {
        public ValueTask<Result<InjectionSource>> Read(InstanceAddress address, CancellationToken cancellationToken) => ValueTask.FromResult(Results.Success(source));
    }

    private sealed class Runner(Func<string> text) : IStepInputRunner
    {
        public int Calls { get; private set; }

        public StepInputContext? Seen { get; private set; }

        public ValueTask<Result<FilledOutput>> Fill(StepInputContext context, int index, CancellationToken cancellationToken)
        {
            Calls++;
            Seen = context;
            return ValueTask.FromResult(Results.Success(Output(text())));
        }
    }

    private static FilledOutput Output(string text) => new(
        new FilledInput(_input, 1, [Entry(text)]),
        new FilledTexts(new Dictionary<string, string>(StringComparer.Ordinal) { ["notes"] = text }));

    private static InjectionEntry Entry(string text) => new("notes", _input, Encoding.UTF8.GetByteCount(text), StepInputRunner.Sha(text));

    private static StepInputContext Context => new(_input, _step, new Dictionary<string, JsonElement>(StringComparer.Ordinal), new Dictionary<FactKey, JsonElement>(), RepoName.From("alpha"));

    private static InstanceView Open(string recorded, bool openRun = true)
    {
        var view = StatusCases.View("desk-task") with { ItemId = ItemKey.From(Item) };
        return openRun ? view with { Task = view.Task! with { Injection = [Entry(recorded)] } } : view with { Run = null, Task = null };
    }

    private static async Task<(Result<InjectionView> Result, MemoryInjectionTexts Texts, Runner Runner)> Read(InstanceView view, string recorded, Func<string> refill, string? section = null, CallerContext? caller = null, bool cached = false)
    {
        var texts = new MemoryInjectionTexts();
        var source = new InjectionSource("checks", Run, [new InjectionInputSource(Output(recorded).Metadata, Context)]);
        if (cached)
        {
            texts.Put(new InjectionKey(view.Address.Value, "checks", Run, 1), Output(recorded).Texts);
        }

        var runner = new Runner(refill);
        var instances = new SomeInstances(view);
        var service = new ReadInjectionService(instances, instances, new Sources(source), texts, runner, new ListInstances.FixedCaller(caller ?? new CallerContextBuilder().Human().Build()));
        return (await service.Handle(new ReadInjectionRequest(view.Id, section), TestContext.Current.CancellationToken), texts, runner);
    }

    [Fact]
    public async Task ReadInjection_serves_cached_text_for_the_open_run()
    {
        var (result, _, runner) = await Read(Open("kept text"), "kept text", () => throw new InvalidOperationException("a hit must not run the input"), cached: true);

        var section = Assert.Single(result.Value.Sections);
        Assert.Equal((Run, "notes", "input.test.a", "kept text", 9), (result.Value.Run, section.Section, section.Input, section.Text, section.Bytes));
        Assert.Equal(0, runner.Calls);
    }

    [Fact]
    public async Task ReadInjection_refills_on_miss_and_serves_when_sha_matches()
    {
        var (result, texts, runner) = await Read(Open("same"), "same", () => "same");

        Assert.Equal("same", Assert.Single(result.Value.Sections).Text);
        Assert.Equal(1, runner.Calls);
        Assert.NotNull(texts.Get(new InjectionKey(Open("same").Address.Value, "checks", Run, 1)));   // the refill is kept
    }

    [Fact]
    public async Task ReadInjection_refuses_SND_WF_017_when_refill_differs_and_serves_nothing()
    {
        var (result, texts, _) = await Read(Open("before"), "before", () => "after");

        Assert.Equal(WorkflowErrors.InjectionChanged.Code, result.Error.Code);
        Assert.Null(texts.Get(new InjectionKey(Open("before").Address.Value, "checks", Run, 1)));
        Assert.DoesNotContain("after", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReadInjection_serves_both_entries_of_the_same_input_listed_twice_each_with_its_own_text()
    {
        var first = new FilledInput(_input, 1, [new InjectionEntry("notes", _input, 5, StepInputRunner.Sha("first"))]);
        var second = new FilledInput(_input, 2, [new InjectionEntry("extra", _input, 6, StepInputRunner.Sha("second"))]);
        var view = StatusCases.View("desk-task") with { ItemId = ItemKey.From(Item) };
        view = view with { Task = view.Task! with { Injection = [.. first.Sections, .. second.Sections] } };
        var source = new InjectionSource("checks", Run, [new InjectionInputSource(first, Context), new InjectionInputSource(second, Context)]);
        var runner = new ByIndexRunner(new Dictionary<int, (string Section, string Text)> { [1] = ("notes", "first"), [2] = ("extra", "second") });
        var texts = new MemoryInjectionTexts();
        var instances = new SomeInstances(view);
        var service = new ReadInjectionService(instances, instances, new Sources(source), texts, runner, new ListInstances.FixedCaller(new CallerContextBuilder().Human().Build()));

        var result = await service.Handle(new ReadInjectionRequest(view.Id), TestContext.Current.CancellationToken);

        Assert.Equal([("notes", "first"), ("extra", "second")], result.Value.Sections.Select(static s => (s.Section, s.Text)));
        Assert.Equal(2, runner.Calls);
        Assert.Equal("first", texts.Get(new InjectionKey(view.Address.Value, "checks", Run, 1))!.Sections["notes"]);
        Assert.Equal("second", texts.Get(new InjectionKey(view.Address.Value, "checks", Run, 2))!.Sections["extra"]);
    }

    private sealed class ByIndexRunner(Dictionary<int, (string Section, string Text)> fills) : IStepInputRunner
    {
        public int Calls { get; private set; }

        public ValueTask<Result<FilledOutput>> Fill(StepInputContext context, int index, CancellationToken cancellationToken)
        {
            Calls++;
            var (section, text) = fills[index];
            var entry = new InjectionEntry(section, _input, Encoding.UTF8.GetByteCount(text), StepInputRunner.Sha(text));
            return ValueTask.FromResult(Results.Success(new FilledOutput(
                new FilledInput(_input, index, [entry]),
                new FilledTexts(new Dictionary<string, string>(StringComparer.Ordinal) { [section] = text }))));
        }
    }

    [Fact]
    public async Task ReadInjection_without_open_run_is_SND_WF_005()
    {
        var (result, _, _) = await Read(Open("x", openRun: false), "x", () => "x");

        Assert.Equal(WorkflowErrors.StaleRunToken.Code, result.Error.Code);
    }

    [Fact]
    public async Task ReadInjection_one_section_and_an_unknown_one()
    {
        var (one, _, _) = await Read(Open("kept"), "kept", () => "kept", "notes", cached: true);
        var (none, _, _) = await Read(Open("kept"), "kept", () => "kept", "nope", cached: true);

        Assert.Equal("kept", Assert.Single(one.Value.Sections).Text);
        Assert.Equal(SindriErrors.ItemNotFound.Code, none.Error.Code);
    }

    [Fact]
    public async Task A_session_reads_any_repos_injection_and_its_own_repo_rides_on_the_refill()
    {
        var (other, _, _) = await Read(Open("kept"), "kept", () => "kept", caller: new CallerContextBuilder().Session().InRepo("beta").Build());
        var (own, _, runner) = await Read(Open("kept"), "kept", () => "kept", caller: new CallerContextBuilder().Session().InRepo("alpha").Build());

        Assert.True(other.IsSuccess);
        Assert.True(own.IsSuccess);
        Assert.Equal(RepoName.From("alpha"), runner.Seen!.Scope);
    }
}
