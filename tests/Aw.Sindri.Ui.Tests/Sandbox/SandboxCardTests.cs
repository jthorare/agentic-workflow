using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Sandbox;

namespace Aw.Sindri.Ui.Tests.Sandbox;

/// <summary>Health's Sandbox row, one test per states.json healthRow (sandbox spec §8 States).</summary>
[Lane(TestLane.Unit)]
public sealed class SandboxCardTests
{
    private readonly SandboxTestBed _bed = new();

    private async Task Read(string scenario)
    {
        _bed.Server.SandboxStatusAnswer = RouteAnswer.SpecShaped("sandbox-status", scenario);
        await _bed.Card.Refresh(SandboxTestBed.Api, SandboxTestBed.Token);
    }

    [Fact]
    public async Task Ready_says_ready()
    {
        await Read("ready");

        Assert.Equal((SandboxTestBed.Text("healthRow", "ready"), SandboxHealthState.Ready, false, false), (_bed.Card.RowText, _bed.Card.State, _bed.Card.IsDimmed, _bed.Card.ShowsCheck));
        Assert.Equal(StateGlyph.Ok, _bed.Card.Glyph);
    }

    [Fact]
    public async Task Blocked_says_the_first_blockers_sentence_and_offers_check_again()
    {
        await Read("blocked");

        var sentence = "Sandboxed sessions can't start: bubblewrap isn't installed.";
        Assert.Equal(SandboxTestBed.Text("healthRow", "blocked").Replace("<the first blocker's sentence>", sentence, StringComparison.Ordinal), _bed.Card.RowText);
        Assert.Equal((SandboxHealthState.Blocked, true, "blocked", StateGlyph.Down), (_bed.Card.State, _bed.Card.ShowsCheck, _bed.Card.StateWord, _bed.Card.Glyph));
        Assert.Equal("sudo apt install bubblewrap", Assert.Single(_bed.Card.Blockers).Command);
    }

    [Fact]
    public async Task A_gray_blocker_reads_as_waiting_never_blocked()
    {
        _bed.Server.SandboxStatusAnswer = new RouteAnswer(HttpStatusCode.OK, """{"checkedAt":"2026-10-28T09:00:00Z","ready":false,"blockers":[{"code":"SND-LAUNCH-014","reason":"unsupported-platform","text":"Steps can't run by themselves on macOS yet.","state":"gray"}],"mechanism":"none","groups":[],"limits":{"memoryMax":"12GiB","cpuQuota":"400%","tasksMax":512},"login":"owner-login","learn":[]}""");
        await _bed.Card.Refresh(SandboxTestBed.Api, SandboxTestBed.Token);

        Assert.Equal(("waiting", StateGlyph.Stale), (_bed.Card.StateWord, _bed.Card.Glyph));
    }

    [Fact]
    public async Task Learning_is_a_warning_naming_the_repo_and_the_days_left()
    {
        await Read("learning");

        var expected = SandboxTestBed.Text("healthRow", "learning").Replace("<repo>", "api", StringComparison.Ordinal).Replace("<days>", "3", StringComparison.Ordinal);
        Assert.Equal((expected, SandboxHealthState.Learning, true, "warning"), (_bed.Card.RowText, _bed.Card.State, _bed.Card.IsWarning, (string)SandboxTestBed.Row("healthRow", "learning")["severity"]!));
        Assert.Equal(StateGlyph.Profile, _bed.Card.Glyph);
    }

    [Fact]
    public async Task Serve_down_is_unknown_and_dimmed()
    {
        _bed.Server.GoDown();
        await _bed.Card.Refresh(SandboxTestBed.Api, SandboxTestBed.Token);

        Assert.Equal((SandboxTestBed.Text("healthRow", "serve-down"), SandboxHealthState.ServeDown, true), (_bed.Card.RowText, _bed.Card.State, _bed.Card.IsDimmed));
        Assert.True((bool)SandboxTestBed.Row("healthRow", "serve-down")["dimmed"]!);
    }

    [Fact]
    public async Task Never_checked_says_so_and_offers_the_first_check()
    {
        await Read("never-checked");

        Assert.Equal((SandboxTestBed.Text("healthRow", "never-checked"), SandboxHealthState.NeverChecked, true), (_bed.Card.RowText, _bed.Card.State, _bed.Card.ShowsCheck));
    }

    [Fact]
    public async Task The_row_follows_the_status_when_it_is_read_again()
    {
        await Read("blocked");
        await Read("ready");

        Assert.Equal((SandboxTestBed.Text("healthRow", "ready"), false), (_bed.Card.RowText, _bed.Card.ShowsCheck));
    }

    [Fact]
    public async Task A_status_the_app_cannot_read_is_details_unavailable_not_serve_down()
    {
        _bed.Server.SandboxStatusAnswer = new RouteAnswer(HttpStatusCode.OK, "not json");
        await _bed.Card.Refresh(SandboxTestBed.Api, SandboxTestBed.Token);

        Assert.Equal((SandboxHealthState.Unreadable, false), (_bed.Card.State, _bed.Card.IsDimmed));
    }

    [Fact]
    public async Task No_row_text_uses_a_word_the_sandbox_vocabulary_bans()
    {
        var banned = System.Text.Json.Nodes.JsonNode.Parse(GoldenFiles.Sandbox("copy.json"))!["bannedWords"]!.AsArray().Select(w => (string)w!).ToList();
        List<string> rows = [];
        foreach (var scenario in new[] { "ready", "blocked", "learning", "never-checked" })
        {
            await Read(scenario);
            rows.Add(_bed.Card.RowText);
        }

        Assert.All(rows, row => Assert.DoesNotContain(banned, w => row.Contains(w, StringComparison.OrdinalIgnoreCase)));
    }
}
