using Aw.Kernel.Ids;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Projects;
using Aw.Sindri.Core.Projects.Vocabulary;
using Aw.Sindri.Features.ChangeProject;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Projects;

/// <summary>Changing a project (projects page spec §5.1, §5.3): 'project.changed' at the version the owner saw, keyed by the version it moves to.</summary>
[Lane(TestLane.Unit)]
public sealed class ChangeProjectTests
{
    private readonly ProjectWriteHarness _h = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ChangeProjectRequest Change(ProjectEdit edit, long version = 1) => new(ProjectRows.HandMadeId, version, edit);

    [Fact]
    public async Task The_changed_fields_are_recorded_and_the_version_moves()
    {
        await _h.Lease();
        await _h.SeedHandMade();
        using var human = _h.Human();

        var result = await _h.Change().Handle(Change(new ProjectEdit(State: ProjectState.InProgress, Note: "started", ClearTarget: true)), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        var recorded = Assert.Single(_h.Slice.Store.Events);
        var payload = _h.Payload<ProjectChanged>(recorded);
        Expect.Equal($"project.changed:{ProjectRows.HandMadeId.Value}:v2", recorded.IdempotencyKey);
        Expect.Equal((2L, ProjectState.InProgress, "started", true), (payload.Version, payload.State, payload.Note, payload.ClearTarget));
        Expect.Equal((recorded.Sequence, ProjectRows.HandMadeId.Value, 2L), (result.Value.Accepted.Sequence, result.Value.Project, result.Value.Version));
        Expect.Equal(payload.CommandId, result.Value.Accepted.CommandId);
    }

    [Fact]
    public async Task The_note_and_each_link_are_scrubbed_before_the_event_is_written()
    {
        await _h.Lease();
        await _h.SeedHandMade();
        using var human = _h.Human();

        var result = await _h.Change().Handle(Change(new ProjectEdit(Links: [$"https://example.com/{ProjectWriteHarness.Secret}"], Note: $"key {ProjectWriteHarness.Secret}")), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        var payload = _h.Payload<ProjectChanged>(Assert.Single(_h.Slice.Store.Events));
        Expect.Equal("key [redacted]", payload.Note);
        Expect.Sequence(["https://example.com/[redacted]"], payload.Links ?? []);
    }

    [Fact]
    public async Task A_stale_version_is_SND_PROJECT_409_and_records_nothing()
    {
        await _h.Lease();
        await _h.SeedHandMade();
        using var human = _h.Human();

        var result = await _h.Change().Handle(Change(new ProjectEdit(State: ProjectState.Paused), version: 4), Token);

        Expect.Equal(SindriErrors.ProjectConflict.Code, result.Error.Code);
        Assert.Empty(_h.Slice.Store.Events);
    }

    [Fact]
    public async Task An_unknown_project_is_SND_PROJECT_404()
    {
        await _h.Lease();
        using var human = _h.Human();

        var result = await _h.Change().Handle(Change(new ProjectEdit(State: ProjectState.Paused)), Token);

        Expect.Equal(SindriErrors.ProjectNotFound.Code, result.Error.Code);
    }

    [Fact]
    public async Task A_bad_field_is_SND_PROJECT_004_naming_it()
    {
        await _h.Lease();
        await _h.SeedHandMade();
        using var human = _h.Human();

        var link = await _h.Change().Handle(Change(new ProjectEdit(Links: ["http://example.com"])), Token);
        var nothing = await _h.Change().Handle(Change(new ProjectEdit()), Token);

        Expect.Equal(SindriErrors.ProjectInvalid.Code, link.Error.Code);
        Assert.StartsWith("links:", link.Error.Message, StringComparison.Ordinal);
        Assert.StartsWith("change:", nothing.Error.Message, StringComparison.Ordinal);
        Assert.Empty(_h.Slice.Store.Events);
    }

    [Fact]
    public async Task Losing_the_version_key_race_is_SND_PROJECT_409()
    {
        await _h.Lease();
        await _h.SeedHandMade();
        var rival = new ProjectChanged(_h.Slice.Ids.New<CommandId>(), ProjectRows.HandMadeId, 2, Name: null, ProjectState.Done, ClearState: false, Links: null, Note: null, ClearNote: false, Target: null, ClearTarget: false);
        _ = await _h.Slice.Store.Append(_h.Slice.Events.Entry(EventTypes.ProjectChanged, rival, $"project.changed:{ProjectRows.HandMadeId.Value}:v2"), _h.Slice.Held.Current!, Token);
        using var human = _h.Human();

        var result = await _h.Change().Handle(Change(new ProjectEdit(State: ProjectState.Paused)), Token);

        Expect.Equal(SindriErrors.ProjectConflict.Code, result.Error.Code);
        _ = Assert.Single(_h.Slice.Store.Events);
    }
}
