using System.Text;
using Aw.Evidence.Core.Guard;
using Aw.Evidence.Core.Settings;
using Aw.Evidence.Core.Stack;
using Aw.Evidence.Features.CheckProvenance;
using Aw.Evidence.Testing;

namespace Aw.Evidence.Tests.Stack;

public sealed class CheckProvenanceTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task CheckProvenance_is_seeded_under_the_configured_seed_policy()
    {
        var query = new FakeProvenanceQuery();
        query.Emails.Add("seed-1@seed.test");
        query.Emails.Add("staff@seed.test");
        var settings = new FakeEvidenceSettings { SeedPolicy = new SeedPolicy(["seed.test"], "seed-%@seed.test") };

        var result = await new CheckProvenanceService(query, settings, new InMemoryRunFiles()).Handle(new CheckProvenanceRequest(), CancellationToken.None);

        Assert.Equal(DbProvenance.Seeded, result.Value.Provenance);
        Assert.Equal(["seed-%@seed.test"], query.Patterns, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task CheckProvenance_is_unknown_with_the_reason_when_the_query_fails()
    {
        var query = new FakeProvenanceQuery { Failure = "docker: no such container" };

        var result = await new CheckProvenanceService(query, new FakeEvidenceSettings(), new InMemoryRunFiles()).Handle(new CheckProvenanceRequest(), CancellationToken.None);

        Assert.Equal((DbProvenance.Unknown, "docker: no such container"), (result.Value.Provenance, result.Value.Reason));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task CheckProvenance_queries_the_worktree_slots_database_and_the_default_one_without_a_worktree()
    {
        var (query, settings, files) = SlotSetup("OTHER=1\nSTACK_SLOT=2\n");

        _ = await new CheckProvenanceService(query, settings, files).Handle(new CheckProvenanceRequest("/work/wt/"), CancellationToken.None);
        _ = await new CheckProvenanceService(query, settings, files).Handle(new CheckProvenanceRequest(), CancellationToken.None);

        Assert.Equal(["app_2", "app_2", null, null], query.Databases);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task CheckProvenance_in_a_worktree_without_a_usable_slot_is_unknown_and_never_queries_the_main_database()
    {
        var (query, settings, files) = SlotSetup("STACK_SLOT=9\n");

        var result = await new CheckProvenanceService(query, settings, files).Handle(new CheckProvenanceRequest("/work/wt"), CancellationToken.None);

        Assert.Equal((DbProvenance.Unknown, "/work/wt/.stack-slot.env has no STACK_SLOT between 1 and 3"), (result.Value.Provenance, result.Value.Reason));
        Assert.Empty(query.Databases);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task CheckProvenance_in_a_worktree_slot_with_no_database_template_is_unknown_and_never_queries_the_main_database()
    {
        var (query, settings, files) = SlotSetup("STACK_SLOT=2\n", database: null);

        var result = await new CheckProvenanceService(query, settings, files).Handle(new CheckProvenanceRequest("/work/wt"), CancellationToken.None);

        Assert.Equal((DbProvenance.Unknown, "no slot database configured (set UI_EVIDENCE_SLOT_DATABASE, e.g. app_{slot}; see SKILL.md \"Worktree stacks\")"), (result.Value.Provenance, result.Value.Reason));
        Assert.Empty(query.Databases);
    }

    private static (FakeProvenanceQuery Query, FakeEvidenceSettings Settings, InMemoryRunFiles Files) SlotSetup(string slotFile, string? database = "app_{slot}")
    {
        var query = new FakeProvenanceQuery();
        query.Emails.Add("seed-1@seed.test");
        var settings = new FakeEvidenceSettings
        {
            SeedPolicy = new SeedPolicy(["seed.test"], "seed-%@seed.test"),
            Slots = new SlotSettings(".stack-slot.env", "STACK_SLOT", Count: 3, PortStep: 10, Database: database),
        };
        var files = new InMemoryRunFiles().With("/work/wt/.stack-slot.env", Encoding.UTF8.GetBytes(slotFile));
        return (query, settings, files);
    }
}
