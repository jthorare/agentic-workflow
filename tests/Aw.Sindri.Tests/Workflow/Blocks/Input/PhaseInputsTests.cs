using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Blocks.Input;
using Aw.Sindri.Testing.Scoping;

namespace Aw.Sindri.Tests.Workflow.Blocks.Input;

/// <summary>'input.phase-packet' and 'input.handoff-note' (PB spec §10.1, W24): the frozen packet's phase.json and design.md, and the latest hand-off note, as sections.</summary>
[Lane(TestLane.Unit)]
public sealed class PhaseInputsTests
{
    private const string PacketDir = "/work/web/.agentic-workflow/packets/p1";

    private static StepInputContext Context(string input, FactValue? packet = null, FactValue? note = null)
    {
        var facts = new Dictionary<FactKey, JsonElement>();
        if (packet is not null)
        {
            facts[FactKey.From("fact:phasePacket")] = Stored(packet);
        }

        if (note is not null)
        {
            facts[FactKey.From("fact:handoffNote")] = Stored(note);
        }

        return new StepInputContext(BlockId.From(input), BlockId.From("step.pr.ci-fix"), new Dictionary<string, JsonElement>(StringComparer.Ordinal), facts, ContextInputFixtures.Web);
    }

    private static JsonElement Stored(FactValue fact) =>
        JsonSerializer.SerializeToElement(new Dictionary<string, string?>(StringComparer.Ordinal) { ["ref"] = fact.Ref, ["sha256"] = fact.Sha256, ["headSha"] = fact.HeadSha });

    private static PhasePacketInput Packet(ContextInputFixtures.MemoryFacts facts, Aw.Sindri.Core.Scoping.IBriefFiles? files = null) =>
        new(files ?? new InMemoryBriefFiles().With("phase.json", "{\"goal\":\"ship it\"}").With("design.md", "# Design\nthe plan"), new InMemoryRepoCheckouts().With("web", "/work/web"), facts);

    [Fact]
    public async Task Phase_packet_serves_phase_json_and_design_md_from_the_facts_directory()
    {
        var facts = new ContextInputFixtures.MemoryFacts();
        var packet = facts.Put("{\"dir\":\"" + PacketDir + "\",\"hash\":\"sha256:abc\"}");

        var filled = await Packet(facts).Fill(Context(PhasePacketInput.Id, packet), TestContext.Current.CancellationToken);

        var text = filled.Value.Sections[PhasePacketInput.Section];
        Assert.Contains("phase.json", text, StringComparison.Ordinal);
        Assert.Contains("\"goal\":\"ship it\"", text, StringComparison.Ordinal);
        Assert.Contains("design.md", text, StringComparison.Ordinal);
        Assert.Contains("the plan", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Phase_packet_reads_a_relative_directory_under_the_repos_checkout()
    {
        var facts = new ContextInputFixtures.MemoryFacts();
        var packet = facts.Put("{\"dir\":\"packets/p1\"}");
        var roots = new List<string>();
        var files = new RootRecordingFiles(new InMemoryBriefFiles().With("phase.json", "{}").With("design.md", "d"), roots);

        _ = await Packet(facts, files).Fill(Context(PhasePacketInput.Id, packet), TestContext.Current.CancellationToken);

        Assert.All(roots, static r => Assert.Equal("/work/web/packets/p1", r));
        Assert.NotEmpty(roots);
    }

    [Fact]
    public async Task Phase_packet_without_the_fact_is_an_honest_section_and_the_step_still_runs()
    {
        var filled = await Packet(new ContextInputFixtures.MemoryFacts()).Fill(Context(PhasePacketInput.Id), TestContext.Current.CancellationToken);

        Assert.False(filled.IsError);
        Assert.Contains("unavailable", filled.Value.Sections[PhasePacketInput.Section], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Phase_packet_with_an_unreadable_file_names_the_file_in_an_unavailable_section()
    {
        var facts = new ContextInputFixtures.MemoryFacts();
        var packet = facts.Put("{\"dir\":\"" + PacketDir + "\"}");

        var filled = await Packet(facts, new InMemoryBriefFiles().With("phase.json", "{}")).Fill(Context(PhasePacketInput.Id, packet), TestContext.Current.CancellationToken);

        Assert.False(filled.IsError);
        var text = filled.Value.Sections[PhasePacketInput.Section];
        Assert.Contains("unavailable", text, StringComparison.Ordinal);
        Assert.Contains("design.md", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Phase_packet_whose_stored_content_changed_fails_the_fill()
    {
        var facts = new ContextInputFixtures.MemoryFacts();
        var packet = facts.Put("{\"dir\":\"" + PacketDir + "\"}");
        var changed = packet with { Sha256 = new string('a', 64) };

        var filled = await Packet(facts).Fill(Context(PhasePacketInput.Id, changed), TestContext.Current.CancellationToken);

        Assert.True(filled.IsError);
    }

    [Fact]
    public async Task Handoff_note_serves_done_next_risks_and_files()
    {
        var facts = new ContextInputFixtures.MemoryFacts();
        var note = facts.Put("{\"done\":\"fixed the flake\",\"next\":\"rerun checks\",\"risks\":\"timing\",\"files\":[\"a.cs\",\"b.cs\"]}");

        var filled = await new HandoffNoteInput(facts).Fill(Context(HandoffNoteInput.Id, note: note), TestContext.Current.CancellationToken);

        var text = filled.Value.Sections[HandoffNoteInput.Section];
        Assert.Contains("fixed the flake", text, StringComparison.Ordinal);
        Assert.Contains("rerun checks", text, StringComparison.Ordinal);
        Assert.Contains("timing", text, StringComparison.Ordinal);
        Assert.Contains("a.cs", text, StringComparison.Ordinal);
        Assert.Contains("b.cs", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Handoff_note_before_any_agent_wrote_one_says_so()
    {
        var filled = await new HandoffNoteInput(new ContextInputFixtures.MemoryFacts()).Fill(Context(HandoffNoteInput.Id), TestContext.Current.CancellationToken);

        Assert.False(filled.IsError);
        Assert.Contains("no handoff note", filled.Value.Sections[HandoffNoteInput.Section], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Handoff_note_that_is_plain_text_is_served_as_it_is()
    {
        var facts = new ContextInputFixtures.MemoryFacts();
        var note = facts.Put("left the branch green");

        var filled = await new HandoffNoteInput(facts).Fill(Context(HandoffNoteInput.Id, note: note), TestContext.Current.CancellationToken);

        Assert.Contains("left the branch green", filled.Value.Sections[HandoffNoteInput.Section], StringComparison.Ordinal);
    }

    private sealed class RootRecordingFiles(Aw.Sindri.Core.Scoping.IBriefFiles inner, List<string> roots) : Aw.Sindri.Core.Scoping.IBriefFiles
    {
        public ValueTask<Aw.Kernel.Result<Aw.Sindri.Core.Scoping.BriefFile>> Read(string path, IReadOnlyList<string> directories, CancellationToken cancellationToken)
        {
            roots.AddRange(directories);
            return inner.Read(path, directories, cancellationToken);
        }
    }
}
