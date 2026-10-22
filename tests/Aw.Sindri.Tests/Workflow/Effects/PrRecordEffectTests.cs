using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Features.Blocks.Effect;
using Aw.Sindri.Testing.Scoping;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Workflow.Effects;

/// <summary>'effect.pr.record' (PB spec §4.7): appends a 'recheck' or 'docs' row to the merge record, once per effect identity.</summary>
[Lane(TestLane.Unit)]
public sealed class PrRecordEffectTests
{
    private readonly PrEffectRig _rig = new();
    private readonly InMemoryRunArea _area = new();
    private readonly FakeTimeProvider _clock = new(KnownInstants.Epoch);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_recheck_row_carries_the_kind_the_result_the_instance_and_the_head()
    {
        var done = await Record("recheck");

        Assert.False(done.Existed);
        Assert.Null(done.Reply);
        var row = JsonDocument.Parse(Assert.Single((await _area.Read(PrEffectRig.Instance, "babysit.jsonl", Token)).Value.Split('\n', StringSplitOptions.RemoveEmptyEntries))).RootElement;
        Assert.Equal("recheck", row.GetProperty("kind").GetString());
        Assert.Equal(PrEffectRig.Instance, row.GetProperty("instance").GetString());
        Assert.Equal("h1", row.GetProperty("headSha").GetString());
        Assert.Equal("resolved", row.GetProperty("result").GetProperty("verdict").GetString());
    }

    [Fact]
    public async Task A_repeated_identity_writes_one_row()
    {
        _ = await Record("docs");

        var again = await Record("docs");

        Assert.True(again.Existed);
        _ = Assert.Single((await _area.Read(PrEffectRig.Instance, "babysit.jsonl", Token)).Value.Split('\n', StringSplitOptions.RemoveEmptyEntries));
    }

    [Fact]
    public async Task A_kind_the_record_does_not_know_is_the_dispatch_error()
    {
        var context = _rig.Context("effect.pr.record", new Dictionary<string, object>(StringComparer.Ordinal) { ["kind"] = "push", ["result"] = PrEffectRig.Fact("{}") });

        var done = await new PrRecordEffect(_area, _rig.Facts, _clock).Dispatch(context, Token);

        Assert.True(done.IsError);
    }

    private async Task<Aw.Sindri.Core.Workflow.Blocks.EffectDone> Record(string kind)
    {
        var context = _rig.Context("effect.pr.record", new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["kind"] = kind,
            ["result"] = PrEffectRig.Fact("""{"verdict":"resolved"}"""),
        }, new Dictionary<string, string>(StringComparer.Ordinal) { ["fact:prState"] = PrEffectRig.PrState("h1", "h1") });
        return (await new PrRecordEffect(_area, _rig.Facts, _clock).Dispatch(context, Token)).Value;
    }
}
