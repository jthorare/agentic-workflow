using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Tests.Launch;

/// <summary>
/// The two tmux allowlists (agents spec §15.2; A7 red): table-driven. The foreign list reads and types only. The launch list adds start, resize, discovery and a kill-session of a
/// 'sindri-&lt;actId&gt;' name. A8 adds the terminal verbs' rows to these tables. Whether the launcher's own 'snd-' sessions are ended through this class is decided in step 3 (they are not in the spec's list).
/// </summary>
public sealed class TmuxAllowlistTests
{
    private const string Session = "sindri-k3j9x0a1";

    private static string[] Args(params string[] args) => args;

    public static TheoryData<string[]> ForeignAccepts =>
    [
        Args("capture-pane", "-e", "-p", "-t", "%3"),
        Args("capture-pane", "-e", "-p", "-t", "%3", "-S", "-200", "-E", "-1"),
        Args("send-keys", "-t", "%3", "-l", "--", "hello world"),
        Args("send-keys", "-t", "%3", "Enter"),
        Args("send-keys", "-t", "%3", "Escape"),
        Args("send-keys", "-t", "%3", "PageUp"),
        Args("send-keys", "-t", "%3", "F12"),
        Args("send-keys", "-t", "%3", "C-c"),
        Args("send-keys", "-t", "%3", "M-x"),
        Args("load-buffer", "-b", "sindri-paste", "-"),
        Args("paste-buffer", "-p", "-d", "-b", "sindri-paste", "-t", "%3"),
        Args("display-message", "-p", "-t", "%3", "#{pane_dead}"),
        Args("list-clients", "-t", "main"),
        Args("list-panes", "-t", "main", "-s", "-F", "#{pane_id}"),
        Args("display-message", "-p", "-t", "%12", "#{cursor_x} #{cursor_y} #{pane_width} #{pane_height} #{pane_in_mode} #{alternate_on} #{pane_dead} #{pane_dead_status}"),
        Args("send-keys", "-t", "%3", "BTab"),
        Args("send-keys", "-t", "%3", "BSpace"),
        Args("send-keys", "-t", "%3", "DC"),
        Args("send-keys", "-t", "%3", "F1"),
        Args("send-keys", "-t", "%3", "C-z"),
        Args("send-keys", "-t", "%3", "M-a"),
    ];

    public static TheoryData<string[]> ForeignRefuses =>
    [
        Args("attach"),
        Args("attach-session", "-t", "main"),
        Args("copy-mode", "-t", "%3"),
        Args("kill-session", "-t", "main"),
        Args("kill-server"),
        Args("kill-pane", "-t", "%3"),
        Args("kill-window", "-t", "%3"),
        Args("new-session", "-d", "-s", Session),
        Args("set-option", "-t", "main", "remain-on-exit", "on"),
        Args("resize-window", "-t", "main", "-x", "100", "-y", "30"),
        Args("select-window", "-t", "main:1"),
        Args("send-keys", "-t", "%3", "F13"),
        Args("send-keys", "-t", "%3", "F0"),
        Args("send-keys", "-t", "%3", "C-A"),
        Args("send-keys", "-t", "%3", "Space"),
        Args("kill-session", "-t", "sindri-k3j9x0a1"),
        Args("capture-pane", "-e", "-p", "-t", "main:0.1"),
        Args("send-keys", "-t", "%3", "C-1"),
        Args("send-keys", "-t", "%3", "M-ab"),
        Args("send-keys", "-t", "%3", "Foo"),
        Args("send-keys", "Enter"),
        Args("send-keys", "-t", "main:0.1", "Enter"),
        Args("capture-pane", "-p", "-t", "%"),
        Args("capture-pane", "-p", "-t", ""),
        Args("paste-buffer", "-b", "other-buffer", "-t", "%3"),
        Args("load-buffer", "-b", "other-buffer", "-"),
        Args(),
    ];

    public static TheoryData<string[]> LaunchAccepts =>
    [
        Args("capture-pane", "-e", "-p", "-t", "%3"),
        Args("send-keys", "-t", "%3", "-l", "--", "hi"),
        Args("new-session", "-d", "-s", Session, "-x", "120", "-y", "40", "-c", "/work/demo", "--", "claude", "--model", "x"),
        Args("new-session", "-d", "-s", Session, "-c", "/work/demo", "sh", "/tmp/launch/run.sh"),
        Args("set-option", "-t", Session, "remain-on-exit", "on"),
        Args("resize-window", "-t", Session, "-x", "100", "-y", "30"),
        Args("has-session", "-t", "=" + Session),
        Args("list-sessions", "-F", "#{session_name}"),
        Args("list-panes", "-a", "-F", "#{pane_id}"),
        Args("kill-session", "-t", Session),
        Args("kill-session", "-t", "=" + Session),
    ];

    public static TheoryData<string[]> LaunchRefuses =>
    [
        Args("attach"),
        Args("attach-session", "-t", Session),
        Args("copy-mode", "-t", "%3"),
        Args("kill-server"),
        Args("kill-session", "-t", "main"),
        Args("kill-session", "-t", "sindri-"),
        Args("kill-session", "-t", "sindri-ab"),
        Args("kill-session", "-t", "sindri-UPPERCASE1"),
        Args("kill-session", "-t", "=snd-k3j9x0a1"),
        Args("kill-session", "-t", "sindri-k3j9 x0a1"),
        Args("kill-session", "-t", "%3"),
        Args("kill-session", "-t", "xsindri-k3j9x0a1"),
        Args("kill-session"),
        Args("kill-pane", "-t", "%3"),
        Args("kill-window", "-t", Session),
        Args("set-option", "-g", "status", "off"),
        Args("set-option", "-t", Session, "mouse", "on"),
        Args("select-window", "-t", Session),
        Args("send-keys", "-t", "%3", "F13"),
        Args("resize-window", "-t", "main", "-x", "100", "-y", "30"),
        Args("new-session", "-s", Session),
        Args("send-keys", "-t", "main:0", "Enter"),
        Args(),
    ];

    [Theory, Trait("Lane", "unit")]
    [MemberData(nameof(ForeignAccepts))]
    public void The_foreign_list_takes(string[] args) => AssertTaken(TmuxList.Foreign, args);

    [Theory, Trait("Lane", "unit")]
    [MemberData(nameof(ForeignRefuses))]
    public void The_foreign_list_refuses(string[] args) => AssertRefused(TmuxList.Foreign, args);

    [Theory, Trait("Lane", "unit")]
    [MemberData(nameof(LaunchAccepts))]
    public void The_launch_list_takes(string[] args) => AssertTaken(TmuxList.Launch, args);

    [Theory, Trait("Lane", "unit")]
    [MemberData(nameof(LaunchRefuses))]
    public void The_launch_list_refuses(string[] args) => AssertRefused(TmuxList.Launch, args);

    [Fact, Trait("Lane", "unit")]
    public void The_foreign_list_is_not_the_launch_list()
    {
        AssertRefused(TmuxList.Foreign, Args("resize-window", "-t", Session, "-x", "100", "-y", "30"));
        AssertTaken(TmuxList.Launch, Args("resize-window", "-t", Session, "-x", "100", "-y", "30"));
    }

    private static void AssertTaken(TmuxList list, string[] args)
    {
        var checkedCall = TmuxAllowlist.Check(list, args);

        Assert.True(checkedCall.IsSuccess, checkedCall.IsError ? checkedCall.Error.Message : null);
    }

    private static void AssertRefused(TmuxList list, string[] args)
    {
        var checkedCall = TmuxAllowlist.Check(list, args);

        Assert.True(checkedCall.IsError, "allowed: " + string.Join(' ', args));
    }
}
