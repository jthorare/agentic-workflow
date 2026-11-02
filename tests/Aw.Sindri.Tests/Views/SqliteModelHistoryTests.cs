using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.ModelHistory;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Infrastructure.Views;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Ledger;

namespace Aw.Sindri.Tests.Views;

/// <summary>The 'model-history' view in views.db (model-pick M-4): the rows the real projection folds are read back by the history port, and 'sindri views rebuild' (a reset and a replay) gives the same rows.</summary>
[Lane(TestLane.Integration)]
public sealed class SqliteModelHistoryTests : IDisposable
{
    private const string Sonnet = "claude-sonnet-5-5";
    private const string Opus = "claude-opus-5-5";
    private const string Haiku = "claude-haiku-5-5";
    private readonly TempHome _home = new();
    private readonly SliceHarness _h = new();
    private readonly SqliteViews _views;

    public SqliteModelHistoryTests()
    {
        _views = new SqliteViews(Path.Combine(_home.Path, "home"));
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _views.Dispose();
        _home.Dispose();
    }

    private IReadOnlyList<LedgerEvent> Scenario() =>
        new ModelHistoryLedger(_h.Events).Begin()
            .Launched(ModelHistoryFolds.Run(1), Opus).Completed("draft", "check", ModelHistoryFolds.Run(1))
            .Launched(ModelHistoryFolds.Run(2), Sonnet, "check").Completed("check", "draft", ModelHistoryFolds.Run(2))
            .Launched(ModelHistoryFolds.Run(3), Sonnet).Failed("draft", ModelHistoryFolds.Run(3), "invalid-result: not json")
            .Launched(ModelHistoryFolds.Run(4), Sonnet).Failed("draft", ModelHistoryFolds.Run(4), "exit")
            .Rows;

    private async Task<string> Snapshot()
    {
        var history = await _views.Reader.List<ModelHistoryRow>(ViewNames.ModelHistory, Token);
        return string.Join('\n', history.Value.Select(static r => JsonSerializer.Serialize(r, SliceHarness.Json)));
    }

    [Fact]
    public async Task The_port_returns_one_row_per_model_in_the_order_asked_and_an_empty_row_for_a_model_with_no_history()
    {
        IProjection[] projections = [new ModelHistoryProjection(_h.Events)];
        _ = (await _views.Store.Open(ViewSchema.Version, Token)).Value;
        Assert.Null((await _views.Store.Apply(Scenario(), projections, _h.Clock.GetUtcNow(), Token)).Value.Stall);

        var rows = (await new SqliteModelHistory(_views.Reader).For(ModelHistoryFolds.Recipe, ModelHistoryFolds.Version, "draft", [Sonnet, Haiku, Opus], Token)).Value;

        Expect.Sequence([Sonnet, Haiku, Opus], rows.Select(static r => r.Model));
        Expect.Equal((2, 1, 1, 0), (rows[0].Runs, rows[0].Refused, rows[0].Crashed, rows[0].RedraftCaused));
        Expect.Equal((0, 0), (rows[1].Runs, rows[1].Recent.Count));   // never run: an empty row, not a missing one
        Expect.Equal((1, 1), (rows[2].Runs, rows[2].RedraftCaused));
        Assert.Equal(1.0, rows[0].EscalationRate);   // the one refused is all that counts; the crash is left out
    }

    [Fact]
    public async Task A_reset_and_a_replay_give_the_same_rows_byte_for_byte()
    {
        var ledger = Scenario();
        IProjection[] projections = [new ModelHistoryProjection(_h.Events)];
        _ = (await _views.Store.Open(ViewSchema.Version, Token)).Value;
        foreach (var recorded in ledger)
        {
            Assert.Null((await _views.Store.Apply([recorded], projections, _h.Clock.GetUtcNow(), Token)).Value.Stall);
        }

        var incremental = await Snapshot();
        _ = (await _views.Store.Reset(ViewNames.All, ViewSchema.Version, _h.Clock.GetUtcNow(), Token)).Value;
        Assert.Empty((await _views.Reader.List<ModelHistoryRow>(ViewNames.ModelHistory, Token)).Value);
        Assert.Null((await _views.Store.Apply(ledger, projections, _h.Clock.GetUtcNow(), Token)).Value.Stall);
        _ = (await _views.Store.FinishRebuild(Token)).Value;

        Assert.Contains("claude-sonnet-5-5", incremental, StringComparison.Ordinal);
        Assert.Equal(incremental, await Snapshot());
    }
}
