using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Features.PinConcepts;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.PinConcepts;

/// <summary>The PinConcepts job (concept pins spec §3.6, "The write"): it writes pins into 'index.concepts', returns the new hash and never approves; every refusal comes before the write.</summary>
[Lane(TestLane.Unit)]
public sealed class PinConceptsServiceTests : IDisposable
{
    private readonly PinConceptsRig _rig = new();

    private static CancellationToken Token => PinConceptsRig.Token;

    public void Dispose() => _rig.Dispose();

    private static ConceptSettings Pin(string id, string name, string? summary = null, string[]? aliases = null, string[]? features = null, string[]? paths = null, string[]? dependsOn = null, string[]? owns = null) =>
        new(ConceptPinId.From(id), name, summary, features, paths, dependsOn, owns, aliases);

    private async Task<Aw.Kernel.Result<PinConceptsResult>> Run(PinConceptsRequest request)
    {
        await _rig.Lease();
        return await _rig.Service().Handle(request, Token);
    }

    [Fact]
    public async Task Pin_concepts_writes_the_entry_and_returns_the_new_hash_without_approving()
    {
        var done = await Run(new PinConceptsRequest(Set: [new PinSet("Billing", Summary: "How we take money", Aliases: ["payments"])]));

        Assert.True(done.IsSuccess);
        Assert.Equal([new PinWritten(ConceptPinId.From("billing"), "Billing", "created")], done.Value.Pins);
        Assert.False(done.Value.Approved);
        Assert.Matches("^[0-9a-f]{64}$", done.Value.Hash.Value);
        Assert.Equal(_rig.Writer.Hash, done.Value.Hash);
        var edit = Assert.Single(_rig.Edits);
        Assert.Equal(new ConceptSettings(ConceptPinId.From("billing"), "Billing", "How we take money", Aliases: ["payments"]).Id, edit.Entry?.Id);
        Assert.Equal(("Billing", "How we take money", ["payments"]), (edit.Entry?.Name, edit.Entry?.Summary, edit.Entry?.Aliases));
    }

    [Fact]
    public async Task A_list_of_three_is_one_write()
    {
        var done = await Run(new PinConceptsRequest(Set: [new PinSet("Billing"), new PinSet("Auth"), new PinSet("Search")]));

        _ = Assert.Single(_rig.Writer.Writes);
        Assert.Equal(["billing", "auth", "search"], [.. _rig.Edits.Select(static e => e.Id.Value)]);
        Assert.Equal(["created", "created", "created"], [.. done.Value.Pins.Select(static p => p.Action)]);
    }

    [Fact]
    public async Task Edit_keeps_the_id_when_the_name_changes()
    {
        _rig.Profile([Pin("billing", "Billing", "How we take money")]);

        var done = await Run(new PinConceptsRequest(Set: [new PinSet("Payments and billing", Id: ConceptPinId.From("billing"))]));

        Assert.Equal([new PinWritten(ConceptPinId.From("billing"), "Payments and billing", "updated")], done.Value.Pins);
        var entry = Assert.Single(_rig.Edits).Entry;
        Assert.Equal(("billing", "Payments and billing"), (entry?.Id?.Value, entry?.Name));
    }

    [Fact]
    public async Task A_name_whose_id_is_a_pin_edits_that_pin()
    {
        _rig.Profile([Pin("billing", "Billing")]);

        var done = await Run(new PinConceptsRequest(Set: [new PinSet("billing", Summary: "Money in")]));

        Assert.Equal("updated", Assert.Single(done.Value.Pins).Action);
    }

    [Fact]
    public async Task Omitted_fields_keep_the_stored_values_and_a_given_list_replaces()
    {
        _rig.Profile([Pin("billing", "Billing", "Stored line", aliases: ["payments", "invoicing"], features: ["feature:billing.refunds"])]);

        _ = await Run(new PinConceptsRequest(Set: [new PinSet("Billing", Aliases: ["charges"])]));

        var entry = Assert.Single(_rig.Edits).Entry;
        Assert.Equal("Stored line", entry?.Summary);
        Assert.Equal(["charges"], entry?.Aliases);
        Assert.Equal(["feature:billing.refunds"], entry?.Features);
    }

    [Fact]
    public async Task An_empty_list_clears_the_stored_one()
    {
        _rig.Profile([Pin("billing", "Billing", aliases: ["payments"])]);

        _ = await Run(new PinConceptsRequest(Set: [new PinSet("Billing", Aliases: [])]));

        Assert.Null(Assert.Single(_rig.Edits).Entry?.Aliases);
    }

    [Fact]
    public async Task Paths_depends_on_and_owns_survive_an_app_edit()
    {
        _rig.Profile([Pin("auth", "Auth"), Pin("billing", "Billing", paths: ["src/billing/**"], dependsOn: ["auth"], owns: ["invoices"])]);

        _ = await Run(new PinConceptsRequest(Set: [new PinSet("Billing", Summary: "Money in")]));

        var entry = Assert.Single(_rig.Edits).Entry;
        Assert.Equal((["src/billing/**"], ["auth"], ["invoices"]), (entry?.Paths, entry?.DependsOn, entry?.Owns));
    }

    [Fact]
    public async Task Removing_a_pin_with_folder_rules_drops_them_and_lists_them()
    {
        _rig.Profile([Pin("billing", "Billing", paths: ["src/billing/**", "src/pay/**"], owns: ["invoices"])]);

        var done = await Run(new PinConceptsRequest(Remove: [ConceptPinId.From("billing")], WithFolderRules: true));

        Assert.Equal([new PinWritten(ConceptPinId.From("billing"), "Billing", "removed")], done.Value.Pins);
        Assert.Equal(["billing.paths", "billing.owns"], done.Value.Dropped);
        var edit = Assert.Single(_rig.Edits);
        Assert.Equal(("billing", null), (edit.Id.Value, edit.Entry));
    }

    [Fact]
    public async Task Removing_a_pin_without_folder_rules_needs_no_flag()
    {
        _rig.Profile([Pin("billing", "Billing", summary: "Money in")]);

        var done = await Run(new PinConceptsRequest(Remove: [ConceptPinId.From("billing")]));

        Assert.True(done.IsSuccess);
        Assert.Empty(done.Value.Dropped);
    }

    [Fact]
    public async Task A_name_the_rules_refuse_is_171_with_its_key_path_and_nothing_is_written()
    {
        _rig.Profile([Pin("auth", "Auth")]);

        var done = await Run(new PinConceptsRequest(Set: [new PinSet("Billing"), new PinSet(new string('x', 201), Id: ConceptPinId.From("long"))]));

        Assert.Equal("SND-CONTEXT-171", done.Error.Code);
        Assert.StartsWith("index.concepts[2].name: ", done.Error.Message, StringComparison.Ordinal);
        Assert.Empty(_rig.Writer.Writes);
    }

    [Fact]
    public async Task A_feature_that_is_not_in_the_map_is_172_and_nothing_is_written()
    {
        _rig.Map.Has.Add("feature:billing.refunds");

        var done = await Run(new PinConceptsRequest(Set: [new PinSet("Billing", Features: ["feature:billing.refunds", "feature:billing.refundz"])]));

        Assert.Equal("SND-CONTEXT-172", done.Error.Code);
        Assert.Equal("feature:billing.refundz is not in map v41 (fix: pick it from context_concepts or the feature picker)", done.Error.Message);
        Assert.Empty(_rig.Writer.Writes);
    }

    [Fact]
    public async Task A_feature_with_no_map_built_is_172_and_says_so()
    {
        _rig.Map.Version = null;

        var done = await Run(new PinConceptsRequest(Set: [new PinSet("Billing", Features: ["feature:billing.refunds"])]));

        Assert.Equal("SND-CONTEXT-172", done.Error.Code);
        Assert.Contains("no map is built yet", done.Error.Message, StringComparison.Ordinal);
        Assert.Empty(_rig.Writer.Writes);
    }

    [Fact]
    public async Task Pins_without_features_do_not_read_the_map()
    {
        _rig.Map.Version = null;

        var done = await Run(new PinConceptsRequest(Set: [new PinSet("Billing", Summary: "Money in")]));

        Assert.True(done.IsSuccess);
        Assert.Equal(0, _rig.Map.Asked);
    }

    [Fact]
    public async Task A_feature_glob_is_not_looked_up_in_the_map()
    {
        var done = await Run(new PinConceptsRequest(Set: [new PinSet("Billing", Features: ["feature:billing.*"])]));

        Assert.True(done.IsSuccess);
        Assert.Equal(0, _rig.Map.Asked);
    }

    [Fact]
    public async Task Two_names_that_make_one_id_are_173_in_the_start_check_and_in_the_job()
    {
        var request = new PinConceptsRequest(Set: [new PinSet("Billing "), new PinSet("billing")]);

        var precheck = await PinPlanner.Precheck(request, Token);
        var done = await Run(request);

        Assert.Equal("SND-CONTEXT-173", precheck.Error.Code);
        Assert.Equal("'Billing ' and 'billing' make the same id billing (fix: --id, or rename one)", precheck.Error.Message);
        Assert.Equal("SND-CONTEXT-173", done.Error.Code);
        Assert.Empty(_rig.Writer.Writes);
    }

    [Fact]
    public async Task An_empty_request_is_refused_before_anything_is_read()
    {
        var precheck = await PinPlanner.Precheck(new PinConceptsRequest(), Token);

        Assert.Equal("SND-CONTEXT-171", precheck.Error.Code);
    }

    [Fact]
    public async Task Removing_an_unknown_id_is_174_and_names_the_nearest()
    {
        _rig.Profile([Pin("billing", "Billing"), Pin("auth", "Auth")]);

        var done = await Run(new PinConceptsRequest(Remove: [ConceptPinId.From("bilin")]));

        Assert.Equal("SND-CONTEXT-174", done.Error.Code);
        Assert.Equal("no pin is named 'bilin' in the profile (fix: try billing, auth)", done.Error.Message);
        Assert.Empty(_rig.Writer.Writes);
    }

    [Fact]
    public async Task Removing_a_pin_with_folder_rules_is_175_without_the_flag_and_counts_every_rule()
    {
        _rig.Profile([Pin("billing", "Billing", paths: ["src/billing/**"], owns: ["invoices"])]);

        var done = await Run(new PinConceptsRequest(Remove: [ConceptPinId.From("billing")]));

        Assert.Equal("SND-CONTEXT-175", done.Error.Code);
        Assert.Equal("pin billing has 2 folder rules set outside the app (fix: --with-folder-rules to drop them too)", done.Error.Message);
        Assert.Empty(_rig.Writer.Writes);
    }

    [Fact]
    public async Task Removing_a_pin_only_a_repos_own_profile_holds_is_176()
    {
        _rig.Profile(pins: null, new RepoEntry("api", "/work/api", Index: new IndexSettings(Concepts: [Pin("billing", "Billing")])));

        var done = await Run(new PinConceptsRequest(Remove: [ConceptPinId.From("billing")]));

        Assert.Equal("SND-CONTEXT-176", done.Error.Code);
        Assert.Equal("pin billing is set in repo api's own profile (fix: edit it there)", done.Error.Message);
        Assert.Empty(_rig.Writer.Writes);
    }

    [Fact]
    public async Task The_callers_profile_hash_goes_to_the_writer_which_writes_when_it_still_holds()
    {
        var done = await Run(new PinConceptsRequest(Set: [new PinSet("Billing")], IfHash: _rig.Writer.Hash));

        Assert.True(done.IsSuccess);
        _ = Assert.Single(_rig.Writer.Writes);
    }

    [Fact]
    public async Task A_moved_file_is_the_writers_003_unchanged_and_no_event_is_recorded()
    {
        var done = await Run(new PinConceptsRequest(Set: [new PinSet("Billing")], IfHash: ProfileHashing.Of("read"u8)));

        Assert.Equal("SND-PROFILE-003", done.Error.Code);
        Assert.Empty(_rig.Writer.Writes);
        Assert.DoesNotContain(_rig.Slice.Store.Events, static e => e.Envelope.Type == EventTypes.ContextPinWritten);
    }

    [Fact]
    public async Task The_event_names_the_actor_the_ids_and_the_hash()
    {
        _rig.CallerIs(new CallerContextBuilder().Launched().Build());
        _rig.Profile([Pin("auth", "Auth")]);

        _ = await Run(new PinConceptsRequest(Set: [new PinSet("Billing")], Remove: [ConceptPinId.From("auth")]));

        var recorded = Assert.Single(_rig.Slice.Store.Events, static e => e.Envelope.Type == EventTypes.ContextPinWritten);
        Assert.Equal("context.pin.written:" + _rig.Writer.Hash.Value, recorded.IdempotencyKey);
        var payload = _rig.Slice.Events.Payload<ContextPinWritten>(recorded).Value;
        Assert.StartsWith("session:", payload.Actor, StringComparison.Ordinal);
        Assert.True(payload.Launch.HasValue);
        Assert.Equal(["billing:created", "auth:removed"], payload.Pins);
        Assert.Equal(_rig.Writer.Hash, payload.Hash);
    }

    [Fact]
    public async Task The_job_says_checking_and_then_written_to_the_profile()
    {
        _ = await Run(new PinConceptsRequest(Set: [new PinSet("Billing")]));

        Assert.Equal(["checking", "written to the profile"], _rig.Progress.Seen);
    }

    [Fact]
    public async Task Without_the_lease_nothing_is_written()
    {
        var done = await _rig.Service().Handle(new PinConceptsRequest(Set: [new PinSet("Billing")]), Token);

        Assert.Equal("SND-API-001", done.Error.Code);
        Assert.Empty(_rig.Writer.Writes);
    }

    [Fact]
    public async Task The_admission_refuses_what_the_profile_refuses_before_the_job_is_claimed()
    {
        _rig.Profile([Pin("billing", "Billing", paths: ["src/billing/**"])]);

        var refused = await _rig.Admission().Admit(new PinConceptsRequest(Remove: [ConceptPinId.From("billing")]), Token);
        var admitted = await _rig.Admission().Admit(new PinConceptsRequest(Remove: [ConceptPinId.From("billing")], WithFolderRules: true), Token);

        Assert.Equal("SND-CONTEXT-175", refused.Error.Code);
        Assert.True(admitted.IsSuccess);
    }

    [Fact]
    public async Task An_edit_of_an_entry_with_no_id_carries_its_name_so_the_writer_replaces_it()
    {
        _rig.Profile([new ConceptSettings(Name: "Billing", Summary: "Hand written")]);

        var done = await Run(new PinConceptsRequest(Set: [new PinSet("Billing", Aliases: ["payments"])]));

        Assert.Equal("updated", Assert.Single(done.Value.Pins).Action);
        var edit = Assert.Single(_rig.Edits);
        Assert.Equal(("billing", "Billing", "Hand written"), (edit.Id.Value, edit.MatchName, edit.Entry?.Summary));
    }

    [Fact]
    public async Task A_removal_of_an_entry_with_no_id_carries_its_name_and_an_edit_by_id_does_not()
    {
        _rig.Profile([new ConceptSettings(Name: "Billing"), Pin("auth", "Auth")]);

        _ = await Run(new PinConceptsRequest(Set: [new PinSet("Auth", Summary: "Who")], Remove: [ConceptPinId.From("billing")]));

        Assert.Equal(["auth", "billing"], [.. _rig.Edits.Select(static e => e.Id.Value)]);
        Assert.Null(_rig.Edits[0].MatchName);
        Assert.Equal("Billing", _rig.Edits[1].MatchName);
    }
}
