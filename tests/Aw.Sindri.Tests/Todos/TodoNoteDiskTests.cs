using Aw.Sindri.Infrastructure.Todos;

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
}
