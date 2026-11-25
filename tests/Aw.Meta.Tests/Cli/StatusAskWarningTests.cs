namespace Aw.Meta.Tests.Cli;

[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class StatusAskWarningTests : IDisposable
{
    private readonly CliHarness _cli = new();

    private static string Hook(string session, string to, string message) =>
        $$$"""{"session_id":"{{{session}}}","tool_name":"SendMessage","tool_input":{"to":"{{{to}}}","message":"{{{message}}}"},"tool_response":{"success":true}}""";

    private async Task SetUp()
    {
        _ = await _cli.Run("team-init", "--team", "t1", "--repo", "/r", "--lanes", "api");
        _ = await _cli.Run("role-set", "--session", "s-meta", "--name", "t1-meta");
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_lane_status_send_with_a_question_mark_prints_one_warning_line()
    {
        await SetUp();

        var sent = await _cli.RunWithInput(Hook("s-lane", "t1-meta", "STATUS: want me to rerun it?"), "outbox-record");

        Assert.Equal(0, sent.Exit);
        Assert.StartsWith("aw-meta: warning: ", sent.Out, StringComparison.Ordinal);
        Assert.DoesNotContain('\n', sent.Out.TrimEnd('\n', '\r'));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_status_send_without_a_question_mark_prints_nothing()
    {
        await SetUp();

        var sent = await _cli.RunWithInput(Hook("s-lane", "t1-meta", "STATUS: review clean"), "outbox-record");

        Assert.Equal((0, string.Empty), (sent.Exit, sent.Out));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_meta_session_is_warned_too()
    {
        await SetUp();

        var sent = await _cli.RunWithInput(Hook("s-meta", "t1-api", "STATUS: want me to rerun it?"), "outbox-record");

        Assert.Equal(0, sent.Exit);
        Assert.Contains("aw-meta: warning: ", sent.Out, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task An_action_send_with_a_question_mark_prints_nothing()
    {
        await SetUp();

        var sent = await _cli.RunWithInput(Hook("s-lane", "t1-meta", "ACTION: can you rerun it?"), "outbox-record");

        Assert.Equal((0, string.Empty), (sent.Exit, sent.Out));
    }

    public void Dispose() => _cli.Dispose();
}
