using Aw.Sindri.Infrastructure.Locks;
using Aw.Sindri.Infrastructure.Todos;
using Aw.Sindri.Tests.Sandbox.Bubblewrap;

namespace Aw.Sindri.Tests.Todos;

/// <summary>The real disk side of Check note on a temp folder (todo spec §4.5): a note's facts, the symlink refusal, and the swap probe that leaves nothing behind.</summary>
public sealed class TodoNoteDiskTests : IDisposable
{
    private readonly string _vault = Directory.CreateTempSubdirectory("aw-todo-disk-").FullName;

    public void Dispose() => Directory.Delete(_vault, recursive: true);

    [Fact, Trait("Lane", "integration")]
    public void A_missing_note_does_not_exist_and_has_no_text()
    {
        var facts = new FileTodoNoteDisk().Inspect(_vault, "Todo.md");

        Assert.Equal((false, false, null), (facts.Exists, facts.IsSymlink, facts.Text));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task An_existing_note_is_read_with_one_link()
    {
        await File.WriteAllTextAsync(Path.Combine(_vault, "Todo.md"), "- [ ] one\n", TestContext.Current.CancellationToken);

        var facts = new FileTodoNoteDisk().Inspect(_vault, "Todo.md");

        Assert.Equal((true, false, 1, "- [ ] one\n"), (facts.Exists, facts.IsSymlink, facts.LinkCount, facts.Text));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_note_that_is_a_symlink_and_a_note_inside_a_symlinked_folder_are_both_symlinks()
    {
        var real = Path.Combine(_vault, "real");
        _ = Directory.CreateDirectory(real);
        await File.WriteAllTextAsync(Path.Combine(real, "Todo.md"), "- [ ] one\n", TestContext.Current.CancellationToken);
        _ = File.CreateSymbolicLink(Path.Combine(_vault, "link.md"), Path.Combine(real, "Todo.md"));
        _ = File.CreateSymbolicLink(Path.Combine(_vault, "linked-folder"), real);
        var disk = new FileTodoNoteDisk();

        Assert.True(disk.Inspect(_vault, "link.md").IsSymlink);
        Assert.True(disk.Inspect(_vault, "linked-folder/Todo.md").IsSymlink);
        Assert.Null(disk.Inspect(_vault, "link.md").Text);   // a symlink is never read
    }

    [Fact, Trait("Lane", "integration")]
    public void A_dangling_symlink_at_the_note_and_one_on_a_folder_on_the_way_are_refused()
    {
        _ = File.CreateSymbolicLink(Path.Combine(_vault, "dangling.md"), Path.Combine(_vault, "nowhere.md"));
        _ = File.CreateSymbolicLink(Path.Combine(_vault, "dangling-folder"), Path.Combine(_vault, "nowhere"));
        var disk = new FileTodoNoteDisk();

        Assert.True(disk.Inspect(_vault, "dangling.md").IsSymlink);
        Assert.True(disk.Inspect(_vault, "dangling-folder/Todo.md").IsSymlink);
    }

    [Fact, Trait("Lane", "integration")]
    public void The_probe_of_a_writable_folder_says_writable_and_leaves_no_file_behind()
    {
        var facts = new FileTodoNoteDisk().Probe(_vault, "Daily/Todo.md");   // Daily doesn't exist: the vault's own folder is probed

        Assert.True(facts.Writable);
        Assert.Empty(Directory.GetFileSystemEntries(_vault));
    }

    [Fact, Trait("Lane", "integration")]
    public void The_probe_of_a_folder_that_is_not_there_is_not_writable()
    {
        var facts = new FileTodoNoteDisk().Probe(Path.Combine(_vault, "gone"), "Todo.md");

        Assert.Equal((false, false), (facts.Writable, facts.CanSwap));
    }

    private static FakeProcesses Stat() => new FakeProcesses().When(static (program, _) => program == "/usr/bin/stat", FakeProcesses.Ok("1\n"));

    // 2417: one idle pass is Inspect (or Read) plus Probe; a second pass on the same note must cost no stat process and no swap probe
    [Fact, Trait("Lane", "integration")]
    public async Task A_second_pass_on_an_unchanged_note_spawns_no_stat_and_runs_no_swap_probe()
    {
        await File.WriteAllTextAsync(Path.Combine(_vault, "Todo.md"), "- [ ] one\n", TestContext.Current.CancellationToken);
        var processes = Stat();
        var swaps = 0;
        var disk = new FileTodoNoteDisk(new NoteLinkCounter(processes), (a, b) => { swaps++; return Libc.SwapFiles(a, b); });
        _ = disk.Inspect(_vault, "Todo.md");
        _ = disk.Probe(_vault, "Todo.md");
        var statsAfterFirst = processes.Calls.Count;
        var swapsAfterFirst = swaps;

        var second = disk.Inspect(_vault, "Todo.md");
        var probed = disk.Probe(_vault, "Todo.md");

        Assert.Equal((1, 1, 1), (statsAfterFirst, swapsAfterFirst, second.LinkCount));
        Assert.Equal((1, 1), (processes.Calls.Count, swaps));
        Assert.True(probed.Writable);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_note_whose_size_or_mtime_changed_gets_its_links_counted_again()
    {
        var note = Path.Combine(_vault, "Todo.md");
        await File.WriteAllTextAsync(note, "- [ ] one\n", TestContext.Current.CancellationToken);
        var processes = Stat();
        var disk = new FileTodoNoteDisk(new NoteLinkCounter(processes), Libc.SwapFiles);
        _ = disk.Inspect(_vault, "Todo.md");

        await File.WriteAllTextAsync(note, "- [ ] one\n- [ ] two\n", TestContext.Current.CancellationToken);   // size changed
        _ = disk.Inspect(_vault, "Todo.md");
        File.SetLastWriteTimeUtc(note, File.GetLastWriteTimeUtc(note).AddMinutes(5));   // mtime only
        _ = disk.Inspect(_vault, "Todo.md");
        _ = disk.Inspect(_vault, "Todo.md");   // unchanged since the last look

        Assert.Equal(3, processes.Calls.Count);
    }

    [Fact, Trait("Lane", "integration")]
    public void The_swap_probe_runs_again_for_another_folder()
    {
        _ = Directory.CreateDirectory(Path.Combine(_vault, "Daily"));
        var swaps = 0;
        var disk = new FileTodoNoteDisk(new NoteLinkCounter(Stat()), (a, b) => { swaps++; return Libc.SwapFiles(a, b); });

        _ = disk.Probe(_vault, "Todo.md");
        _ = disk.Probe(_vault, "Daily/Todo.md");
        _ = disk.Probe(_vault, "Daily/Todo.md");

        Assert.Equal(2, swaps);
    }
}
