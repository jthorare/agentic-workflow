using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Tests.Sandbox;

namespace Aw.Sindri.Tests.CheckSandbox;

/// <summary>The owner sentences a blocker prints (sandbox spec §8): the code's table is the golden 'copy.json', and an open value in a sentence is filled from the blocker.</summary>
[Lane(TestLane.Unit)]
public sealed class SandboxBlockerLineTests
{
    [Fact]
    public void The_copy_table_says_exactly_what_the_golden_says()
    {
        foreach (var entry in SandboxGoldens.Object("copy.json")["entries"]!.AsArray().Cast<JsonObject>())
        {
            var reason = (string)entry["reason"]!;
            var copy = SandboxCopy.For((string)entry["code"]!, reason.Replace("<status>", "502", StringComparison.Ordinal), (string?)entry["source"]);

            Assert.NotNull(copy);
            Assert.Equal(((string)entry["text"]!).Replace("<status>", "502", StringComparison.Ordinal), copy.Text);
            Assert.Equal((string?)entry["command"], copy.Command);
            Assert.Equal((string?)entry["state"], copy.State);
        }
    }

    [Fact]
    public void The_attended_copy_says_exactly_what_the_golden_says()
    {
        foreach (var entry in SandboxGoldens.Object("copy.json")["entries"]!.AsArray().Cast<JsonObject>())
        {
            var reason = (string)entry["reason"]!;
            var copy = SandboxCopy.For((string)entry["code"]!, reason.Replace("<status>", "502", StringComparison.Ordinal), (string?)entry["source"], values: null, attended: true);

            Assert.NotNull(copy);
            Assert.Equal(((string)entry["attendedText"]!).Replace("<status>", "502", StringComparison.Ordinal), copy.Text);
            Assert.Equal((string?)entry["command"], copy.Command);
            Assert.Equal((string?)entry["state"], copy.State);
        }
    }

    [Fact]
    public void The_not_checked_sentence_says_exactly_what_the_golden_says() =>
        Assert.Equal(SandboxCopy.HooksNotChecked, (string)SandboxGoldens.Object("copy.json")["notChecked"]!["hooks-run"]!);

    [Fact]
    public void The_session_cap_sentence_fills_its_count_and_cap_from_the_refusal()
    {
        var refusal = SandboxRefusal.For(SindriErrors.LaunchSessionWait, SandboxReasons.Sessions, details: new Dictionary<string, string>(StringComparer.Ordinal) { ["running"] = "3", ["cap"] = "3" });

        var line = SandboxBlockerLine.From(SandboxBlocker.Of(refusal));

        Assert.Equal("Waiting for a free session slot (`3` of `3` running). This Step starts by itself.", line.Text);
    }

    [Fact]
    public void An_open_value_is_filled_from_the_refusal()
    {
        var refusal = SandboxRefusal.For(SindriErrors.LaunchSandboxUnavailable, SandboxReasons.NoCgroupControllers, details: new Dictionary<string, string>(StringComparer.Ordinal) { ["which"] = "memory" });

        var line = SandboxBlockerLine.From(SandboxBlocker.Of(refusal));

        Assert.Equal("The system doesn't give your account memory or CPU limits. Enable the missing limit (memory) in systemd, then press Check again.", line.Text);
    }

    [Fact]
    public void A_login_sentence_names_the_target_and_the_source_picks_the_wording()
    {
        var refusal = SandboxRefusal.For(SindriErrors.LaunchLoginUnavailable, SandboxReasons.NoLogin, source: ModelLoginSource.SeparateLogin, details: new Dictionary<string, string>(StringComparer.Ordinal) { ["target"] = "claude-code" });

        var line = SandboxBlockerLine.From(SandboxBlocker.Of(refusal));

        Assert.Contains("Run `sindri sandbox login claude-code`", line.Text, StringComparison.Ordinal);
        Assert.Equal("sindri sandbox login claude-code", line.Command);
        Assert.Equal("separate-login", line.Source);
    }

    [Fact]
    public void A_http_reason_fills_the_status()
    {
        var refusal = SandboxRefusal.For(SindriErrors.LaunchGitHubTokenFailed, "http-502");

        Assert.Contains("(`502`)", SandboxBlockerLine.From(SandboxBlocker.Of(refusal)).Text, StringComparison.Ordinal);
    }
}
