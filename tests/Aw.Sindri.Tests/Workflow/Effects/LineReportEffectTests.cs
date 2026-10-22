using System.Text;
using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Blocks.Effect;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Workflow.Effects;

/// <summary>
/// 'effect.line.report' (line spec BD7, LN-WF1, Q13): a phase that belongs to a line records 'shipped' or 'merged' once per effect identity in
/// 'line-report.jsonl' of its run area (the audit trail) and replies the status with 'fact:phaseEvent' { line, phase, status, instance, at }, which
/// the 'line.phaseChanged' event carries to the line; a phase with no line records and replies nothing.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class LineReportEffectTests
{
    private const string Instance = "T-4/phase/1";
    private const string Record = "line-report.jsonl";
    private const string LineContext = """{"line":"L1","phase":"p2","base":"origin/main","run":"line-1"}""";

    private readonly InMemoryRunArea _area = new();
    private readonly InMemoryFactStore _facts = new();
    private readonly FakeTimeProvider _clock = new(KnownInstants.Epoch);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_shipped_report_is_a_row_with_the_status_the_line_the_phase_and_the_instance()
    {
        var done = await Report("shipped", LineContext);

        Assert.Equal(OutcomeName.From("shipped"), done.Value.Reply);
        Assert.False(done.Value.Existed);
        var row = JsonDocument.Parse(Assert.Single(Rows())).RootElement;
        Assert.Equal("shipped", row.GetProperty("status").GetString());
        Assert.Equal("L1", row.GetProperty("line").GetString());
        Assert.Equal("p2", row.GetProperty("phase").GetString());
        Assert.Equal(Instance, row.GetProperty("instance").GetString());
    }

    [Theory]
    [InlineData("shipped")]
    [InlineData("merged")]
    [InlineData("failed")]
    public async Task A_report_replies_its_status_with_the_phase_storedfact(string status)
    {
        var done = await Report(status, LineContext);

        Assert.Equal(OutcomeName.From(status), done.Value.Reply);
        Assert.Equal("line.phaseChanged", done.Value.Event?.Value);   // 'merged' is also pr.mergeResult's and pr.docsMerged's outcome
        var fact = Assert.Single(done.Value.Facts!);
        Assert.Equal(FactKey.From("fact:phaseEvent"), fact.Key);
        var stored = await _facts.Read(new FactValue(fact.Value.Ref, fact.Value.Sha256, fact.Value.HeadSha), Token);
        var payload = JsonDocument.Parse(stored.Value).RootElement;
        Assert.Equal("L1", payload.GetProperty("line").GetString());
        Assert.Equal("p2", payload.GetProperty("phase").GetString());
        Assert.Equal(status, payload.GetProperty("status").GetString());
        Assert.Equal(Instance, payload.GetProperty("instance").GetString());
        Assert.Equal(JsonDocument.Parse(Assert.Single(Rows())).RootElement.GetProperty("at").GetString(), payload.GetProperty("at").GetString());
    }

    [Fact]
    public async Task A_repeated_identity_writes_one_row_and_replies_the_same_storedagain()
    {
        var first = await Report("merged", LineContext);
        _clock.Advance(TimeSpan.FromMinutes(5));

        var again = await Report("merged", LineContext);

        Assert.True(again.Value.Existed);
        Assert.Equal(OutcomeName.From("merged"), again.Value.Reply);
        Assert.Equal(Assert.Single(first.Value.Facts!).Value.Sha256, Assert.Single(again.Value.Facts!).Value.Sha256);
        _ = Assert.Single(Rows());
    }

    [Fact]
    public async Task A_phase_with_no_line_records_nothing()
    {
        var done = await Report("shipped", """{"base":"origin/main","run":"line-1"}""");

        Assert.Equal(new EffectDone(Reply: null, Existed: false), done.Value);
        Assert.Empty(Rows());
    }

    [Theory]
    [InlineData("started")]
    [InlineData("")]
    public async Task A_status_other_than_shipped_merged_or_failed_is_the_dispatch_error(string status)
    {
        var done = await Report(status, LineContext);

        Assert.True(done.IsError);
        Assert.Empty(Rows());
    }

    [Fact]
    public async Task A_line_that_is_not_a_stored_fact_is_the_dispatch_error()
    {
        var context = Context("shipped", JsonDocument.Parse("\"L1\"").RootElement.Clone());

        var done = await new LineReportEffect(_area, _facts, _facts, _clock).Dispatch(context, Token);

        Assert.True(done.IsError);
    }

    private string[] Rows() => _area.Read(Instance, Record, Token).AsTask().GetAwaiter().GetResult().Value.Split('\n', StringSplitOptions.RemoveEmptyEntries);

    private async Task<Aw.Kernel.Result<EffectDone>> Report(string status, string lineContext)
    {
        var put = (await _facts.Put(Encoding.UTF8.GetBytes(lineContext), Token)).Value;
        var stored = JsonDocument.Parse($$"""{"ref":"{{put.Ref}}","sha256":"{{put.Sha256}}"}""").RootElement.Clone();
        return await new LineReportEffect(_area, _facts, _facts, _clock).Dispatch(Context(status, stored), Token);
    }

    private static EffectContext Context(string status, JsonElement line) => new(
        BlockId.From("effect.line.report"), Instance + ":6:0", "c-1", FromEngine: false, Instance, "T-4", Attempt: 1,
        new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            ["status"] = JsonDocument.Parse(JsonSerializer.Serialize(status)).RootElement.Clone(),
            ["line"] = line,
        },
        new Dictionary<FactKey, JsonElement>(), Text: null);
}
