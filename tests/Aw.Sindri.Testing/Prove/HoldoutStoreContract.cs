using Aw.Sindri.Core;
using Aw.Sindri.Core.Prove;
using Aw.Sindri.Core.Workflow;
using Xunit;

namespace Aw.Sindri.Testing.Prove;

/// <summary>The contract every <see cref="IHoldoutStore"/> honours (line spec §9.1).</summary>
public abstract class HoldoutStoreContract
{
    private const string Line = "T-7";
    private const string Phase = "p1";

    private const string SetText = """{ "schemaVersion": 1, "phase": "p1", "scenarios": [ { "id": "P1-H1", "item": "AC-1", "kind": "api", "steps": ["open an expired link"], "expected": "answers 410" }, { "id": "P1-H2", "item": "AC-2", "kind": "ui", "steps": ["click save twice"], "expected": "one record" }, { "id": "P1-C", "item": null, "kind": "canary", "steps": [], "expected": "never", "phrase": "violet otter ledger 4417" } ] }""";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>An empty store.</summary>
    /// <returns>The store.</returns>
    protected abstract IHoldoutStore Create();

    /// <summary>Puts a phase's files in the store, as the author Step and the seal would: a null text leaves that file out.</summary>
    /// <param name="store">The store from <see cref="Create"/>.</param>
    /// <param name="line">The line's slug.</param>
    /// <param name="phase">The phase key.</param>
    /// <param name="setText">The set file's text.</param>
    /// <param name="sealText">The seal file's text.</param>
    protected abstract void Seed(IHoldoutStore store, string line, string phase, string? setText, string? sealText);

    /// <summary>A written set reads back with its scenarios, its canary phrase and the hash of its file.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_set_reads_back_with_its_canary_and_the_hash_of_its_file()
    {
        var store = Create();
        Seed(store, Line, Phase, SetText, sealText: null);

        var read = await store.Set(Line, Phase, Token);

        Expect.True(read.IsSuccess);
        Expect.Sequence(["P1-H1", "P1-H2", "P1-C"], read.Value.Scenarios.Select(static s => s.Key));
        Expect.Equal("AC-1", read.Value.Scenarios[0].Item);
        Expect.Equal("click save twice", read.Value.Scenarios[1].Steps[0]);
        Expect.Equal("one record", read.Value.Scenarios[1].Expected);
        Expect.Equal("violet otter ledger 4417", read.Value.Canary);
        Expect.Equal(HoldoutFiles.Hash(SetText), read.Value.Hash);
        Expect.True(read.Value.Scenarios[2].IsCanary && read.Value.Scenarios[2].Item is null);
    }

    /// <summary>A seal reads back; a phase with a set but no seal has none.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_seal_reads_back_and_an_unsealed_phase_has_none()
    {
        var store = Create();
        Seed(store, Line, Phase, SetText, """{ "schemaVersion": 1, "phase": "p1", "setHash": "sha256:aa", "planHash": "sha256:bb" }""");
        Seed(store, Line, "p2", SetText, sealText: null);

        var sealed1 = await store.Seal(Line, Phase, Token);
        var unsealed = await store.Seal(Line, "p2", Token);

        Expect.True(sealed1.IsSuccess);
        Expect.Equal("sha256:aa", sealed1.Value.SetHash);
        Expect.Equal("sha256:bb", sealed1.Value.PlanHash);
        Expect.True(unsealed.IsError && unsealed.Error.Code == WorkflowErrors.HoldoutSetMissing.Code);
    }

    /// <summary>A phase nothing was written for is SND-BLOCK-020, never an empty set.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_phase_with_no_set_is_not_found()
    {
        var read = await Create().Set(Line, Phase, Token);

        Expect.True(read.IsError);
        Expect.Equal(WorkflowErrors.HoldoutSetMissing.Code, read.Error.Code);
    }

    /// <summary>A set file that is not a set is SND-BLOCK-021.</summary>
    /// <param name="text">The file's text.</param>
    /// <returns>The test.</returns>
    [Theory]
    [InlineData("not json")]
    [InlineData("""{ "phase": "p1", "scenarios": [] }""")]
    [InlineData("""{ "scenarios": [ { "id": "x", "kind": "api" } ] }""")]
    [InlineData("""{ "phase": "p1", "scenarios": [ { "kind": "api" } ] }""")]
    public async Task A_set_that_is_not_a_set_is_unavailable(string text)
    {
        var store = Create();
        Seed(store, Line, Phase, text, sealText: null);

        var read = await store.Set(Line, Phase, Token);

        Expect.True(read.IsError);
        Expect.Equal(WorkflowErrors.HoldoutStoreUnavailable.Code, read.Error.Code);
    }

    /// <summary>No scenario is spent until marked; marks accumulate, marking twice changes nothing, and each phase keeps its own.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Spent_marks_accumulate_stay_and_belong_to_their_phase()
    {
        var store = Create();
        Seed(store, Line, Phase, SetText, sealText: null);
        Seed(store, Line, "p2", SetText, sealText: null);
        Expect.Equal(0, (await store.Spent(Line, Phase, Token)).Value.Count);

        Expect.True((await store.MarkSpent(Line, Phase, ["P1-H1"], Token)).IsSuccess);
        Expect.True((await store.MarkSpent(Line, Phase, ["P1-H1", "P1-H2"], Token)).IsSuccess);
        Expect.True((await store.MarkSpent(Line, Phase, [], Token)).IsSuccess);

        Expect.Sequence(["P1-H1", "P1-H2"], (await store.Spent(Line, Phase, Token)).Value.Order(StringComparer.Ordinal));
        Expect.Equal(0, (await store.Spent(Line, "p2", Token)).Value.Count);
    }

    /// <summary>A mark for a scenario the set does not hold, or on a phase with no set, is refused and writes nothing.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Marking_what_is_not_in_the_set_is_refused()
    {
        var store = Create();
        Seed(store, Line, Phase, SetText, sealText: null);

        var unknown = await store.MarkSpent(Line, Phase, ["P1-H1", "P9-H9"], Token);
        var absent = await store.MarkSpent(Line, "p3", ["P1-H1"], Token);

        Expect.True(unknown.IsError && absent.IsError);
        Expect.Equal(WorkflowErrors.HoldoutSetMissing.Code, absent.Error.Code);
        Expect.Equal(0, (await store.Spent(Line, Phase, Token)).Value.Count);
    }

    /// <summary>Lines are apart: the same phase key under another line is another set.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Lines_are_apart()
    {
        var store = Create();
        Seed(store, Line, Phase, SetText, sealText: null);

        Expect.True((await store.Set("T-8", Phase, Token)).IsError);
        _ = await store.MarkSpent(Line, Phase, ["P1-H1"], Token);
        Expect.True((await store.Spent("T-8", Phase, Token)).Value.Count == 0);
    }

    /// <summary>A line or phase that is not one safe path segment is refused on every call.</summary>
    /// <param name="key">The key.</param>
    /// <returns>The test.</returns>
    [Theory]
    [InlineData("")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("../x")]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData("/etc")]
    [InlineData(".hidden")]
    public async Task A_key_that_is_not_one_segment_is_refused(string key)
    {
        var store = Create();

        Expect.Equal(SindriErrorsInvalid, (await store.Set(key, Phase, Token)).Error.Code);
        Expect.Equal(SindriErrorsInvalid, (await store.Set(Line, key, Token)).Error.Code);
        Expect.Equal(SindriErrorsInvalid, (await store.Seal(key, Phase, Token)).Error.Code);
        Expect.Equal(SindriErrorsInvalid, (await store.Spent(Line, key, Token)).Error.Code);
        Expect.Equal(SindriErrorsInvalid, (await store.MarkSpent(key, Phase, ["P1-H1"], Token)).Error.Code);
    }

    private static string SindriErrorsInvalid => SindriErrors.InvalidInput.Code;
}
