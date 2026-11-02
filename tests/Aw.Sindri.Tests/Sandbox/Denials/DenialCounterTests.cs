using System.Globalization;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Redaction;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Sandbox.Denials;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Host;
using Aw.Sindri.Infrastructure.Sandbox;
using Aw.Sindri.Infrastructure.Text;
using Aw.Sindri.Tests.Ledger;
using Aw.Sindri.Tests.Sandbox.Egress;

namespace Aw.Sindri.Tests.Sandbox.Denials;

/// <summary>The run's denial counter (sandbox spec §3.6, §8): counts, the 200-target cap, the 1/10/100 cadence, scrubbing and the file.</summary>
public sealed class DenialCounterTests
{
    private static readonly LaunchId _launch = LaunchId.From("LCH-00000000000000000000000000000001");
    private static readonly System.Text.Json.JsonSerializerOptions _json = AwJson.CreateOptions(SindriJsonContext.Default);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static (DenialCounter Counter, RecordingDenials Sink) Make(string path = "/nonexistent/denials.json")
    {
        var sink = new RecordingDenials();
        return (new DenialCounter(_launch, path, _json, new RedactingScrubber(Redactor.Default), TimeProvider.System) { Sink = sink }, sink);
    }

    private static DenialObservation Net(string host, string rule = DenialRules.NoGroup) => new(DenialKind.Network, host, rule);

    [Fact, Lane(TestLane.Unit)]
    public async Task Counter_emits_on_the_1st_10th_and_100th()
    {
        var (counter, sink) = Make();

        for (var i = 0; i < 120; i++)
        {
            counter.Record(Net("evil.example"));
        }

        await counter.Publish(Token);

        Assert.Equal([1, 10, 100], sink.Sent.Select(s => s.Count));
        Assert.All(sink.Sent, s => Assert.Equal(("evil.example", DenialRules.NoGroup, DenialKind.Network, false), (s.Target, s.Rule, s.Kind, s.Overflow)));
        Assert.Equal(120, counter.Count(DenialKind.Network, "evil.example", DenialRules.NoGroup));
    }

    [Fact, Lane(TestLane.Unit)]
    public async Task Counter_overflows_after_200_targets_once_per_kind()
    {
        var (counter, sink) = Make();
        for (var i = 0; i < DenialCounter.MaxTargets; i++)
        {
            counter.Record(Net(string.Create(CultureInfo.InvariantCulture, $"h{i}.example")));
        }

        for (var i = 0; i < 5; i++)
        {
            counter.Record(Net(string.Create(CultureInfo.InvariantCulture, $"extra{i}.example")));
        }

        counter.Record(new DenialObservation(DenialKind.Path, "/home/x/.ssh", DenialRules.NotBound));   // a path is a kind of its own, but the 200 are shared: it overflows too
        counter.Record(Net("h0.example"));   // an already counted target keeps counting
        await counter.Publish(Token);

        var overflow = sink.Sent.Where(s => s.Overflow).ToList();
        Assert.Equal([DenialKind.Network, DenialKind.Path], overflow.Select(o => o.Kind));
        Assert.All(overflow, o => Assert.Equal((string.Empty, DenialRules.Overflow, 1), (o.Target, o.Rule, o.Count)));
        var file = counter.Snapshot();
        Assert.Equal(DenialCounter.MaxTargets, file.Entries.Count);
        Assert.Equal([(DenialKind.Network, 5), (DenialKind.Path, 1)], file.Overflow.Select(o => (o.Kind, o.Count)));
        Assert.Equal(2, counter.Count(DenialKind.Network, "h0.example", DenialRules.NoGroup));
    }

    [Fact, Lane(TestLane.Unit)]
    public async Task Planted_token_in_a_target_is_not_in_the_event_or_the_file()
    {
        var (counter, sink) = Make();
        var token = "ghp_" + new string('a', 36);

        counter.Record(Net(token + ".evil.example"));
        counter.Record(new DenialObservation(DenialKind.Path, "/home/x/" + token + "/id", DenialRules.DenyList));
        await counter.Publish(Token);

        var wire = string.Join('\n', sink.Sent.Select(s => System.Text.Encoding.UTF8.GetString(AwJson.Serialize(s, _json))))
            + System.Text.Encoding.UTF8.GetString(AwJson.Serialize(counter.Snapshot(), _json));
        Assert.DoesNotContain(token, wire, StringComparison.Ordinal);
        Assert.Equal(2, sink.Sent.Count);
    }

    [Fact, Lane(TestLane.Unit)]
    public void A_long_multiline_target_is_cut_and_has_no_control_characters()
    {
        var (counter, _) = Make();

        counter.Record(new DenialObservation(DenialKind.Policy, "Bash\nrm -rf /" + new string('x', 400), DenialRules.InnerPolicy));

        var target = Assert.Single(counter.Snapshot().Entries).Target;
        Assert.Equal(DenialCounter.MaxTargetLength, target.Length);
        Assert.DoesNotContain('\n', target);
    }

    [Fact, Lane(TestLane.Unit)]
    public async Task A_refused_send_stays_queued_for_the_next_publish()
    {
        var (counter, sink) = Make();
        sink.Refuse = true;
        counter.Record(Net("evil.example"));
        await counter.Publish(Token);
        Assert.Empty(sink.Sent);

        sink.Refuse = false;
        await counter.Publish(Token);

        _ = Assert.Single(sink.Sent);
    }

    [Fact, Lane(TestLane.Integration)]
    public void Denials_file_is_rewritten_only_when_changed()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "denials.json");
        var (counter, _) = Make(path);

        counter.Flush();
        Assert.True(File.Exists(path));   // an empty counter still writes once
        var first = File.GetLastWriteTimeUtc(path);
        File.SetLastWriteTimeUtc(path, first.AddHours(-1));
        var stamped = File.GetLastWriteTimeUtc(path);

        counter.Flush();
        Assert.Equal(stamped, File.GetLastWriteTimeUtc(path));

        counter.Record(Net("evil.example"));
        counter.Flush();
        Assert.NotEqual(stamped, File.GetLastWriteTimeUtc(path));
        Assert.Contains("evil.example", File.ReadAllText(path), StringComparison.Ordinal);
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact, Lane(TestLane.Unit)]
    public void Flush_creates_the_denials_folder_when_it_is_not_there_yet()
    {
        using var home = new TempHome();
        var path = LaunchPaths.DenialsFile(Path.Combine(home.Path, "launch"), _launch);
        var (counter, _) = Make(path);
        counter.Record(Net("evil.example"));

        counter.Flush();

        Assert.True(File.Exists(path));
    }

    [Fact, Lane(TestLane.Unit)]
    public void Flush_writes_the_counter_file_owner_only()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var home = new TempHome();
        var path = LaunchPaths.DenialsFile(Path.Combine(home.Path, "launch"), _launch);
        var (counter, _) = Make(path);
        counter.Record(Net("evil.example"));

        counter.Flush();

        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));
    }
}
