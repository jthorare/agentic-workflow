using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Redaction;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Sandbox.Denials;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Host;
using Aw.Sindri.Infrastructure.Sandbox;
using Aw.Sindri.Infrastructure.Text;
using Aw.Sindri.Tests.Sandbox.Denials;
using Aw.Sindri.Tests.Sandbox.Plan;
using Req = Aw.Sindri.Testing.Sandbox.SandboxRequests;

namespace Aw.Sindri.Tests.Sandbox.Launch;

/// <summary>Collecting a session's denials at its end (sandbox spec §3.6): transcript tail and failopen.log into the counter, flushed and published.</summary>
[Lane(TestLane.Unit)]
public sealed class DenialCollectorTests : IDisposable
{
    private const string Home = "/home/dev";
    private static readonly LaunchId _launch = LaunchId.From("LCH-00000000000000000000000000000001");

    private readonly string _dir = Directory.CreateTempSubdirectory("aw-collect-").FullName;
    private readonly RecordingDenials _sink = new();

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "Sandbox", "Denials", "Fixtures", name);

    private static SandboxPlan Plan() => SandboxPlannerTests.Build(Req.WriteScope) with
    {
        Mounts = [new PlanMount(Home, PlanMountKind.Tmpfs), new PlanMount(Home + "/work", PlanMountKind.ReadWrite), new PlanMount(Home + "/.ssh", PlanMountKind.Deny)],
    };

    private DenialCounter Counter() =>
        new(_launch, Path.Combine(_dir, "denials.json"), AwJson.CreateOptions(SindriJsonContext.Default), new RedactingScrubber(Redactor.Default), TimeProvider.System) { Sink = _sink };

    [Fact]
    public async Task A_claude_transcript_counts_policy_and_path_denials()
    {
        var counter = Counter();

        await DenialCollector.Collect(new DenialSources(Plan(), Home, Fixture("claude-tail.jsonl"), TranscriptKind.Claude, FailOpenLogPath: null), counter, TestContext.Current.CancellationToken);

        Assert.Equal(1, counter.Count(DenialKind.Policy, "Bash: git push", DenialRules.InnerPolicy));
        Assert.Equal(1, counter.Count(DenialKind.Path, Home + "/.ssh/probe", DenialRules.DenyList));
        Assert.Equal(0, counter.Count(DenialKind.Path, "/var/data/missing.txt", DenialRules.NotBound));   // outside $HOME: genuinely missing
    }

    [Fact]
    public async Task The_failopen_log_counts_each_hook_that_let_a_call_through()
    {
        var counter = Counter();

        await DenialCollector.Collect(new DenialSources(Plan(), Home, TranscriptPath: null, TranscriptKind.Claude, Fixture("failopen.log")), counter, TestContext.Current.CancellationToken);

        Assert.Equal(1, counter.Count(DenialKind.Policy, "send-gate", DenialRules.HookFailOpen));
        Assert.Equal(1, counter.Count(DenialKind.Policy, "scope-gate", DenialRules.HookFailOpen));
        Assert.Equal(0, counter.Count(DenialKind.Policy, "ignored", DenialRules.HookFailOpen));
    }

    [Fact]
    public async Task Collecting_writes_the_counter_file_and_publishes_the_events_before_returning()
    {
        var counter = Counter();

        await DenialCollector.Collect(new DenialSources(Plan(), Home, Fixture("claude-tail.jsonl"), TranscriptKind.Claude, Fixture("failopen.log")), counter, TestContext.Current.CancellationToken);

        Assert.True(File.Exists(Path.Combine(_dir, "denials.json")));
        Assert.Contains(_sink.Sent, d => string.Equals(d.Target, "Bash: git push", StringComparison.Ordinal) && d.Count == 1);
        Assert.Contains(_sink.Sent, d => string.Equals(d.Target, "send-gate", StringComparison.Ordinal) && d.Count == 1);
    }

    [Fact]
    public async Task A_codex_rollout_counts_its_failed_paths()
    {
        var counter = Counter();

        await DenialCollector.Collect(new DenialSources(Plan(), Home, Fixture("codex-tail.jsonl"), TranscriptKind.Codex, FailOpenLogPath: null), counter, TestContext.Current.CancellationToken);

        var denied = counter.Snapshot().Entries.Where(e => e.Kind == DenialKind.Path).ToList();
        Assert.All(denied, e => Assert.StartsWith(Home, e.Target, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Missing_files_collect_nothing_and_do_not_throw()
    {
        var counter = Counter();

        await DenialCollector.Collect(new DenialSources(Plan(), Home, Path.Combine(_dir, "no.jsonl"), TranscriptKind.Claude, Path.Combine(_dir, "no.log")), counter, TestContext.Current.CancellationToken);

        Assert.Empty(counter.Snapshot().Entries);
        Assert.Empty(_sink.Sent);
    }

    [Fact]
    public async Task A_refusing_ledger_leaves_the_events_queued_and_the_file_written()
    {
        var counter = Counter();
        _sink.Refuse = true;

        await DenialCollector.Collect(new DenialSources(Plan(), Home, Fixture("claude-tail.jsonl"), TranscriptKind.Claude, FailOpenLogPath: null), counter, TestContext.Current.CancellationToken);

        Assert.True(File.Exists(Path.Combine(_dir, "denials.json")));
        Assert.Empty(_sink.Sent);
    }
}
