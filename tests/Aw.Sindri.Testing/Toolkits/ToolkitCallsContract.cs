using Aw.Sindri.Core.Toolkits;
using Xunit;

namespace Aw.Sindri.Testing.Toolkits;

/// <summary>The contract every <see cref="IToolkitCalls"/> honours: a call reads back whole, a list is newest first with a toolkit filter and a limit, and delete-before keeps newer rows.</summary>
public abstract class ToolkitCallsContract
{
    private static readonly DateTimeOffset _t0 = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A fresh, empty store.</summary>
    /// <returns>The store.</returns>
    protected abstract IToolkitCalls Create();

    /// <summary>A call for tests: the minute is how long after a fixed start it began.</summary>
    /// <param name="toolkit">The toolkit.</param>
    /// <param name="tool">The tool.</param>
    /// <param name="minute">Minutes after the fixed start.</param>
    /// <returns>The call.</returns>
    public static ToolkitCall Sample(string toolkit, string tool, int minute)
        => new(toolkit, tool, "session:x", Input: "{}", Output: "ok", Truncated: false, Error: null, DurationMs: 5, At: _t0.AddMinutes(minute));

    /// <summary>A call reads back whole.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_call_reads_back_whole()
    {
        var store = Create();
        var call = Sample("notes", "search", 0) with { Input = "{\"q\":1}", Output = "ok", Truncated = true, Error = "boom", DurationMs = 42 };

        _ = await store.Add(call, Token);
        var read = await store.List(toolkit: null, limit: 10, Token);

        Expect.True(read.IsSuccess);
        Expect.Equal(call, Expect.Only(read.Value));
    }

    /// <summary>A list is newest first, filtered by toolkit and cut at the limit.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_list_is_newest_first_filtered_by_toolkit_and_limited()
    {
        var store = Create();
        _ = await store.Add(Sample("notes", "a", 1), Token);
        _ = await store.Add(Sample("other", "b", 2), Token);
        _ = await store.Add(Sample("notes", "c", 3), Token);

        var all = await store.List(toolkit: null, limit: 10, Token);
        var notes = await store.List("notes", 10, Token);
        var one = await store.List(toolkit: null, limit: 1, Token);

        Expect.Sequence(["c", "b", "a"], all.Value.Select(static c => c.Tool));
        Expect.Sequence(["c", "a"], notes.Value.Select(static c => c.Tool));
        Expect.Sequence(["c"], one.Value.Select(static c => c.Tool));
    }

    /// <summary>Delete-before removes older rows and keeps the rest.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Delete_before_removes_older_rows_only()
    {
        var store = Create();
        _ = await store.Add(Sample("notes", "old", 1), Token);
        _ = await store.Add(Sample("notes", "new", 10), Token);

        var deleted = await store.DeleteBefore(_t0.AddMinutes(5), Token);
        var left = await store.List(toolkit: null, limit: 10, Token);

        Expect.Equal(1, deleted.Value);
        Expect.Sequence(["new"], left.Value.Select(static c => c.Tool));
    }
}
