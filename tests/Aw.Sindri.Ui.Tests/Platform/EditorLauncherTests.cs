using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Platform;

/// <summary>A file opens in the configured editor, as an argv list with no shell, and falls back to the system default when none is set or it won't start (#3715),
/// or when every setting is a terminal editor, which has no terminal to draw in here (#3836); a terminal editor is skipped for the next setting (#4190).</summary>
[Lane(TestLane.Unit)]
public sealed class EditorLauncherTests
{
    private static readonly string _file = Path.GetFullPath(typeof(EditorLauncherTests).Assembly.Location);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static (EditorLauncher Launcher, FakeLauncher Inner, List<IReadOnlyList<string>> Started) Make(EditorEnvironment environment, bool startWorks = true)
    {
        var inner = new FakeLauncher();
        inner.AddFile(_file);
        var started = new List<IReadOnlyList<string>>();
        return (new EditorLauncher(inner, () => environment, argv =>
        {
            started.Add(argv);
            return startWorks;
        }), inner, started);
    }

    [Fact]
    public async Task A_configured_editor_gets_the_file_as_its_last_argument()
    {
        var (launcher, inner, started) = Make(new EditorEnvironment(Configured: null, Visual: "code --wait", Editor: null));

        Assert.True(await launcher.OpenFile(_file, Token));

        Assert.Equal([["code", "--wait", _file]], started);
        Assert.Empty(inner.Opened);
    }

    [Fact]
    public async Task A_file_name_with_spaces_and_shell_characters_stays_one_argument()
    {
        var dir = Directory.CreateTempSubdirectory("aw-editor-3715-");
        try
        {
            var odd = Path.Combine(dir.FullName, "a b;$(x)#.cs");
            await File.WriteAllTextAsync(odd, "x", Token);
            var (launcher, _, started) = Make(new EditorEnvironment(Configured: "code", Visual: null, Editor: null));

            Assert.True(await launcher.OpenFile(odd, Token));

            Assert.Equal(["code", odd], started[0], StringComparer.Ordinal);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Theory]
    [InlineData(null, null, "vim")]
    [InlineData(null, null, "/usr/bin/vim")]
    [InlineData(null, "emacs -nw", null)]
    [InlineData("nvim", null, null)]
    [InlineData("nvim", "vim", "nano")]
    [InlineData(null, null, "nano")]
    [InlineData(null, null, "Vim.exe")]
    public async Task A_terminal_editor_opens_the_file_with_the_system_default(string? configured, string? visual, string? editor)
    {
        var (launcher, inner, started) = Make(new EditorEnvironment(configured, visual, editor));

        Assert.True(await launcher.OpenFile(_file, Token));

        Assert.Empty(started);
        Assert.Equal([_file], inner.Opened, StringComparer.Ordinal);
    }

    [Theory]
    [InlineData("nvim", "code", null, "code")]
    [InlineData("nvim", "emacs -nw", "code -w", "code")]
    [InlineData(null, "nano", "/usr/bin/gvim -f", "/usr/bin/gvim")]
    public async Task A_terminal_editor_is_skipped_for_the_next_setting(string? configured, string? visual, string? editor, string program)
    {
        var (launcher, inner, started) = Make(new EditorEnvironment(configured, visual, editor));

        Assert.True(await launcher.OpenFile(_file, Token));

        Assert.Equal(program, Assert.Single(started)[0], StringComparer.Ordinal);
        Assert.Empty(inner.Opened);
    }

    [Theory]
    [InlineData("emacs", "emacs")]
    [InlineData("code", "code")]
    [InlineData("code -w", "code")]
    [InlineData("/usr/bin/gvim -f", "/usr/bin/gvim")]
    public async Task A_gui_editor_still_gets_the_file(string editor, string program)
    {
        var (launcher, inner, started) = Make(new EditorEnvironment(Configured: null, Visual: null, Editor: editor));

        Assert.True(await launcher.OpenFile(_file, Token));

        Assert.Equal(program, Assert.Single(started)[0], StringComparer.Ordinal);
        Assert.Empty(inner.Opened);
    }

    [Fact]
    public async Task No_editor_set_opens_the_file_with_the_system_default()
    {
        var (launcher, inner, started) = Make(new EditorEnvironment(Configured: null, Visual: null, Editor: null));

        Assert.True(await launcher.OpenFile(_file, Token));

        Assert.Empty(started);
        Assert.Equal([_file], inner.Opened, StringComparer.Ordinal);
    }

    [Fact]
    public async Task An_editor_that_will_not_start_falls_back_to_the_system_default()
    {
        var (launcher, inner, started) = Make(new EditorEnvironment(Configured: null, Visual: null, Editor: "no-such-editor"), startWorks: false);

        Assert.True(await launcher.OpenFile(_file, Token));

        _ = Assert.Single(started);
        Assert.Equal([_file], inner.Opened, StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_file_that_is_not_there_does_not_start_the_editor()
    {
        var (launcher, _, started) = Make(new EditorEnvironment(Configured: "code", Visual: null, Editor: null));

        Assert.False(await launcher.OpenFile(Path.Combine(Path.GetDirectoryName(_file)!, "missing-3715.ts"), Token));

        Assert.Empty(started);
    }

    [Fact]
    public async Task A_link_goes_straight_to_the_system_default()
    {
        var (launcher, inner, started) = Make(new EditorEnvironment(Configured: "code", Visual: null, Editor: null));

        Assert.True(await launcher.OpenUri(new Uri("https://example.test/x"), Token));

        Assert.Empty(started);
        Assert.Equal(["https://example.test/x"], inner.Opened, StringComparer.Ordinal);
    }
}
