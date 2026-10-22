using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.NeedsYou;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.GetInstance;

namespace Aw.Sindri.Tests.Layout;

/// <summary>
/// The injection plan on the status surfaces and in the ledger (plan S4): 'injection' lines and fields hold the section, input, size and
/// digest, never text; the 'workflow.injection' row is catalogued and public; and no projection that decides item state or the needs-you
/// list reads it (P11).
/// </summary>
[Lane(TestLane.Unit)]
public sealed class StatusInjectionTests
{
    private static readonly string _sha = new('a', 64);

    private static InstanceView Planned(params InjectionEntry[] entries)
    {
        var view = StatusCases.View("desk-task");
        return view with { Task = view.Task! with { Injection = entries } };
    }

    [Fact]
    public void Status_text_lists_each_section_after_the_inputs_with_its_digest_and_size()
    {
        var view = Planned(new InjectionEntry("echo", BlockId.From("input.prove.echo"), 42, _sha), new InjectionEntry("notes", BlockId.From("input.b"), 7, _sha, Replaced: BlockId.From("input.a")));

        var text = StatusLayout.Render(view, StatusCases.Now);

        Assert.Contains($"  injection echo  input.prove.echo  sha256:{_sha[..12]}  42 B\n", text, StringComparison.Ordinal);
        Assert.Contains($"            notes  input.b  sha256:{_sha[..12]}  7 B (replaced input.a)\n", text, StringComparison.Ordinal);
        Assert.True(text.IndexOf("  inputs ", StringComparison.Ordinal) < text.IndexOf("  injection ", StringComparison.Ordinal));
    }

    [Fact]
    public void Status_fields_carry_the_same_sections_as_metadata_and_empty_without_inputs()
    {
        var fields = StatusRecords.Of(Planned(new InjectionEntry("echo", BlockId.From("input.prove.echo"), 42, _sha)), StatusCases.Now);

        var line = Assert.Single(fields.Injection);
        Assert.Equal(("echo", "input.prove.echo", 42, $"sha256:{_sha}"), (line.Section, line.Input, line.Bytes, line.Hash));
        var replacing = StatusRecords.Of(Planned(new InjectionEntry("notes", BlockId.From("input.b"), 7, _sha, Replaced: BlockId.From("input.a"))), StatusCases.Now);
        Assert.Equal("input.a", Assert.Single(replacing.Injection).Replaced);
        Assert.Null(line.Replaced);
        Assert.Empty(StatusRecords.Of(StatusCases.View("desk-task"), StatusCases.Now).Injection);
        Assert.DoesNotContain("injection", StatusLayout.Render(StatusCases.View("desk-task"), StatusCases.Now), StringComparison.Ordinal);
    }

    [Fact]
    public void Status_fields_carry_the_takeover_time_only_while_taken_over()
    {
        var taken = StatusRecords.Of(StatusCases.View("taken-over"), StatusCases.Now);

        Assert.Equal(taken.Header.Since, taken.TakenOverAt);
        Assert.Null(StatusRecords.Of(StatusCases.View("desk-task"), StatusCases.Now).TakenOverAt);
    }

    [Fact]
    public void The_injection_row_is_a_public_catalogued_event()
    {
        var entry = Assert.Single(EventCatalog.All, static e => e.Type == EventTypes.WorkflowInjection);

        Assert.True(entry.Public);
        Assert.Equal("workflow.injection", EventTypes.WorkflowInjection);
    }

    [Fact]
    public void No_state_deciding_projection_reads_the_injection_row()
    {
        var slice = new SliceHarness();

        Assert.False(new NeedsYouProjection(slice.Events, new PassThroughScrubber(), []).Handles(EventTypes.WorkflowInjection));
        Assert.False(new ItemsProjection(slice.Events, new PassThroughScrubber()).Handles(EventTypes.WorkflowInjection));
        Assert.False(new RequestsProjection(slice.Events, new PassThroughScrubber()).Handles(EventTypes.WorkflowInjection));
    }
}
