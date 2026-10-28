using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Sandbox;

namespace Aw.Sindri.Ui.Tests.Sandbox;

/// <summary>Settings' Sandbox section, one test per states.json settingsSection (sandbox spec §8 States).</summary>
[Lane(TestLane.Unit)]
public sealed class SandboxSettingsTests
{
    private readonly SandboxTestBed _bed = new();

    private async Task<SandboxSettingsViewModel> Read(string scenario)
    {
        _bed.Server.SandboxStatusAnswer = RouteAnswer.SpecShaped("sandbox-status", scenario);
        var section = new SandboxSettingsViewModel(_bed.Card);
        await _bed.Card.Refresh(SandboxTestBed.Api, SandboxTestBed.Token);
        return section;
    }

    [Fact]
    public async Task Loaded_shows_the_mechanism_groups_limits_and_learn_mode()
    {
        var section = await Read("ready");

        Assert.Equal(SandboxTestBed.Text("settingsSection", "loaded"), section.Title);
        var names = ((System.Text.Json.Nodes.JsonArray)SandboxTestBed.Row("settingsSection", "loaded")["fields"]!).Select(f => (string)f!).ToList();
        Assert.Equal("mechanism | groups | limits | learn mode", SandboxTestBed.J(names));
        Assert.Equal("Mechanism | Allowed networks | Limits | Learn mode | Sign-in", SandboxTestBed.J(section.Fields.Select(f => f.Label)));
        Assert.Equal("bubblewrap | github, npm, nuget | 12GiB memory, 400% CPU, 512 processes, 40m time | off | owner-login", SandboxTestBed.J(section.Fields.Select(f => f.Value)));
        Assert.False(section.IsDimmed);
    }

    [Fact]
    public async Task Learn_mode_on_shows_its_expiry()
    {
        var section = await Read("learning");

        Assert.Equal(SandboxTestBed.Text("settingsSection", "learn-on").Replace("<days>", "3", StringComparison.Ordinal), section.Title);
        Assert.Equal("on for api, ends in 3 days", section.Fields.Single(f => f.Label == "Learn mode").Value);
    }

    [Fact]
    public async Task An_unreadable_status_says_details_unavailable()
    {
        _bed.Server.SandboxStatusAnswer = new RouteAnswer(HttpStatusCode.OK, "not json");
        var section = new SandboxSettingsViewModel(_bed.Card);
        await _bed.Card.Refresh(SandboxTestBed.Api, SandboxTestBed.Token);

        Assert.Equal((SandboxTestBed.Text("settingsSection", "unreadable-plan"), false, false), (section.Title, section.HasFields, section.IsDimmed));
    }

    [Fact]
    public async Task Serve_down_is_status_unavailable_and_dimmed()
    {
        _bed.Server.GoDown();
        var section = new SandboxSettingsViewModel(_bed.Card);
        await _bed.Card.Refresh(SandboxTestBed.Api, SandboxTestBed.Token);

        Assert.Equal((SandboxTestBed.Text("settingsSection", "serve-down"), false, true), (section.Title, section.HasFields, section.IsDimmed));
        Assert.True((bool)SandboxTestBed.Row("settingsSection", "serve-down")["dimmed"]!);
    }
}
