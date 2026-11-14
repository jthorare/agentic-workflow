using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Todo;

namespace Aw.Sindri.Ui.Tests.FreshHome;

/// <summary>
/// The app reads Sindri's real fresh-home answers (Aw.Sindri.Tests FreshHomeWireTests pins the same files against the server's own output) through its own JSON
/// context, so the field the app reads can't drift from the one the server writes (#3113).
/// </summary>
[Lane(TestLane.Unit)]
public sealed class FreshHomeWireTests
{
    private static string File(string name) => System.IO.File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Golden", "FreshHome", name));

    private static T Read<T>(string json)
        where T : class
    {
        var read = AwJson.Deserialize<T>(System.Text.Encoding.UTF8.GetBytes(json), SindriApiClient.Json);
        Assert.True(read.IsSuccess, read.IsError ? read.Error.Message : null);
        return read.Value;
    }

    [Fact]
    public void The_servers_fresh_home_profile_reads_not_set_up_with_no_red_cross_and_no_Live_row()
    {
        var profile = Read<ProfileView>(File("profile.json"));

        Assert.True(StatusText.IsMissing(profile));
        Assert.Equal(("not set up", false), (StatusText.ProfileState(profile), StatusText.ProfileGlyph(profile) == StateGlyph.Down));
        Assert.DoesNotContain(StatusText.Profile(profile), cell => string.Equals(cell.Label, Labels.RowLive, StringComparison.Ordinal));
    }

    [Fact]
    public void The_servers_fresh_home_todo_note_draws_no_sync_line_and_no_note_line()
    {
        var note = Read<TodoNoteView>(File("todo-note.json"));

        Assert.Equal("not set up", note.State);
        Assert.Equal((string.Empty, string.Empty, false), TodoSyncLine.Of(note, DateTimeOffset.UnixEpoch, TimeZoneInfo.Utc));
    }

    [Fact]
    public void A_status_with_the_problemKind_field_absent_from_the_json_still_reads_unreadable_with_the_cross()
    {
        var profile = Read<ProfileView>("""{"live":null,"problem":"No profile found","source":null,"modelPick":"off","labelPick":"off","surfacePick":"off"}""");

        Assert.Equal(("unreadable", StateGlyph.Down), (StatusText.ProfileState(profile), StatusText.ProfileGlyph(profile)));
    }

    [Fact]
    public void A_profile_the_server_calls_invalid_reads_invalid_with_the_cross()
    {
        var profile = Read<ProfileView>("""{"live":null,"problem":"line 3: unknown key","source":null,"modelPick":"off","labelPick":"off","surfacePick":"off","problemKind":"invalid"}""");

        Assert.Equal(("invalid", StateGlyph.Down), (StatusText.ProfileState(profile), StatusText.ProfileGlyph(profile)));
    }

    [Fact]
    public void A_profile_file_the_server_cant_read_reads_unreadable_with_the_cross()
    {
        var profile = Read<ProfileView>("""{"live":null,"problem":"/home/thor/profile.yaml can't be read: Access to the path is denied.","source":null,"modelPick":"off","labelPick":"off","surfacePick":"off","problemKind":"unreadable"}""");

        Assert.Equal(("unreadable", StateGlyph.Down), (StatusText.ProfileState(profile), StatusText.ProfileGlyph(profile)));
    }

    [Fact]
    public void An_invalid_profile_keeps_its_Live_row()
    {
        var profile = Read<ProfileView>("""{"live":null,"problem":"line 3: unknown key","source":null,"modelPick":"off","labelPick":"off","surfacePick":"off","problemKind":"invalid"}""");

        Assert.Contains(StatusText.Profile(profile), cell => string.Equals(cell.Label, Labels.RowLive, StringComparison.Ordinal));
    }
}
