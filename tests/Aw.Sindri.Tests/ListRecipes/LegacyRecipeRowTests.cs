using Aw.Sindri.Core.Challenges;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Features.RevokeRecipe;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Testing.Workflow.Registry;
using Aw.Sindri.Tests.ApproveRecipe;
using Aw.Sindri.Tests.Workflow;

namespace Aw.Sindri.Tests.ListRecipes;

/// <summary>
/// A registry row from before urgency, outputs and an input's kind (spec §3.1): its text and hash stay as registered,
/// it still lists and shows with the defaults, and the same file registered again hashes differently, so its id and
/// version are taken (SND-RECIPE-007 naming RCP-VERSION-001) until the author bumps the version.
/// </summary>
public sealed class LegacyRecipeRowTests
{
    private static async Task<(RegistryHarness Registry, RegisteredRecipe Row)> Seeded()
    {
        var registry = new RegistryHarness();
        _ = await registry.H.HoldLease();
        var source = LegacyStep();
        var row = new RegisteredRecipe(RecipeId.From("workflow.step"), 1, RecipeDeclarationsTests.LegacyHash(source),
            RecipeDeclarationsTests.LegacyJson(source).ToJsonString(), Base: null, registry.H.Clock.GetUtcNow());
        _ = await registry.Recipes.Add(row, TestContext.Current.CancellationToken);
        return (registry, row);
    }

    /// <summary>workflow.step as it was registered before P2b: the file with its inputs in the bare-key short form it had then.</summary>
    internal static string LegacyStep() => ShippedRecipes.Read("sindri/workflow.step.yaml")
        .Replace("version: 4\n", "version: 1\n", StringComparison.Ordinal)
        .Replace("displayName: \"Do one step, check it and review it\"\n", string.Empty, StringComparison.Ordinal)
        .Replace("inputs:\n  - { key: fact:spec, label: \"Spec\", type: text, kind: text }\n", "inputs: [fact:spec]\n", StringComparison.Ordinal);

    [Fact, Trait("Lane", "unit")]
    public async Task An_old_row_still_lists_with_its_stored_hash_and_the_defaults()
    {
        var (r, row) = await Seeded();
        await using var _ = r;

        var line = await r.Line(row);

        Assert.Equal(row.Hash, line.Hash);
        Assert.Equal("work", line.Urgency);
        Assert.Empty(line.Outputs);
        Assert.Equal(["fact:spec"], line.Inputs.Select(i => i.Key.Value), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_shipped_file_registers_over_an_old_row_because_its_version_moved_past_it()
    {
        var (r, row) = await Seeded();
        await using var _ = r;

        var again = await r.RegisterShipped("sindri/workflow.step.yaml");

        Assert.True(again.IsSuccess, again.IsError ? again.Error.ToString() : null);
        Assert.NotEqual(row.Hash, again.Value.Hash);
        Assert.Equal(2, (await r.List()).Count);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_stale_list_names_an_approved_old_row_as_legacy_shape()
    {
        var (r, row) = await Seeded();
        await using var _ = r;
        await r.ApproveAsInThePast(row);

        var stale = Assert.Single(await r.List(stale: true));

        Assert.Equal(row.Hash, stale.Hash);
        Assert.Equal([StaleReason.LegacyShape], stale.Stale);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_current_shipped_row_is_not_stale()
    {
        await using var r = new RegistryHarness();
        _ = await r.H.HoldLease();
        var current = await r.Shipped("sindri/workflow.step.yaml");
        _ = await r.Approve(current);

        Assert.Empty((await r.Line(current)).Stale);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_listing_of_a_new_recipe_carries_its_urgency_and_outputs()
    {
        await using var r = new RegistryHarness();
        _ = await r.H.HoldLease();
        var yaml = RecipeReaderTests.Minimal.Replace("initial: produce\n", "displayName: Minimal recipe\nurgency: incident\ninputs: [{ key: fact:item, type: item, kind: item }]\noutputs: [{ key: fact:output, type: file, from: produce }]\ninitial: produce\n", StringComparison.Ordinal);
        var registered = await r.RegisterText(yaml, "minimal.yaml");
        Assert.True(registered.IsSuccess, registered.IsError ? registered.Error.ToString() : null);

        var line = Assert.Single(await r.List());

        Assert.Equal("incident", line.Urgency);
        Assert.Equal(["fact:output"], line.Outputs.Select(o => o.Key.Value), StringComparer.Ordinal);
        Assert.Equal("produce", Assert.Single(line.Outputs).From);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_listing_of_a_recipe_carries_its_path_order_and_loops()
    {
        await using var r = new RegistryHarness();
        _ = await r.H.HoldLease();
        var registered = await r.RegisterShipped("sindri/workflow.step.yaml");
        Assert.True(registered.IsSuccess, registered.IsError ? registered.Error.ToString() : null);

        var line = Assert.Single(await r.List());

        Assert.Equal("path", line.OrderedBy);
        Assert.Equal(["produce=1", "checks=2", "verify=3", "done=4"], line.States.Select(s => $"{s.State}={s.Order?.ToString(System.Globalization.CultureInfo.InvariantCulture)}"), StringComparer.Ordinal);
        Assert.Equal(["verify>produce", "checks>produce"], line.Loops.Select(l => $"{l.From}>{l.To}"), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_legacy_hash_of_workflow_step_is_the_one_it_was_registered_under()
    {
        Assert.Equal(
            "sha256:d828d91407649c3a280b476cc7c33bf6726ea755f08a001a8a90fd149ecebd31",
            RecipeDeclarationsTests.LegacyHash(LegacyStep()).Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_approved_old_row_lists_approved_and_legacy_shape_stale_shows_its_preview_and_revokes_by_its_stored_hash()
    {
        var (r, row) = await Seeded();
        await using var _ = r;

        await r.ApproveAsInThePast(row);
        var line = await r.Line(row);
        var shown = await r.Preview(row.Id, row.Hash.Display);
        var issued = (await r.Issue($"revoke workflow.step@{row.Hash.Display}")).Value;
        var revoked = await r.Run<RevokeRecipeRequest, RevokeRecipeResponse>(
            new RevokeRecipeRequest(row.Id, row.Hash, new ChallengeAnswer(issued.Challenge, issued.Action)), RegistryHarness.Human);

        Assert.Equal(RecipeStanding.Approved, line.Standing);
        Assert.Equal([StaleReason.LegacyShape], line.Stale);
        Assert.True(shown.IsSuccess, shown.IsError ? shown.Error.ToString() : null);
        Assert.Equal(row.Hash, shown.Value.Hash);
        Assert.True(revoked.IsSuccess, revoked.IsError ? revoked.Error.ToString() : null);
        Assert.Equal(row.Hash, revoked.Value.Revocation.Hash);
        Assert.Equal(RecipeStanding.Revoked, (await r.Line(row)).Standing);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_diff_against_an_approved_old_row_shows_only_what_the_author_changed()
    {
        var (r, row) = await Seeded();
        await using var _ = r;
        await r.ApproveAsInThePast(row);
        var next = await r.Register(RecipeReaderTests.Bind(LegacyStep()) with { Version = 2, Description = "A reworded step." });

        var shown = await r.Preview(next.Id, next.Hash.Display);

        Assert.True(shown.IsSuccess, shown.IsError ? shown.Error.ToString() : null);
        var changed = shown.Value.Diff!.Split('\n').Where(l => (l.StartsWith('+') || l.StartsWith('-')) && !l.StartsWith("+++", StringComparison.Ordinal) && !l.StartsWith("---", StringComparison.Ordinal)).ToList();
        Assert.Contains(changed, l => l.Contains("A reworded step.", StringComparison.Ordinal));
        Assert.DoesNotContain(changed, l => l.Contains("urgency", StringComparison.Ordinal) || l.Contains("outputs", StringComparison.Ordinal) || l.Contains("\"kind\": null", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Approving_a_legacy_shape_row_is_refused_naming_the_fix_and_records_no_approval()
    {
        var (r, row) = await Seeded();
        await using var _ = r;

        var approved = await r.Issue(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"approve workflow.step {row.Version}"));   // the challenge's preview is the planner's, so it refuses first

        Assert.True(approved.IsError);
        Assert.Equal(WorkflowErrors.RecipeInvalid.Code, approved.Error.Code);
        Assert.Contains(SubjectKinds.LegacyFix, approved.Error.Message, StringComparison.Ordinal);
        Assert.Equal(RecipeStanding.Unapproved, (await r.Line(row)).Standing);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_unapproved_legacy_shape_row_is_marked_legacy_shape_in_the_list_too()
    {
        var (r, row) = await Seeded();
        await using var _ = r;

        var line = await r.Line(row);

        Assert.Equal(RecipeStanding.Unapproved, line.Standing);
        Assert.Equal([StaleReason.LegacyShape], line.Stale);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Approving_an_already_approved_legacy_shape_row_with_drifted_pins_still_completes_and_resumes_a_parked_instance()
    {
        var (r, row) = await Seeded();
        await using var disposable = r;
        _ = await r.Approvals.Approve(RegistryData.Approval(row, new RecipePins([new BlockPin(BlockId.From("step.produce"), RegistryData.Descriptor("old"))], [])), TestContext.Current.CancellationToken);
        var parked = WorkflowInstanceId.For(new Guid(7, 0, 0, new byte[8]));
        r.Wake.Run(row.Hash, parked);

        var approved = await r.Approve(row);   // the drift path: the stored pins differ from this build's, so it records a new approval

        Assert.True(approved.IsSuccess, approved.IsError ? approved.Error.ToString() : null);
        Assert.Equal([parked], approved.Value.Resumed);
        var recorded = Assert.Single(r.Events(EventTypes.RecipeApproved));
        Assert.True(r.H.Events.Payload<RecipeApproved>(recorded).Value.Drift);
        Assert.Equal(2, (await r.Approvals.History(row.Id, row.Version, row.Hash, TestContext.Current.CancellationToken)).Value.Count);
        Assert.Equal(RecipeWakeKind.Approved, Assert.Single(r.Wake.Sent).Kind);
        Assert.Equal(RecipeStanding.Approved, (await r.Line(row)).Standing);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_legacy_shape_row_without_evals_is_refused_as_legacy_with_a_fix_that_works_not_as_missing_evals()
    {
        await using var r = new RegistryHarness();
        _ = await r.H.HoldLease();
        var node = RecipeDeclarationsTests.LegacyJson(LegacyStep());
        node["evals"] = new System.Text.Json.Nodes.JsonArray();
        var row = new RegisteredRecipe(RecipeId.From("workflow.step"), 1, RecipeHash.Of(CanonicalJson.Of(node)), node.ToJsonString(), Base: null, r.H.Clock.GetUtcNow());
        _ = await r.Recipes.Add(row, TestContext.Current.CancellationToken);

        var approved = await r.Issue(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"approve workflow.step {row.Version}"));

        Assert.True(approved.IsError);
        Assert.Equal(WorkflowErrors.RecipeInvalid.Code, approved.Error.Code);
        Assert.Contains(SubjectKinds.LegacyFix, approved.Error.Message, StringComparison.Ordinal);
    }
}
