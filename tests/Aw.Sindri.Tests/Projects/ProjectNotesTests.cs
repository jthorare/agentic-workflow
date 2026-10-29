using System.Text;
using System.Text.Json;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Projects.Cards;
using Aw.Sindri.Host.Endpoints;

namespace Aw.Sindri.Tests.Projects;

/// <summary>The projects' notes field (owner q158): the request bodies take 'notes' and, for one release, 'note' as an alias; the card serves 'notes' only.</summary>
[Lane(TestLane.Unit)]
public sealed class ProjectNotesTests
{
    private static T Read<T>(string json) => AwJson.Deserialize<T>(Encoding.UTF8.GetBytes(json), SliceHarness.Json).Value;

    [Theory]
    [InlineData("""{"name":"K","notes":"a"}""", "a")]
    [InlineData("""{"name":"K","note":"a"}""", "a")]
    [InlineData("""{"name":"K","notes":"a","note":"a"}""", "a")]
    [InlineData("""{"name":"K"}""", null)]
    public void An_attach_body_takes_notes_or_its_alias_note(string json, string? expected)
    {
        var request = Read<AttachProjectBody>(json).ToRequest();

        Assert.True(request.IsSuccess, request.IsError ? request.Error.ToString() : null);
        Assert.Equal(expected, request.Value.Notes);
    }

    [Fact]
    public void An_attach_body_with_notes_and_a_different_note_is_SND_PROJECT_004_on_notes()
    {
        var request = Read<AttachProjectBody>("""{"name":"K","notes":"a","note":"b"}""").ToRequest();

        Assert.Equal(SindriErrors.ProjectInvalid.Code, request.Error.Code);
        Assert.StartsWith("notes:", request.Error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""{"version":1,"notes":"a"}""", "a", false)]
    [InlineData("""{"version":1,"note":"a"}""", "a", false)]
    [InlineData("""{"version":1,"notes":"a","note":"a"}""", "a", false)]
    [InlineData("""{"version":1,"clearNotes":true}""", null, true)]
    [InlineData("""{"version":1,"clearNote":true}""", null, true)]
    [InlineData("""{"version":1,"state":"paused"}""", null, false)]
    public void A_change_body_takes_notes_and_clearNotes_or_their_aliases(string json, string? notes, bool clear)
    {
        var request = Read<ChangeProjectBody>(json).ToRequest(ProjectRows.HandMadeId);

        Assert.True(request.IsSuccess, request.IsError ? request.Error.ToString() : null);
        Assert.Equal((notes, clear), (request.Value.Edit.Notes, request.Value.Edit.ClearNotes));
    }

    [Fact]
    public void A_change_body_with_notes_and_a_different_note_is_SND_PROJECT_004_on_notes()
    {
        var request = Read<ChangeProjectBody>("""{"version":1,"notes":"a","note":"b"}""").ToRequest(ProjectRows.HandMadeId);

        Assert.Equal(SindriErrors.ProjectInvalid.Code, request.Error.Code);
        Assert.StartsWith("notes:", request.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_card_serves_notes_and_has_no_note_field()
    {
        var card = new ProjectCard("prj_1", ProjectOrigin.HandMade, "Kitchen", Description: null, State: null, StateSetByOwner: false, Health: null, Lead: null, Target: null, OpenIssues: null,
            Counting: false, OpenPrs: null, PrScope: null, LastActivity: null, FinishedAt: null, Repos: [], Links: [], Notes: "line one\nline two", Synced: true, Version: 1);

        using var json = JsonDocument.Parse(AwJson.Serialize(card, SliceHarness.Json));

        Assert.Equal("line one\nline two", json.RootElement.GetProperty("notes").GetString());
        Assert.False(json.RootElement.TryGetProperty("note", out _));
    }
}
