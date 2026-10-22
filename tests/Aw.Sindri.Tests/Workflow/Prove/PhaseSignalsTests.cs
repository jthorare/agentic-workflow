using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Prove;

namespace Aw.Sindri.Tests.Workflow.Prove;

/// <summary>The one reader of a phase packet's signals.json (line spec §3.2 row 4): the by-item form and the flat form the plan station copies from the design read alike.</summary>
[Lane(TestLane.Unit)]
public sealed class PhaseSignalsTests
{
    [Fact]
    public void The_by_item_form_is_read_as_it_is()
    {
        var file = JsonNode.Parse("""{"signals":{"AC-1":[{"id":"SG-1","kind":"test","statement":"s","runsAt":"base+head","script":"proofs/a.sh"}],"AC-2":[]}}""");

        var signal = Assert.Single(PhaseSignals.Of(file, "AC-1"));

        Assert.Equal(("SG-1", "base+head", "proofs/a.sh"), ((string?)signal["id"], (string?)signal["runsAt"], (string?)signal["script"]));
        Assert.Empty(PhaseSignals.Of(file, "AC-2"));
        Assert.Empty(PhaseSignals.Of(file, "AC-9"));
    }

    [Fact]
    public void The_flat_form_is_filtered_by_item_with_runs_read_as_runsAt_and_the_script_kept()
    {
        var file = JsonNode.Parse("""{"signals":[{"item":"AC-1","kind":"test","statement":"a","runs":"head"},{"item":"AC-2","kind":"test","statement":"b","runs":"base+head","script":"proofs/b.sh"},{"item":"AC-1","kind":"cli-transcript","statement":"c","runs":"base+head"}]}""");

        var own = PhaseSignals.Of(file, "AC-1");

        Assert.Equal(["head", "base+head"], own.Select(static s => (string?)s["runsAt"]));
        Assert.Equal("proofs/b.sh", (string?)Assert.Single(PhaseSignals.Of(file, "AC-2"))["script"]);
    }

    [Fact]
    public void A_flat_signal_with_no_id_is_named_by_its_item_and_position_and_keeps_an_id_it_has()
    {
        var file = JsonNode.Parse("""{"signals":[{"item":"AC-1","kind":"test","statement":"a","runs":"head"},{"item":"AC-1","id":"mine","kind":"test","statement":"b","runs":"head"},{"item":"AC-1","kind":"test","statement":"c","runs":"head"}]}""");

        Assert.Equal(["AC-1-s1", "mine", "AC-1-s3"], PhaseSignals.Of(file, "AC-1").Select(static s => (string?)s["id"]));
    }

    [Fact]
    public void Nothing_to_read_is_no_signals()
    {
        Assert.Empty(PhaseSignals.Of(null, "AC-1"));
        Assert.Empty(PhaseSignals.Of(JsonNode.Parse("""{"schemaVersion":1}"""), "AC-1"));
    }
}
