using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Projects;
using Aw.Sindri.Core.Projects.Vocabulary;
using Aw.Sindri.Features.ChangeProject;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Projects;

/// <summary>Notes on a Linear project the owner leads (owner q158): 'project.led.noted' through the change route, on the hide row's version.</summary>
[Lane(TestLane.Unit)]
public sealed class LedProjectNotesTests
{
    private readonly ProjectWriteHarness _h = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ChangeProjectRequest Change(ProjectEdit edit, long version = 0, string project = ProjectRows.LinearKey) => new(project, version, edit);

    [Fact]
    public async Task Notes_on_a_led_project_are_recorded_and_the_card_version_moves_to_one()
    {
        await _h.Lease();
        _h.OwnerLeads();
        using var human = _h.Human();

        var result = await _h.Change().Handle(Change(new ProjectEdit(Notes: "call the vendor\nthen the bank")), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        var recorded = Assert.Single(_h.Slice.Store.Events);
        var payload = _h.Payload<ProjectLedNoted>(recorded);
        Expect.Equal(($"project.led.noted:{ProjectRows.LinearKey}:v1", EventTypes.ProjectLedNoted), (recorded.IdempotencyKey, recorded.Envelope.Type));
        Expect.Equal((ProjectRows.LinearKey, "call the vendor\nthen the bank", 1L), (payload.Linear, payload.Notes, payload.Version));
        Expect.Equal((recorded.Sequence, ProjectRows.LinearKey, 1L), (result.Value.Accepted.Sequence, result.Value.Project, result.Value.Version));
    }

    [Fact]
    public async Task A_hide_then_a_notes_save_goes_from_version_one_to_two()
    {
        await _h.Lease();
        _h.OwnerLeads();
        await _h.SeedHidden(version: 1, hidden: true);
        using var human = _h.Human();

        var result = await _h.Change().Handle(Change(new ProjectEdit(Notes: "x"), version: 1), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        Expect.Equal(2L, result.Value.Version);
    }

    [Fact]
    public async Task Clearing_the_notes_records_null_notes()
    {
        await _h.Lease();
        _h.OwnerLeads();
        await _h.SeedLedNotes(version: 1, notes: "old");
        using var human = _h.Human();

        var result = await _h.Change().Handle(Change(new ProjectEdit(ClearNotes: true), version: 1), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        Expect.Equal((null, 2L), (_h.Payload<ProjectLedNoted>(Assert.Single(_h.Slice.Store.Events)).Notes, result.Value.Version));
    }

    [Fact]
    public async Task The_notes_are_scrubbed_before_the_event_is_written()
    {
        await _h.Lease();
        _h.OwnerLeads();
        using var human = _h.Human();

        var result = await _h.Change().Handle(Change(new ProjectEdit(Notes: $"key {ProjectWriteHarness.Secret}")), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        Expect.Equal("key [redacted]", _h.Payload<ProjectLedNoted>(Assert.Single(_h.Slice.Store.Events)).Notes);
    }

    [Fact]
    public async Task A_stale_card_version_is_SND_PROJECT_409_and_records_nothing()
    {
        await _h.Lease();
        _h.OwnerLeads();
        await _h.SeedHidden(version: 2, hidden: false);
        using var human = _h.Human();

        var result = await _h.Change().Handle(Change(new ProjectEdit(Notes: "x"), version: 1), Token);

        Expect.Equal(SindriErrors.ProjectConflict.Code, result.Error.Code);
        Assert.Empty(_h.Slice.Store.Events);
    }

    [Fact]
    public async Task Any_other_field_is_SND_PROJECT_004_and_records_nothing()
    {
        await _h.Lease();
        _h.OwnerLeads();
        using var human = _h.Human();

        var result = await _h.Change().Handle(Change(new ProjectEdit(Notes: "x", State: ProjectState.Done)), Token);

        Expect.Equal(SindriErrors.ProjectInvalid.Code, result.Error.Code);
        Assert.Contains("only takes notes here", result.Error.Message, StringComparison.Ordinal);
        Assert.Empty(_h.Slice.Store.Events);
    }

    [Fact]
    public async Task A_key_the_owner_does_not_lead_is_the_not_found_error()
    {
        await _h.Lease();
        using var human = _h.Human();   // the stored project exists but is led by someone else
        var other = "linear:project/ffffffff-0000-4000-8000-000000000000";

        var notLed = await _h.Change().Handle(Change(new ProjectEdit(Notes: "x")), Token);
        var unknown = await _h.Change().Handle(Change(new ProjectEdit(Notes: "x"), project: other), Token);

        Expect.Equal((SindriErrors.ProjectNotFound.Code, SindriErrors.ProjectNotFound.Code), (notLed.Error.Code, unknown.Error.Code));
        Assert.Empty(_h.Slice.Store.Events);
    }

    [Fact]
    public async Task Something_that_is_neither_a_project_id_nor_a_Linear_key_is_refused()
    {
        await _h.Lease();
        using var human = _h.Human();

        var result = await _h.Change().Handle(Change(new ProjectEdit(Notes: "x"), project: "nope"), Token);

        Expect.Equal(SindriErrors.InvalidInput.Code, result.Error.Code);
    }
}
