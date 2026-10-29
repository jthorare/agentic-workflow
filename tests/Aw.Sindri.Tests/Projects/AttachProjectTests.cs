using System.Globalization;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Projects.Vocabulary;
using Aw.Sindri.Features.AttachProject;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Projects;

/// <summary>Attaching a project (projects page spec §5.1, §5.3): 'project.attached', scrubbed, keyed by the new id.</summary>
[Lane(TestLane.Unit)]
public sealed class AttachProjectTests
{
    private readonly ProjectWriteHarness _h = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_hand_made_project_is_recorded_with_its_fields()
    {
        await _h.Lease();
        using var human = _h.Human();

        var result = await _h.Attach().Handle(new AttachProjectRequest(Name: " Kitchen ", State: ProjectState.Planned, Links: ["https://example.com/plan"], Notes: "ordering tile", Target: new DateOnly(2026, 12, 1)), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        var recorded = Assert.Single(_h.Slice.Store.Events);
        var payload = _h.Payload<ProjectAttached>(recorded);
        Expect.Equal(EventTypes.ProjectAttached, recorded.Envelope.Type);
        Expect.Equal($"project.attached:{payload.Id.Value}", recorded.IdempotencyKey);
        Expect.Equal(("Kitchen", ProjectState.Planned, "ordering tile", new DateOnly(2026, 12, 1)), (payload.Name, payload.State, payload.Notes, payload.Target));
        Expect.Sequence(["https://example.com/plan"], payload.Links);
        Expect.Equal((recorded.Sequence, payload.Id.Value, 1L, "Kitchen"), (result.Value.Accepted.Sequence, result.Value.Project, result.Value.Version, result.Value.Name));
        Expect.Equal(payload.CommandId, result.Value.Accepted.CommandId);
    }

    [Fact]
    public async Task Attaching_a_project_that_stopped_being_led_takes_its_led_notes_when_none_are_typed()
    {
        await _h.Lease();
        await _h.SeedLedNotes(version: 1, notes: "from when I led it");
        using var human = _h.Human();   // the stored project is led by someone else now

        var kept = await _h.Attach().Handle(new AttachProjectRequest(Linear: ProjectRows.LinearKey), Token);

        Assert.True(kept.IsSuccess, kept.IsError ? kept.Error.ToString() : null);
        Expect.Equal("from when I led it", _h.Payload<ProjectAttached>(Assert.Single(_h.Slice.Store.Events)).Notes);
    }

    [Fact]
    public async Task Notes_typed_on_attach_win_over_the_led_notes()
    {
        await _h.Lease();
        await _h.SeedLedNotes(version: 1, notes: "from when I led it");
        using var human = _h.Human();

        var typed = await _h.Attach().Handle(new AttachProjectRequest(Linear: ProjectRows.LinearKey, Notes: "fresh start"), Token);

        Assert.True(typed.IsSuccess, typed.IsError ? typed.Error.ToString() : null);
        Expect.Equal("fresh start", _h.Payload<ProjectAttached>(Assert.Single(_h.Slice.Store.Events)).Notes);
    }

    [Fact]
    public async Task Attach_stores_the_linear_title_as_the_name()
    {
        await _h.Lease();
        using var human = _h.Human();

        var result = await _h.Attach().Handle(new AttachProjectRequest(Linear: ProjectRows.LinearKey), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        var payload = _h.Payload<ProjectAttached>(Assert.Single(_h.Slice.Store.Events));
        Expect.Equal((ProjectRows.LinearKey, "Checkout rewrite"), (payload.Linear, payload.Name));
        Expect.Equal("Checkout rewrite", result.Value.Name);
    }

    [Fact]
    public async Task Attach_keeps_a_typed_name_over_the_linear_title()
    {
        await _h.Lease();
        using var human = _h.Human();

        var result = await _h.Attach().Handle(new AttachProjectRequest(Linear: ProjectRows.LinearKey, Name: " Checkout "), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        var payload = _h.Payload<ProjectAttached>(Assert.Single(_h.Slice.Store.Events));
        Expect.Equal((ProjectRows.LinearKey, "Checkout"), (payload.Linear, payload.Name));
    }

    [Fact]
    public async Task The_notes_and_each_link_are_scrubbed_before_the_event_is_written()
    {
        await _h.Lease();
        using var human = _h.Human();

        var result = await _h.Attach().Handle(new AttachProjectRequest(Name: "Kitchen", Links: [$"https://example.com/{ProjectWriteHarness.Secret}"], Notes: $"key {ProjectWriteHarness.Secret}"), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        var payload = _h.Payload<ProjectAttached>(Assert.Single(_h.Slice.Store.Events));
        Expect.Equal("key [redacted]", payload.Notes);
        Expect.Sequence(["https://example.com/[redacted]"], payload.Links);
    }

    [Theory]
    [InlineData(null, null, "name")]
    [InlineData(null, "  ", "name")]
    [InlineData("banana", null, "linear")]
    [InlineData("https://example.com/acme/project/x-abc123def456", null, "linear")]
    public async Task A_bad_field_is_SND_PROJECT_004_naming_it_and_records_nothing(string? linear, string? name, string field)
    {
        await _h.Lease();
        using var human = _h.Human();

        var result = await _h.Attach().Handle(new AttachProjectRequest(linear, name), Token);

        Expect.Equal(SindriErrors.ProjectInvalid.Code, result.Error.Code);
        Assert.StartsWith(field + ":", result.Error.Message, StringComparison.Ordinal);
        Assert.Empty(_h.Slice.Store.Events);
    }

    [Fact]
    public async Task Long_notes_a_plain_http_link_and_too_many_links_are_refused()
    {
        await _h.Lease();
        using var human = _h.Human();

        var note = await _h.Attach().Handle(new AttachProjectRequest(Name: "Kitchen", Notes: new string('x', 16385)), Token);
        var link = await _h.Attach().Handle(new AttachProjectRequest(Name: "Kitchen", Links: ["http://example.com"]), Token);
        var many = await _h.Attach().Handle(new AttachProjectRequest(Name: "Kitchen", Links: [.. Enumerable.Range(0, 11).Select(static i => "https://example.com/" + i.ToString(CultureInfo.InvariantCulture))]), Token);

        Assert.StartsWith("notes:", note.Error.Message, StringComparison.Ordinal);
        Assert.StartsWith("links:", link.Error.Message, StringComparison.Ordinal);
        Assert.StartsWith("links:", many.Error.Message, StringComparison.Ordinal);
        Assert.Empty(_h.Slice.Store.Events);
    }

    [Fact]
    public async Task A_linear_key_already_attached_is_SND_PROJECT_001()
    {
        await _h.Lease();
        await _h.SeedAttached();
        using var human = _h.Human();

        var result = await _h.Attach().Handle(new AttachProjectRequest(Linear: ProjectRows.LinearKey), Token);

        Expect.Equal(SindriErrors.ProjectAlreadyOnPage.Code, result.Error.Code);
        Assert.Empty(_h.Slice.Store.Events);
    }

    [Fact]
    public async Task Without_a_lease_it_is_not_running()
    {
        using var human = _h.Human();

        var result = await _h.Attach().Handle(new AttachProjectRequest(Name: "Kitchen"), Token);

        Expect.Equal(SindriErrors.NotRunning.Code, result.Error.Code);
    }

    [Fact]
    public async Task A_project_url_resolves_to_the_key_of_the_stored_project_with_that_slug()
    {
        await _h.Lease();
        using var human = _h.Human();

        var result = await _h.Attach().Handle(new AttachProjectRequest(Linear: ProjectRows.LinearUrl), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        Expect.Equal(ProjectRows.LinearKey, _h.Payload<ProjectAttached>(Assert.Single(_h.Slice.Store.Events)).Linear);
    }

    [Theory]
    [InlineData("https://linear.app/acme/project/other-zzzzzz999999")]
    [InlineData("linear:project/not-in-the-catalog")]
    public async Task A_project_the_store_does_not_hold_is_SND_PROJECT_003_naming_the_synced_teams(string typed)
    {
        await _h.Lease();
        using var human = _h.Human();

        var result = await _h.Attach().Handle(new AttachProjectRequest(Linear: typed), Token);

        Expect.Equal(SindriErrors.ProjectNotSynced.Code, result.Error.Code);
        Assert.Contains("ENG, OPS", result.Error.Message, StringComparison.Ordinal);
        Assert.Contains("context.sources.linear.teams", result.Error.Message, StringComparison.Ordinal);
        Assert.Empty(_h.Slice.Store.Events);
    }

    [Fact]
    public async Task A_project_the_owner_leads_is_SND_PROJECT_001()
    {
        await _h.Lease();
        _ = _h.Facts.With(ProjectRows.Snapshot(ProjectRows.Fact(leadIsOwner: true)));
        using var human = _h.Human();

        var result = await _h.Attach().Handle(new AttachProjectRequest(Linear: ProjectRows.LinearKey), Token);

        Expect.Equal(SindriErrors.ProjectAlreadyOnPage.Code, result.Error.Code);
        Assert.Empty(_h.Slice.Store.Events);
    }

    [Fact]
    public async Task A_failing_facts_read_is_its_error()
    {
        await _h.Lease();
        _ = _h.Facts.Failing(SindriErrors.NotRunning.With("context is down"));
        using var human = _h.Human();

        var result = await _h.Attach().Handle(new AttachProjectRequest(Linear: ProjectRows.LinearKey), Token);

        Expect.Equal(SindriErrors.NotRunning.Code, result.Error.Code);
    }

    [Fact]
    public async Task A_hand_made_project_reads_no_facts()
    {
        await _h.Lease();
        using var human = _h.Human();

        _ = await _h.Attach().Handle(new AttachProjectRequest(Name: "Kitchen"), Token);

        Assert.Empty(_h.Facts.Asked);
    }
}
