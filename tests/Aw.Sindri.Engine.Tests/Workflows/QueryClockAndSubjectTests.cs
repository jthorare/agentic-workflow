using System.Runtime.Versioning;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.DependencyInjection;
using Temporalio.Worker;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// On the pinned dev server: the Query carries instants, not ages (the composers measure to the caller's clock, so an age
/// moves between two reads of an idle workflow), and the workflow names the subject from the first input's stored content on
/// start, so the 'Subject' search attribute and the view's subject agree (API spec §3.8 'subject', 'status').
/// </summary>
[Lane(TestLane.Integration)]
public sealed class QueryClockAndSubjectTests : IDisposable
{
    private readonly WorkflowsHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    private async Task<InstanceStart> StartWith(IServiceProvider sindri, string item, string content, CancellationToken ct)
    {
        var started = await WorkflowsHarness.StepStart(sindri, item, ct);
        return started with { Inputs = new Dictionary<FactKey, InputFact> { [FactKey.From("fact:spec")] = new("notes.txt", _harness.Fact(content)) } };
    }

    [Fact(Timeout = 120_000)]
    public async Task Two_reads_of_an_idle_instance_differ_in_age_and_timeout_because_the_composer_measures_to_the_callers_clock()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var env = await WorkflowsHarness.StartDevServer(ct: ct);
        await using var served = await ApplyCommand.Served.Start(_harness.Home, env, ct);
        var handle = await WorkflowsHarness.Start(env.Client, await StartWith(served.Sindri, "T-1", "Fix the crash on launch", ct));
        _ = await WorkflowsHarness.Until(handle, static v => v.Task is not null);

        var first = await handle.QueryAsync(static wf => wf.GetInstance());
        var firstRead = RealTime.NowOffset;
        await Task.Delay(TimeSpan.FromSeconds(2.2), ct);
        var second = await handle.QueryAsync(static wf => wf.GetInstance());
        var secondRead = RealTime.NowOffset;

        var before = InstanceViews.Status(first, firstRead);
        var after = InstanceViews.Status(second, secondRead);
        Assert.Equal((first.Since, first.Task!.TimeoutAt), (second.Since, second.Task!.TimeoutAt));   // the view holds instants, and they did not move
        Assert.True(after.Header.AgeSeconds >= before.Header.AgeSeconds + 2, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"age {before.Header.AgeSeconds} then {after.Header.AgeSeconds}"));
        Assert.True(before.Timeout!.InSeconds - after.Timeout!.InSeconds >= 2, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"timeout {before.Timeout.InSeconds} then {after.Timeout.InSeconds}"));
        await handle.TerminateAsync("test over");
    }

    [Fact(Timeout = 120_000)]
    public async Task The_subject_is_named_from_the_stored_content_and_the_search_attribute_is_the_same_key()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var env = await WorkflowsHarness.StartDevServer(ct: ct);
        await using var served = await ApplyCommand.Served.Start(_harness.Home, env, ct);
        var handle = await WorkflowsHarness.Start(env.Client, await StartWith(served.Sindri, "T-1", "Fix the crash on launch of the tab manager when two windows are open\nsecond line", ct));

        var view = await WorkflowsHarness.Until(handle, static v => v.Task is not null);

        const string line = "Fix the crash on launch of the tab manager when two windows are open";
        var key = line[..40];
        Assert.Equal(new SubjectRef("text", key, line, Href: null), view.Subject);
        Assert.Equal(key, (await handle.DescribeAsync()).TypedSearchAttributes.Get(SindriTemporal.SubjectKey));
        await handle.TerminateAsync("test over");
    }

    [Fact(Timeout = 120_000)]
    public async Task A_first_input_whose_fact_is_not_stored_still_starts_with_the_subject_keyed_by_the_fact_key()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var env = await WorkflowsHarness.StartDevServer(ct: ct);
        await using var served = await ApplyCommand.Served.Start(_harness.Home, env, ct);
        var handle = await WorkflowsHarness.Start(env.Client, await WorkflowsHarness.StepStart(served.Sindri, "T-1", ct));   // its 'fact:spec' content is not in the store

        var view = await WorkflowsHarness.Until(handle, static v => v.Task is not null);

        Assert.Equal(new SubjectRef("text", "fact:spec", "spec.md", Href: null), view.Subject);
        Assert.Equal("fact:spec", (await handle.DescribeAsync()).TypedSearchAttributes.Get(SindriTemporal.SubjectKey));
        await handle.TerminateAsync("test over");
    }

    [UnsupportedOSPlatform("windows")]
    [Fact(Timeout = 120_000)]
    public async Task A_first_input_whose_stored_content_cannot_be_opened_still_starts_with_the_subject_keyed_by_the_fact_key()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows() || Environment.UserName == "root", "needs a file the current user is refused (a root user opens any)");
        var ct = TestContext.Current.CancellationToken;
        var fact = _harness.Fact("Fix the crash on launch");
        var stored = Path.Combine(_harness.Home, "workflow", "facts", fact.Sha256);   // a directory there reads as 'not stored' (File.Exists is false): only a file the open is refused makes the activity throw
        File.SetUnixFileMode(stored, UnixFileMode.None);
        await using var env = await WorkflowsHarness.StartDevServer(ct: ct);
        await using var served = await ApplyCommand.Served.Start(_harness.Home, env, ct);
        var started = await WorkflowsHarness.StepStart(served.Sindri, "T-1", ct);
        var handle = await WorkflowsHarness.Start(env.Client, started with { Inputs = new Dictionary<FactKey, InputFact> { [FactKey.From("fact:spec")] = new("notes.txt", fact) } });

        var view = await WorkflowsHarness.Until(handle, static v => v.Task is not null);

        Assert.Equal(new SubjectRef("text", "fact:spec", "notes.txt", Href: null), view.Subject);
        Assert.Equal("fact:spec", (await handle.DescribeAsync()).TypedSearchAttributes.Get(SindriTemporal.SubjectKey));
        await handle.TerminateAsync("test over");
    }

    private const string Named = "schema: 1\nid: workflow.named\nversion: 1\nname: \"{fact:scope} \u00b7 {start:date}\"\n"
        + "inputs:\n"
        + "  - { key: fact:scope, label: Scope, type: text, kind: text }\n"
        + "  - { key: fact:run, type: text, kind: text, generate: \"run {start:date}\" }\n"
        + "initial: produce\nstates:\n"
        + "  produce: { kind: active, step: step.produce, timeout: { after: 4h, then: park }, next: \"Produce the output.\" }\n"
        + "  done: { kind: terminal, outcome: resolved }\n"
        + "transitions:\n"
        + "  - { from: produce, to: done, on: step.completed, when: { outcome: done } }\n";

    [Fact(Timeout = 120_000)]
    public async Task A_recipe_name_is_rendered_once_at_start_as_the_subject_label_and_a_generated_input_is_produced_by_the_engine()
    {
        var ct = TestContext.Current.CancellationToken;
        _harness.Recipe("workflow.named", Named);
        await using var env = await WorkflowsHarness.StartDevServer(ct: ct);
        await using var served = await ApplyCommand.Served.Start(_harness.Home, env, ct);
        var start = await WorkflowsHarness.RecipeStart(served.Sindri, "workflow.named", "T-1", ct);
        var handle = await WorkflowsHarness.Start(env.Client, start with
        {
            Inputs = new Dictionary<FactKey, InputFact>
            {
                [FactKey.From("fact:scope")] = new("scope.txt", _harness.Fact("Crash on launch\nsecond line")),
                [FactKey.From("fact:run")] = new("generated", _harness.Fact("run 2026-10-15"), Generated: true),
            },
        });

        var view = await WorkflowsHarness.Until(handle, static v => v.Task is not null);

        Assert.Equal("text", view.Subject!.Kind);
        Assert.Equal("Crash on launch", view.Subject.Key);
        Assert.Matches(@"^Crash on launch \u00b7 \d{4}-\d\d-\d\d$", view.Subject.Label);
        Assert.Equal(view.Subject.Label, view.Name);
        Assert.Equal("engine:generate", view.Facts.Single(static f => f.Key == "fact:run").By);
        Assert.Equal([("fact:scope", true), ("fact:run", false)], view.Inputs.Select(static i => (i.Key, i.Required)));
        await handle.TerminateAsync("test over");
    }

    [Fact(Timeout = 120_000)]
    public async Task The_started_row_records_the_rendered_subject_and_each_facts_producer()
    {
        var ct = TestContext.Current.CancellationToken;
        _harness.Recipe("workflow.named", Named);
        await using var env = await WorkflowsHarness.StartDevServer(ct: ct);
        await using var served = await ApplyCommand.Served.Start(_harness.Home, env, ct);
        var start = await WorkflowsHarness.RecipeStart(served.Sindri, "workflow.named", "T-1", ct);
        var handle = await WorkflowsHarness.Start(env.Client, start with
        {
            Inputs = new Dictionary<FactKey, InputFact>
            {
                [FactKey.From("fact:scope")] = new("scope.txt", _harness.Fact("Crash on launch")),
                [FactKey.From("fact:run")] = new("generated", _harness.Fact("run 2026-10-15"), Generated: true),
            },
        });
        var view = await WorkflowsHarness.Until(handle, static v => v.Task is not null);

        var delta = await StartedDelta(served.Sindri, ct);

        var subject = delta.GetProperty("subject");
        Assert.Equal(("text", "Crash on launch", view.Subject!.Label), (subject.GetProperty("kind").GetString(), subject.GetProperty("key").GetString(), subject.GetProperty("label").GetString()));
        var producers = delta.GetProperty("producers");
        Assert.Equal("engine:generate", producers.GetProperty("fact:run").GetString());
        Assert.Equal("human:desk", producers.GetProperty("fact:scope").GetString());   // a supplied fact is the trigger's actor, which the start's trigger names
        await handle.TerminateAsync("test over");
    }

    [Fact(Timeout = 120_000)]
    public async Task The_name_reads_the_start_time_the_instance_was_stamped_with_not_the_workflows_own_clock()
    {
        var ct = TestContext.Current.CancellationToken;
        _harness.Recipe("workflow.named", Named);
        await using var env = await WorkflowsHarness.StartDevServer(ct: ct);
        await using var served = await ApplyCommand.Served.Start(_harness.Home, env, ct);
        var start = await WorkflowsHarness.RecipeStart(served.Sindri, "workflow.named", "T-1", ct);
        var handle = await WorkflowsHarness.Start(env.Client, start with
        {
            StartedAt = new DateTimeOffset(2020, 1, 2, 3, 4, 0, TimeSpan.Zero),
            Inputs = new Dictionary<FactKey, InputFact>
            {
                [FactKey.From("fact:scope")] = new("scope.txt", _harness.Fact("Crash on launch")),
                [FactKey.From("fact:run")] = new("generated", _harness.Fact("run 2020-01-02"), Generated: true),
            },
        });

        var view = await WorkflowsHarness.Until(handle, static v => v.Task is not null);

        Assert.Equal("Crash on launch \u00b7 2020-01-02", view.Subject!.Label);
        await handle.TerminateAsync("test over");
    }

    [Fact(Timeout = 120_000)]
    public async Task A_named_instance_replays_cleanly_and_keeps_its_name_after_the_stored_fact_changed()
    {
        var ct = TestContext.Current.CancellationToken;
        _harness.Recipe("workflow.named", Named);
        await using var env = await WorkflowsHarness.StartDevServer(ct: ct);
        await using var served = await ApplyCommand.Served.Start(_harness.Home, env, ct);
        var start = await WorkflowsHarness.RecipeStart(served.Sindri, "workflow.named", "T-1", ct);
        var scope = _harness.Fact("Crash on launch");
        var handle = await WorkflowsHarness.Start(env.Client, start with
        {
            StartedAt = new DateTimeOffset(2026, 10, 15, 7, 5, 0, TimeSpan.Zero),
            Inputs = new Dictionary<FactKey, InputFact>
            {
                [FactKey.From("fact:scope")] = new("scope.txt", scope),
                [FactKey.From("fact:run")] = new("generated", _harness.Fact("run 2026-10-15"), Generated: true),
            },
        });
        var named = (await WorkflowsHarness.Until(handle, static v => v.Task is not null)).Subject!.Label;

        await File.WriteAllTextAsync(Path.Combine(_harness.Home, "workflow", "facts", scope.Sha256), "Something else entirely", ct);   // the stored scope changed under the instance
        await served.RestartWorker();   // a new worker holds no cached workflow: the query below replays the history, so it can't be answered from memory
        var later = await handle.QueryAsync(static wf => wf.GetInstance());
        var replayed = await new WorkflowReplayer(new WorkflowReplayerOptions().AddWorkflow<RecipeWorkflow>())
            .ReplayWorkflowAsync(await handle.FetchHistoryAsync(), throwOnReplayFailure: false, ct);

        Assert.Equal("Crash on launch \u00b7 2026-10-15", named);
        Assert.Equal(named, later.Subject!.Label);
        Assert.Null(replayed.ReplayFailure);
        await handle.TerminateAsync("test over");
    }

    private RegisteredRecipe RegisterLegacyStep() => _harness.RegisterLegacy(ShippedRecipes.Read("sindri/workflow.step.yaml")
        .Replace("id: workflow.step\n", "id: workflow.legacy\n", StringComparison.Ordinal)
        .Replace("inputs:\n  - { key: fact:spec, label: \"Spec\", type: text, kind: text }\n", "inputs: [fact:spec]\n", StringComparison.Ordinal));

    [Fact(Timeout = 120_000)]
    public async Task An_approved_row_from_before_input_kinds_loads_for_the_engine_with_the_defaults()
    {
        var legacy = RegisterLegacyStep();
        await using var sindri = _harness.Sindri();

        var found = await sindri.GetRequiredService<IApprovedRecipes>().Find(legacy.Id, legacy.Hash, TestContext.Current.CancellationToken);

        Assert.True(found.IsSuccess, found.IsError ? found.Error.ToString() : null);
        Assert.Null(found.Value.Recipe.Inputs[0].Kind);
    }

    [Fact(Timeout = 120_000)]
    public async Task A_legacy_instance_keeps_the_pre_P4c_command_sequence_so_its_history_still_replays()
    {
        var ct = TestContext.Current.CancellationToken;
        var legacy = RegisterLegacyStep();
        await using var env = await WorkflowsHarness.StartDevServer(ct: ct);
        await using var served = await ApplyCommand.Served.Start(_harness.Home, env, ct);
        var start = new InstanceStart(new RecipeVersion(legacy.Id, legacy.Version, legacy.Hash), Core.Launch.ItemKey.From("T-1"), Attempt: 1, new Dictionary<FactKey, InputFact> { [FactKey.From("fact:spec")] = new("notes.txt", _harness.Fact("Fix it")) }, Tenant: "local", Ring: 0) { StartedAt = RealTime.NowOffset };
        var handle = await WorkflowsHarness.Start(env.Client, start);
        _ = await WorkflowsHarness.Until(handle, static v => v.Task is not null);

        var history = await handle.FetchHistoryAsync();
        var read = history.Events.Count(static e => e.MarkerRecordedEventAttributes is { } marker
            && marker.Details.Values.Any(static v => v.Payloads_.Any(static p => p.Data.ToStringUtf8().Contains("ReadSubject", StringComparison.Ordinal))));

        var replayed = await new WorkflowReplayer(new WorkflowReplayerOptions().AddWorkflow<RecipeWorkflow>())
            .ReplayWorkflowAsync(history, throwOnReplayFailure: false, ct);

        Assert.Equal(1, read);   // a history recorded before P4c has this marker: skipping the call would fail its replay
        Assert.Null(replayed.ReplayFailure);
        await handle.TerminateAsync("test over");
    }

    [Fact(Timeout = 120_000)]
    public async Task The_started_row_records_the_address_when_the_rendered_name_is_empty()
    {
        var ct = TestContext.Current.CancellationToken;
        _harness.Recipe("workflow.named", Named.Replace("name: \"{fact:scope} \u00b7 {start:date}\"", $"name: \"{new string(' ', 85)}{{start:date}}\"", StringComparison.Ordinal));   // the cut to 80 characters leaves only spaces
        await using var env = await WorkflowsHarness.StartDevServer(ct: ct);
        await using var served = await ApplyCommand.Served.Start(_harness.Home, env, ct);
        var start = await WorkflowsHarness.RecipeStart(served.Sindri, "workflow.named", "T-1", ct);
        var handle = await WorkflowsHarness.Start(env.Client, start with
        {
            Inputs = new Dictionary<FactKey, InputFact>
            {
                [FactKey.From("fact:scope")] = new("scope.txt", _harness.Fact("Crash on launch")),
                [FactKey.From("fact:run")] = new("generated", _harness.Fact("run"), Generated: true),
            },
        });
        _ = await WorkflowsHarness.Until(handle, static v => v.Task is not null);

        var delta = await StartedDelta(served.Sindri, ct);

        Assert.Equal(handle.Id, delta.GetProperty("subject").GetProperty("label").GetString());
        await handle.TerminateAsync("test over");
    }

    /// <summary>The 'detail.delta' of the workflow.started row, read once the mirror has written it.</summary>
    private static async Task<System.Text.Json.JsonElement> StartedDelta(IServiceProvider sindri, CancellationToken ct)
    {
        var deadline = RealTime.NowDateTime + TimeSpan.FromSeconds(20);
        while (true)
        {
            var row = (await sindri.GetRequiredService<Core.Ledger.ILedgerStore>().ReadAfter(0, 100, ct)).Value.FirstOrDefault(static e => e.Envelope.Type == "workflow.started");
            if (row is not null)
            {
                return System.Text.Json.JsonDocument.Parse(row.Envelope.Payload).RootElement.GetProperty("detail").GetProperty("delta").Clone();
            }

            Assert.True(RealTime.NowDateTime < deadline, "the workflow.started row was not mirrored in 20 s");
            await Task.Delay(200, ct);
        }
    }
}
