using System.Globalization;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Projects;
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

        var result = await _h.Attach().Handle(new AttachProjectRequest(Name: " Kitchen ", State: ProjectState.Planned, Links: ["https://example.com/plan"], Note: "ordering tile", Target: new DateOnly(2026, 12, 1)), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        var recorded = Assert.Single(_h.Slice.Store.Events);
        var payload = _h.Payload<ProjectAttached>(recorded);
        Expect.Equal(EventTypes.ProjectAttached, recorded.Envelope.Type);
        Expect.Equal($"project.attached:{payload.Id.Value}", recorded.IdempotencyKey);
        Expect.Equal(("Kitchen", ProjectState.Planned, "ordering tile", new DateOnly(2026, 12, 1)), (payload.Name, payload.State, payload.Note, payload.Target));
        Expect.Equal(["https://example.com/plan"], payload.Links);
        Expect.Equal((recorded.Sequence, payload.Id.Value, 1L, "Kitchen"), (result.Value.Accepted.Sequence, result.Value.Project, result.Value.Version, result.Value.Name));
        Expect.Equal(payload.CommandId, result.Value.Accepted.CommandId);
    }

    [Fact]
    public async Task A_linear_key_is_recorded_without_a_name()
    {
        await _h.Lease();
        using var human = _h.Human();

        var result = await _h.Attach().Handle(new AttachProjectRequest(Linear: ProjectRows.LinearKey), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        var payload = _h.Payload<ProjectAttached>(Assert.Single(_h.Slice.Store.Events));
        Expect.Equal((ProjectRows.LinearKey, null), (payload.Linear, payload.Name));
        Expect.Equal(ProjectRows.LinearKey, result.Value.Name);
    }

    [Fact]
    public async Task The_note_and_each_link_are_scrubbed_before_the_event_is_written()
    {
        await _h.Lease();
        using var human = _h.Human();

        var result = await _h.Attach().Handle(new AttachProjectRequest(Name: "Kitchen", Links: [$"https://example.com/{ProjectWriteHarness.Secret}"], Note: $"key {ProjectWriteHarness.Secret}"), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        var payload = _h.Payload<ProjectAttached>(Assert.Single(_h.Slice.Store.Events));
        Expect.Equal("key [redacted]", payload.Note);
        Expect.Equal(["https://example.com/[redacted]"], payload.Links);
    }

    [Theory]
    [InlineData(null, null, "name")]
    [InlineData(null, "  ", "name")]
    [InlineData("banana", null, "linear")]
    [InlineData("https://example.com/acme/project/x-abc123def456", null, "linear")]
    [InlineData("https://linear.app/acme/project/checkout-rewrite-a1b2c3d4e5f6", null, "linear")]   // Ph4: resolved against the stored projects
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
    public async Task A_long_note_a_plain_http_link_and_too_many_links_are_refused()
    {
        await _h.Lease();
        using var human = _h.Human();

        var note = await _h.Attach().Handle(new AttachProjectRequest(Name: "Kitchen", Note: new string('x', 201)), Token);
        var link = await _h.Attach().Handle(new AttachProjectRequest(Name: "Kitchen", Links: ["http://example.com"]), Token);
        var many = await _h.Attach().Handle(new AttachProjectRequest(Name: "Kitchen", Links: [.. Enumerable.Range(0, 11).Select(static i => "https://example.com/" + i.ToString(CultureInfo.InvariantCulture))]), Token);

        Assert.StartsWith("note:", note.Error.Message, StringComparison.Ordinal);
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
}
