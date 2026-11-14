using System.Text.Json.Nodes;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Xunit;

namespace Aw.Sindri.Testing.Workflow;

/// <summary>
/// The contract every <see cref="IWorkflowStore"/> honours (issue 2258, PR 5): a save is a new revision and never changes an
/// old one, a save on a revision that is no longer the latest is a conflict, activating pins a revision, archiving hides.
/// </summary>
public abstract class WorkflowStoreContract
{
    private static readonly WorkflowId _id = WorkflowId.From("WF-00000000000000000000000000000001");
    private static readonly WorkflowSlug _slug = WorkflowSlug.From("contract-drift");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A fresh store with no workflows.</summary>
    /// <returns>The store.</returns>
    protected abstract IWorkflowStore Create();

    private static GraphSpec Spec(string name) =>
        new(Version: 1, Name: name, Description: null, Concurrency: null, Resources: null,
            Nodes: [new GraphNode(NodeId.From("daily"), "schedule", Title: null, new JsonObject { ["cron"] = "0 7 * * 1-5" })], Edges: []);

    /// <summary>A workflow starts at revision 1 and each save adds the next.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_save_on_the_latest_revision_adds_the_next_and_keeps_the_old_one()
    {
        var store = Create();
        _ = (await store.Create(_id, _slug, Spec("one"), "owner", Token)).Value;

        var saved = await store.Save(_id, 1, Spec("two"), "session:s1", Token);

        Expect.Equal(2, saved.Value.Revision);
        Expect.Equal("two", (await store.GetRevision(_id, revision: null, Token)).Value.Spec.Name);
        Expect.Equal("one", (await store.GetRevision(_id, 1, Token)).Value.Spec.Name);
        Expect.Equal(2, (await store.Get(_id, Token)).Value.LatestRevision);
    }

    /// <summary>A save made on a revision someone else already replaced is refused, and nothing is written.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_save_with_a_stale_base_revision_is_a_conflict()
    {
        var store = Create();
        _ = (await store.Create(_id, _slug, Spec("one"), "owner", Token)).Value;
        _ = (await store.Save(_id, 1, Spec("two"), "owner", Token)).Value;

        var stale = await store.Save(_id, 1, Spec("three"), "session:s1", Token);

        Expect.Equal(SindriErrors.GraphStaleRevision.Code, stale.Error.Code);
        Expect.Equal(2, (await store.Get(_id, Token)).Value.LatestRevision);
    }

    /// <summary>A slug belongs to one workflow.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_second_workflow_with_the_same_slug_is_refused()
    {
        var store = Create();
        _ = (await store.Create(_id, _slug, Spec("one"), "owner", Token)).Value;

        var other = await store.Create(WorkflowId.From("WF-00000000000000000000000000000002"), _slug, Spec("two"), "owner", Token);

        Expect.Equal(SindriErrors.GraphSlugTaken.Code, other.Error.Code);
    }

    /// <summary>A live taker is named "another workflow" and the text speaks of a short id, not a slug.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_slug_taken_by_a_live_workflow_says_so_in_plain_words()
    {
        var store = Create();
        _ = (await store.Create(_id, _slug, Spec("one"), "owner", Token)).Value;

        var other = await store.Create(WorkflowId.From("WF-00000000000000000000000000000002"), _slug, Spec("two"), "owner", Token);

        Expect.Equal("The short id 'contract-drift' is taken by another workflow. Pick another.", other.Error.Message);
    }

    /// <summary>An archived taker is named as archived, because the list hides it.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_slug_taken_by_an_archived_workflow_says_it_is_archived()
    {
        var store = Create();
        _ = (await store.Create(_id, _slug, Spec("one"), "owner", Token)).Value;
        _ = (await store.Archive(_id, Token)).Value;

        var other = await store.Create(WorkflowId.From("WF-00000000000000000000000000000002"), _slug, Spec("two"), "owner", Token);

        Expect.Equal(SindriErrors.GraphSlugTaken.Code, other.Error.Code);
        Expect.Equal("The short id 'contract-drift' is taken by an archived workflow. Pick another.", other.Error.Message);
    }

    /// <summary>Pausing clears the active revision and keeps every revision; pausing an unknown workflow is not found.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Pausing_clears_the_active_revision_and_an_unknown_workflow_is_not_found()
    {
        var store = Create();
        _ = (await store.Create(_id, _slug, Spec("one"), "owner", Token)).Value;
        _ = (await store.Activate(_id, 1, Token)).Value;

        var paused = (await store.Pause(_id, Token)).Value;

        Expect.True(paused.ActiveRevision is null);
        Expect.Equal(1, paused.LatestRevision);
        Expect.Equal(SindriErrors.GraphNotFound.Code, (await store.Pause(WorkflowId.From("WF-00000000000000000000000000000009"), Token)).Error.Code);
    }

    /// <summary>Activating pins the revision triggers run; an unknown revision is not found.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Activating_pins_a_revision_and_an_unknown_one_is_not_found()
    {
        var store = Create();
        _ = (await store.Create(_id, _slug, Spec("one"), "owner", Token)).Value;
        _ = (await store.Save(_id, 1, Spec("two"), "owner", Token)).Value;

        var pinned = await store.Activate(_id, 1, Token);
        var missing = await store.Activate(_id, 9, Token);

        Expect.Equal(1, pinned.Value.ActiveRevision);
        Expect.Equal(SindriErrors.GraphNotFound.Code, missing.Error.Code);
        Expect.Equal(1, (await store.Get(_id, Token)).Value.ActiveRevision);
    }

    /// <summary>An archived workflow leaves the list, turns off, and is still readable by id.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Archiving_hides_a_workflow_from_the_list_and_turns_it_off()
    {
        var store = Create();
        _ = (await store.Create(_id, _slug, Spec("one"), "owner", Token)).Value;
        _ = (await store.Activate(_id, 1, Token)).Value;

        var archived = await store.Archive(_id, Token);

        Expect.True(archived.Value.ArchivedAt is not null);
        Expect.True(archived.Value.ActiveRevision is null);
        Expect.True((await store.List(includeArchived: false, Token)).Value.Count == 0);
        Expect.Equal(_slug, Expect.Only((await store.List(includeArchived: true, Token)).Value).Slug);
    }

    /// <summary>An id that is already used is refused before anything is written, so the workflow keeps its revisions.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Create_with_a_used_id_is_refused_and_keeps_the_revisions()
    {
        var store = Create();
        _ = (await store.Create(_id, _slug, Spec("one"), "owner", Token)).Value;
        _ = (await store.Save(_id, 1, Spec("two"), "owner", Token)).Value;

        var again = await store.Create(_id, WorkflowSlug.From("another-slug"), Spec("three"), "owner", Token);

        Expect.Equal(SindriErrors.GraphIdTaken.Code, again.Error.Code);
        Expect.Equal(2, (await store.Get(_id, Token)).Value.LatestRevision);
        Expect.Equal("one", (await store.GetRevision(_id, 1, Token)).Value.Spec.Name);
        Expect.Equal(_slug, (await store.Get(_id, Token)).Value.Slug);
    }

    /// <summary>Archiving turns a workflow off for good, so activating it again is refused and nothing is pinned.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Activate_on_an_archived_workflow_is_refused()
    {
        var store = Create();
        _ = (await store.Create(_id, _slug, Spec("one"), "owner", Token)).Value;
        _ = (await store.Archive(_id, Token)).Value;

        var pinned = await store.Activate(_id, 1, Token);

        Expect.Equal(SindriErrors.GraphArchived.Code, pinned.Error.Code);
        Expect.True((await store.Get(_id, Token)).Value.ActiveRevision is null);
    }

    /// <summary>A save on an archived workflow is refused and adds no revision (a delete can land between a service's read and its save).</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Save_on_an_archived_workflow_is_refused_and_adds_no_revision()
    {
        var store = Create();
        _ = (await store.Create(_id, _slug, Spec("one"), "owner", Token)).Value;
        _ = (await store.Archive(_id, Token)).Value;

        var saved = await store.Save(_id, 1, Spec("two"), "owner", Token);

        Expect.Equal(SindriErrors.GraphArchived.Code, saved.Error.Code);
        Expect.Equal(1, (await store.Get(_id, Token)).Value.LatestRevision);
    }

    /// <summary>A workflow nobody created is not found.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task An_unknown_workflow_is_not_found()
    {
        var store = Create();

        Expect.Equal(SindriErrors.GraphNotFound.Code, (await store.Get(_id, Token)).Error.Code);
    }

    /// <summary>Activate sets WasActivated, Pause keeps it with no active revision, and a never-activated workflow has it false (#3683).</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Was_activated_is_set_by_activate_and_kept_by_pause()
    {
        var store = Create();
        _ = (await store.Create(_id, _slug, Spec("one"), "owner", Token)).Value;
        Expect.Equal(false, (await store.Get(_id, Token)).Value.WasActivated);

        var active = (await store.Activate(_id, 1, Token)).Value;
        var paused = (await store.Pause(_id, Token)).Value;

        Expect.Equal(true, active.WasActivated);
        Expect.Equal(true, paused.WasActivated);
        Expect.Equal(null, paused.ActiveRevision);
        Expect.Equal(true, (await store.Get(_id, Token)).Value.WasActivated);
    }
}
