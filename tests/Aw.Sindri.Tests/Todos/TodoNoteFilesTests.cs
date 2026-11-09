using System.Security.Cryptography;
using System.Text;
using Aw.Sindri.Core.Todos.Note;
using Aw.Sindri.Infrastructure.Todos;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Todos;

/// <summary>The note sync's files on temp folders (todo spec §4.5 Writing steps 1-5, the base): read limits, quiet wait, swap, keep, intent and base files.</summary>
public sealed class TodoNoteFilesTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("aw-todo-files-").FullName;
    private readonly string _vault;
    private readonly string _note;
    private readonly FileTodoNoteFiles _files;

    public TodoNoteFilesTests()
    {
        _vault = Directory.CreateDirectory(Path.Combine(_root, "vault")).FullName;
        _note = Path.Combine(_vault, "Todo.md");
        _files = new FileTodoNoteFiles(new FakeSindriHome(Path.Combine(_root, "home")), TimeProvider.System);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static string Sha(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    private string TodoFolder => Path.Combine(_root, "home", "todo");

    [Fact, Trait("Lane", "integration")]
    public async Task A_note_reads_with_its_text_size_and_sha256()
    {
        await File.WriteAllTextAsync(_note, "- [ ] one\n", TestContext.Current.CancellationToken);

        var read = _files.Read(_note);

        Assert.Equal(("- [ ] one\n", Sha(Encoding.UTF8.GetBytes("- [ ] one\n")), 10L, false, null), (read.Text, read.Hash, read.Bytes, read.Missing, read.Problem));
    }

    [Fact, Trait("Lane", "integration")]
    public void A_missing_note_reads_as_missing()
    {
        var read = _files.Read(_note);

        Assert.Equal((null, null, 0L, true, null), (read.Text, read.Hash, read.Bytes, read.Missing, read.Problem));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_symlink_is_refused_and_not_read()
    {
        var real = Path.Combine(_root, "real.md");
        await File.WriteAllTextAsync(real, "x", TestContext.Current.CancellationToken);
        _ = File.CreateSymbolicLink(_note, real);

        var read = _files.Read(_note);

        Assert.Null(read.Text);
        Assert.NotNull(read.Problem);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_note_with_two_hard_links_is_refused()
    {
        await File.WriteAllTextAsync(_note, "x", TestContext.Current.CancellationToken);
        Assert.Equal(0, Link(_note, Path.Combine(_root, "other.md")));

        var read = _files.Read(_note);

        Assert.Null(read.Text);
        Assert.NotNull(read.Problem);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_note_over_1_MB_has_a_problem_and_no_text()
    {
        await File.WriteAllBytesAsync(_note, new byte[(1024 * 1024) + 1], TestContext.Current.CancellationToken);

        var read = _files.Read(_note);

        Assert.Equal((null, "the note is over 1 MB"), (read.Text, read.Problem));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_file_rewritten_during_the_wait_settles_only_after_the_last_write()
    {
        await File.WriteAllTextAsync(_note, "a", TestContext.Current.CancellationToken);
        var writer = Task.Run(async () =>
        {
            await Task.Delay(150, TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(_note, "b", TestContext.Current.CancellationToken);
        }, TestContext.Current.CancellationToken);

        var (read, settled) = await _files.ReadQuiet(_note, TimeSpan.FromMilliseconds(400), TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        await writer;

        Assert.Equal(("b", true), (read.Text, settled));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_file_that_keeps_changing_returns_not_settled_at_the_longest_wait()
    {
        await File.WriteAllTextAsync(_note, "0", TestContext.Current.CancellationToken);
        using var stop = new CancellationTokenSource();
        var writer = Task.Run(async () =>
        {
            for (var i = 1; !stop.IsCancellationRequested; i++)
            {
                await File.WriteAllTextAsync(_note, i.ToString(System.Globalization.CultureInfo.InvariantCulture), CancellationToken.None);
                await Task.Delay(50, CancellationToken.None);
            }
        }, CancellationToken.None);

        var (_, settled) = await _files.ReadQuiet(_note, TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        await stop.CancelAsync();
        await writer;

        Assert.False(settled);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Swapping_an_existing_note_puts_the_new_text_in_place_and_the_old_text_in_the_tmp_file_with_the_notes_mode()
    {
        await File.WriteAllTextAsync(_note, "old", TestContext.Current.CancellationToken);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(_note, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead);
        }

        var swap = _files.Swap(_note, "new", _vault);

        Assert.Null(swap.Problem);
        Assert.Equal(Path.Combine(_vault, ".Todo.md.sindri-tmp"), swap.CameOut);
        Assert.Equal(("new", "old"), (await File.ReadAllTextAsync(_note, TestContext.Current.CancellationToken), await File.ReadAllTextAsync(swap.CameOut!, TestContext.Current.CancellationToken)));
        if (!OperatingSystem.IsWindows())
        {
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead, File.GetUnixFileMode(_note));
        }
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Swapping_a_missing_note_creates_it_and_nothing_came_out()
    {
        var swap = _files.Swap(_note, "fresh", _vault);

        Assert.Equal((null, null, "fresh"), (swap.CameOut, swap.Problem, await File.ReadAllTextAsync(_note, TestContext.Current.CancellationToken)));
        Assert.False(File.Exists(Path.Combine(_vault, ".Todo.md.sindri-tmp")));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_symlinked_folder_between_the_vault_and_the_note_refuses_the_swap_and_the_target_is_untouched()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var outside = Directory.CreateDirectory(Path.Combine(_root, "outside")).FullName;
        await File.WriteAllTextAsync(Path.Combine(outside, "Todo.md"), "old", TestContext.Current.CancellationToken);
        Directory.CreateSymbolicLink(Path.Combine(_vault, "linked"), outside);

        var swap = _files.Swap(Path.Combine(_vault, "linked", "Todo.md"), "new", _vault);

        Assert.NotNull(swap.Problem);
        Assert.Null(swap.CameOut);
        Assert.Equal("old", await File.ReadAllTextAsync(Path.Combine(outside, "Todo.md"), TestContext.Current.CancellationToken));
        Assert.Equal(["Todo.md"], Directory.EnumerateFileSystemEntries(outside).Select(Path.GetFileName));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_tmp_file_already_there_is_a_problem_and_the_note_is_untouched()
    {
        await File.WriteAllTextAsync(_note, "old", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(_vault, ".Todo.md.sindri-tmp"), "left", TestContext.Current.CancellationToken);

        var swap = _files.Swap(_note, "new", _vault);

        Assert.Equal((null, "a tmp file is already there", "old", "left"), (swap.CameOut, swap.Problem, await File.ReadAllTextAsync(_note, TestContext.Current.CancellationToken), await File.ReadAllTextAsync(Path.Combine(_vault, ".Todo.md.sindri-tmp"), TestContext.Current.CancellationToken)));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Leftover_finds_the_tmp_file_and_delete_removes_it()
    {
        Assert.Null(_files.Leftover(_note));
        var tmp = Path.Combine(_vault, ".Todo.md.sindri-tmp");
        await File.WriteAllTextAsync(tmp, "left", TestContext.Current.CancellationToken);

        Assert.Equal(tmp, _files.Leftover(_note));
        _files.Delete(tmp);
        _files.Delete(tmp);   // a missing file is fine

        Assert.Null(_files.Leftover(_note));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Keep_renames_the_file_with_the_utc_time_in_its_name()
    {
        var tmp = Path.Combine(_vault, ".Todo.md.sindri-tmp");
        await File.WriteAllTextAsync(tmp, "kept", TestContext.Current.CancellationToken);

        var kept = _files.Keep(tmp, new DateTimeOffset(2026, 11, 8, 14, 30, 5, TimeSpan.FromHours(2)));

        Assert.Equal(Path.Combine(_vault, ".Todo.md.sindri-tmp.keep-20261108T123005Z"), kept);
        Assert.Equal(("kept", false), (await File.ReadAllTextAsync(kept, TestContext.Current.CancellationToken), File.Exists(tmp)));
    }

    [Fact, Trait("Lane", "integration")]
    public void The_intent_round_trips_and_clears()
    {
        Assert.Null(_files.ReadIntent());

        _files.WriteIntent("aaa");
        _files.WriteIntent("bbb");
        Assert.Equal("bbb", _files.ReadIntent());

        _files.ClearIntent();
        Assert.Null(_files.ReadIntent());
        if (!OperatingSystem.IsWindows())
        {
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, File.GetUnixFileMode(TodoFolder));
        }
    }

    [Fact, Trait("Lane", "integration")]
    public void A_base_round_trips_and_is_named_by_its_hash_with_mode_0600()
    {
        var hash = _files.SaveBase(new NoteBase(["- [ ] one", "- [x] two"]));

        Assert.Equal(Sha(Encoding.UTF8.GetBytes("- [ ] one\n- [x] two\n")), hash);
        Assert.Equal(["- [ ] one", "- [x] two"], _files.LoadBase(hash)!.Lines);
        var path = Path.Combine(TodoFolder, "note-base-" + hash + ".md");
        Assert.True(File.Exists(path));
        if (!OperatingSystem.IsWindows())
        {
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));
        }
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_tampered_or_missing_base_loads_as_null()
    {
        var hash = _files.SaveBase(new NoteBase(["- [ ] one"]));
        Assert.Null(_files.LoadBase(new string('0', 64)));

        await File.WriteAllTextAsync(Path.Combine(TodoFolder, "note-base-" + hash + ".md"), "- [ ] tampered\n", TestContext.Current.CancellationToken);

        Assert.Null(_files.LoadBase(hash));
    }

    [Fact, Trait("Lane", "integration")]
    public void Deleting_bases_keeps_only_the_named_one_or_none()
    {
        var one = _files.SaveBase(new NoteBase(["- [ ] one"]));
        var two = _files.SaveBase(new NoteBase(["- [ ] two"]));

        _files.DeleteBases(two);
        Assert.Equal((null, true), (_files.LoadBase(one), _files.LoadBase(two) is not null));

        _files.DeleteBases(keep: null);
        Assert.Null(_files.LoadBase(two));
    }

    private static int Link(string existing, string link)
    {
        var info = new System.Diagnostics.ProcessStartInfo("/bin/ln") { UseShellExecute = false, RedirectStandardError = true };
        info.ArgumentList.Add(existing);
        info.ArgumentList.Add(link);
        using var run = System.Diagnostics.Process.Start(info)!;
        run.WaitForExit();
        return run.ExitCode;
    }
}
