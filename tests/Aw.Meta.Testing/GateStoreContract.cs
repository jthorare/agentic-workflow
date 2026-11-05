using Aw.Meta.Core;
using Aw.Meta.Core.Gate;
using Aw.Meta.Core.Obligations;
using Aw.Meta.Core.Ports;
using Xunit;

namespace Aw.Meta.Testing;

/// <summary>The IGateStore contract: the same tests run against the in-memory fake and every real adapter.</summary>
public abstract class GateStoreContract
{
    private static readonly CancellationToken _none = CancellationToken.None;

    /// <summary>A fresh, empty store.</summary>
    /// <returns>The store.</returns>
    protected abstract ValueTask<IGateStore> Create();

    private static GateRecord Gate(int pr, string head, GateVerdict verdict = GateVerdict.Fail) => new(
        pr,
        head,
        "main1",
        ["repo", "dotnet"],
        [new GateStep("repo", GateVerdict.Pass, "/logs/repo.log"), new GateStep("unit", GateVerdict.Fail, "/logs/unit.log")],
        verdict,
        A.At(5))
    {
        Tree = "tree-" + head,
        Stack = [new StackedPr(188, "h188"), new StackedPr(189, "h189")],
    };

    private static bool Same(GateRecord? a, GateRecord b) =>
        a is not null && a.Pr == b.Pr && a.Run == b.Run && a.Head == b.Head && a.Main == b.Main && a.Verdict == b.Verdict && a.At == b.At
        && SameTrain(a, b) && a.Scopes.SequenceEqual(b.Scopes, StringComparer.Ordinal) && a.Steps.Select(Key).SequenceEqual(b.Steps.Select(Key), StringComparer.Ordinal);

    private static bool SameTrain(GateRecord a, GateRecord b) => a.Tree == b.Tree && a.Stack.SequenceEqual(b.Stack);

    private static string Key(GateStep step) => $"{step.Name}|{step.Verdict}|{step.Log}|{string.Join(',', step.Reruns.Select(r => r.Test + "=" + r.Verdict))}";

    /// <summary>Runs are numbered per pull request and the latest one is returned with its steps in order.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Runs_are_numbered_per_pull_request_and_the_latest_is_returned()
    {
        var store = await Create();

        var first = await store.Save(Gate(190, "h1"), _none);
        var second = await store.Save(Gate(190, "h2"), _none);
        var other = await store.Save(Gate(191, "h9"), _none);
        var latest = await store.Latest(190, _none);
        var none = await store.Latest(404, _none);

        Expect.That(first.Value.Run == 1 && second.Value.Run == 2 && other.Value.Run == 1, "runs count per pull request");
        Expect.That(Same(latest.Value, second.Value), "the latest run reads back with every field");
        Expect.That(latest.Value!.Steps.Select(s => s.Name).SequenceEqual(["repo", "unit"], StringComparer.Ordinal), "steps keep their order");
        Expect.That(none.IsSuccess && none.Value is null, "no run is null");
    }

    /// <summary>A gate saved with a planned run number above the stored maximum keeps it; a lower or unset one takes the next number.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_planned_run_above_the_stored_maximum_is_kept()
    {
        var store = await Create();

        var planned = await store.Save(Gate(190, "h1") with { Run = 3 }, _none);
        var next = await store.Save(Gate(190, "h2") with { Run = 1 }, _none);
        var unset = await store.Save(Gate(190, "h3") with { Run = 0 }, _none);
        var latest = await store.Latest(190, _none);

        Expect.That(planned.Value.Run == 3, "a planned run above the maximum is stored as planned");
        Expect.That(next.Value.Run == 4, "a run at or below the maximum takes the next number");
        Expect.That(unset.Value.Run == 5 && latest.Value!.Run == 5, "an unset run takes the next number");
    }

    /// <summary>A gate's tested tree and its stack read back through Latest and History; a gate without them reads back with a null tree and an empty stack.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task The_tree_and_the_stack_round_trip()
    {
        var store = await Create();
        var plain = Gate(190, "h1") with { Tree = null, Stack = [] };
        _ = await store.Save(plain, _none);
        _ = await store.Save(Gate(191, "h2"), _none);

        var legacy = await store.Latest(190, _none);
        var stacked = await store.Latest(191, _none);
        var history = await store.History(191, _none);

        Expect.That(legacy.Value!.Tree is null && legacy.Value.Stack.Count == 0, "no tree and no stack read back as null and empty");
        Expect.That(stacked.Value!.Tree == "tree-h2", "the tree reads back");
        Expect.That(stacked.Value.Stack.SequenceEqual([new StackedPr(188, "h188"), new StackedPr(189, "h189")]), "the stack reads back in order");
        Expect.That(history.Value[0].Tree == "tree-h2" && history.Value[0].Stack.Count == 2, "history carries both too");
    }

    /// <summary>The history lists every run of the pull request newest first, with steps.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task History_lists_every_run_newest_first()
    {
        var store = await Create();
        _ = await store.Save(Gate(190, "h1"), _none);
        _ = await store.Save(Gate(191, "h7"), _none);
        _ = await store.Save(Gate(190, "h2", GateVerdict.Pass), _none);

        var history = await store.History(190, _none);
        var none = await store.History(404, _none);

        Expect.That(history.Value.Select(g => (g.Run, g.Head)).SequenceEqual([(2, "h2"), (1, "h1")]), "newest run first, only this pull request");
        Expect.That(history.Value.All(g => g.Steps.Count == 2), "each run carries its steps");
        Expect.That(none.IsSuccess && none.Value.Count == 0, "no runs is an empty list");
    }

    /// <summary>Classifying a test of a failed step is stored per test; an unknown step or run is AW-META-007.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_failed_step_can_be_classified_per_test()
    {
        var store = await Create();
        var saved = await store.Save(Gate(190, "h1"), _none);

        var done = await store.Classify(190, saved.Value.Run, "unit", new TestVerdict("A", FlakeVerdict.Flake), _none);
        var other = await store.Classify(190, saved.Value.Run, "unit", new TestVerdict("B", FlakeVerdict.Real), _none);
        var again = await store.Classify(190, saved.Value.Run, "unit", new TestVerdict("B", FlakeVerdict.Flake), _none);
        var wrongStep = await store.Classify(190, saved.Value.Run, "nope", new TestVerdict("A", FlakeVerdict.Flake), _none);
        var wrongRun = await store.Classify(190, 9, "unit", new TestVerdict("A", FlakeVerdict.Flake), _none);
        var latest = await store.Latest(190, _none);
        var history = await store.History(190, _none);

        Expect.That(done.IsSuccess && other.IsSuccess && again.IsSuccess, "classifying succeeds");
        Expect.That(latest.Value!.Steps[1].Reruns.SequenceEqual([new TestVerdict("A", FlakeVerdict.Flake), new TestVerdict("B", FlakeVerdict.Flake)]), "each test keeps its own verdict, the last one replacing an earlier one");
        Expect.That(latest.Value.Steps[0].Reruns.Count == 0, "a step with no rerun has none");
        Expect.That(history.Value[0].Steps[1].Reruns.Count == 2, "history carries the reruns too");
        Expect.That(wrongStep.IsError && wrongStep.Error.Code == MetaErrors.GateStepNotFound.Code, "AW-META-007 for an unknown step");
        Expect.That(wrongRun.IsError && wrongRun.Error.Code == MetaErrors.GateStepNotFound.Code, "AW-META-007 for an unknown run");
    }

    /// <summary>The latest merge is the one that merged last.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task The_latest_merge_is_the_most_recent()
    {
        var store = await Create();
        var none = await store.LatestMerge(_none);
        var older = new MergeRecord(188, "aaaaaaa1111", A.At(1));
        var newer = new MergeRecord(190, "bbbbbbb2222", A.At(9));

        _ = await store.RecordMerge(newer, _none);
        _ = await store.RecordMerge(older, _none);
        var latest = await store.LatestMerge(_none);

        Expect.That(none.IsSuccess && none.Value is null, "no merge is null");
        Expect.That(latest.Value == newer, "the most recent merge wins");
    }

    /// <summary>A merge is found by its pull request.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_merge_is_found_by_its_pull_request()
    {
        var store = await Create();
        var merge = new MergeRecord(190, "bbbbbbb2222", A.At(9));

        var before = await store.FindMerge(190, _none);
        _ = await store.RecordMerge(merge, _none);
        var after = await store.FindMerge(190, _none);
        var other = await store.FindMerge(191, _none);

        Expect.That(before.IsSuccess && before.Value is null, "no merge is null");
        Expect.That(after.Value == merge, "the recorded merge is found");
        Expect.That(other.Value is null, "another pull request has none");
    }
}
