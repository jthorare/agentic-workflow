using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.ModelHistory;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Launch;

/// <summary>The model-history view as the pick's question shows it (model-pick M-5): the last outcomes counted per model, a crash counted as neither a refusal nor a failed check, a view that can't be read as no history.</summary>
[Lane(TestLane.Unit)]
public sealed class ModelPickHistoryAdapterTests
{
    private static readonly RecipeId _recipe = RecipeId.From("workflow.scoping");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Each_models_recent_outcomes_are_counted()
    {
        var row = ModelHistoryRow.Empty(_recipe, 3, "draft", "haiku") with
        {
            Recent = [ModelOutcome.Ok, ModelOutcome.Refused, ModelOutcome.CheckFailed, ModelOutcome.Crashed, ModelOutcome.Redraft, ModelOutcome.Refused],
        };
        var adapter = new ModelPickHistoryAdapter(new Reads(Rows(row, ModelHistoryRow.Empty(_recipe, 3, "draft", "opus"))));

        var rows = await adapter.For(_recipe, 3, "draft", ["haiku", "opus"], Token);

        Expect.Equal(("haiku", 6, 2, 1, 1), (rows[0].Model, rows[0].Runs, rows[0].Refused, rows[0].CheckFailed, rows[0].Redraft));
        Expect.Equal(("opus", 0, false), (rows[1].Model, rows[1].Runs, rows[1].Unusual));
        Assert.False(rows[0].Unusual);   // 2 refused + 1 check failed of 6 is exactly half, not more than half
    }

    [Fact]
    public async Task A_view_that_cannot_be_read_gives_no_history()
    {
        var adapter = new ModelPickHistoryAdapter(new Reads(SindriErrors.InvalidInput.With("views.db is locked")));

        Assert.Empty(await adapter.For(_recipe, 3, "draft", ["haiku"], Token));
    }

    private static Result<IReadOnlyList<ModelHistoryRow>> Rows(params ModelHistoryRow[] rows) => rows;

    private sealed class Reads(Result<IReadOnlyList<ModelHistoryRow>> answer) : IModelHistory
    {
        public ValueTask<Result<IReadOnlyList<ModelHistoryRow>>> For(RecipeId recipe, int version, string state, IReadOnlyList<string> models, CancellationToken cancellationToken) => ValueTask.FromResult(answer);
    }
}
