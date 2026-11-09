using System.Security.Cryptography;
using System.Text;
using Aw.Sindri.Core.Todos.Note;
using Xunit;

namespace Aw.Sindri.Testing;

/// <summary>
/// The contract every <see cref="ITodoNoteFiles"/> honours (todo spec §4.5 Writing steps 1-5, "The base"), run against <see cref="FakeTodoNoteFiles"/> and the files on disk. It states what the sync can rely on:
/// the note's read, the swap and the tmp file beside the note, the kept file, the intent file and the base files. Details a real disk adds (modes, the 1 MB limit, links) stay in the disk's own tests.
/// </summary>
public abstract class TodoNoteFilesContract
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>Gets the note's path: a '.md' file named 'Todo.md' in a folder that exists and holds nothing else.</summary>
    protected abstract string NotePath { get; }

    /// <summary>The files under test, with nothing stored yet.</summary>
    /// <returns>The files.</returns>
    protected abstract ITodoNoteFiles Create();

    /// <summary>Puts a file in place, replacing what is there.</summary>
    /// <param name="path">The file.</param>
    /// <param name="text">Its text.</param>
    protected abstract void Put(string path, string text);

    /// <summary>The text of a file.</summary>
    /// <param name="path">The file.</param>
    /// <returns>The text, or null when the file isn't there.</returns>
    protected abstract string? Text(string path);

    private string Tmp => Path.Combine(Path.GetDirectoryName(NotePath)!, ".Todo.md.sindri-tmp");

    private static string Sha(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    /// <summary>A note reads with its text, its size in bytes and the sha256 of those bytes.</summary>
    [Fact]
    public void A_note_reads_with_its_text_size_and_hash()
    {
        var files = Create();
        Put(NotePath, "- [ ] one \U0001F4C5\n");

        var read = files.Read(NotePath);

        Expect.Equal(("- [ ] one \U0001F4C5\n", Sha("- [ ] one \U0001F4C5\n"), false, null), (read.Text, read.Hash, read.Missing, read.Problem));
        Expect.Equal(Encoding.UTF8.GetByteCount("- [ ] one \U0001F4C5\n"), read.Bytes);
    }

    /// <summary>A note that isn't there reads as missing, not as a problem.</summary>
    [Fact]
    public void A_missing_note_reads_as_missing()
    {
        var read = Create().Read(NotePath);

        Expect.Equal((null, null, 0L, true, null), (read.Text, read.Hash, read.Bytes, read.Missing, read.Problem));
    }

    /// <summary>A note of 0 bytes is there, with no text to speak of: never "missing".</summary>
    [Fact]
    public void An_empty_note_reads_as_zero_bytes_and_not_missing()
    {
        var files = Create();
        Put(NotePath, string.Empty);

        var read = files.Read(NotePath);

        Expect.Equal((0L, false, null), (read.Bytes, read.Missing, read.Problem));
    }

    /// <summary>A quiet read of a file nobody writes to settles at once and reads what is there.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_quiet_read_of_a_file_nobody_writes_settles()
    {
        var files = Create();
        Put(NotePath, "steady");

        var (read, settled) = await files.ReadQuiet(NotePath, TimeSpan.FromMilliseconds(200), TimeSpan.FromSeconds(10), Token);

        Expect.Equal(("steady", true), (read.Text, settled));
    }

    /// <summary>A swap of an existing note puts the new text in place and the old text in the tmp file beside the note.</summary>
    [Fact]
    public void Swapping_an_existing_note_leaves_the_old_text_in_the_tmp_file()
    {
        var files = Create();
        Put(NotePath, "old");

        var swap = files.Swap(NotePath, "new", Path.GetDirectoryName(NotePath)!);

        Expect.Equal((Tmp, null, "new", "old"), (swap.CameOut, swap.Problem, Text(NotePath), Text(Tmp)));
    }

    /// <summary>A swap of a missing note creates it; nothing came out and no tmp file stays.</summary>
    [Fact]
    public void Swapping_a_missing_note_creates_it_and_nothing_came_out()
    {
        var files = Create();

        var swap = files.Swap(NotePath, "fresh", Path.GetDirectoryName(NotePath)!);

        Expect.Equal((null, null, "fresh", null), (swap.CameOut, swap.Problem, Text(NotePath), Text(Tmp)));
    }

    /// <summary>A tmp file from an earlier run stops the swap with a problem and leaves the note and the tmp file alone.</summary>
    [Fact]
    public void A_tmp_file_already_there_stops_the_swap_and_both_files_stay()
    {
        var files = Create();
        Put(NotePath, "old");
        Put(Tmp, "left");

        var swap = files.Swap(NotePath, "new", Path.GetDirectoryName(NotePath)!);

        Expect.Equal((null, "a tmp file is already there", "old", "left"), (swap.CameOut, swap.Problem, Text(NotePath), Text(Tmp)));
    }

    /// <summary>Leftover finds the tmp file beside the note; Delete removes it and a second Delete is no error.</summary>
    [Fact]
    public void Leftover_finds_the_tmp_file_and_delete_removes_it()
    {
        var files = Create();
        Expect.Equal(expected: null, files.Leftover(NotePath));
        Put(Tmp, "left");

        Expect.Equal(Tmp, files.Leftover(NotePath));
        files.Delete(Tmp);
        files.Delete(Tmp);

        Expect.Equal(expected: null, files.Leftover(NotePath));
    }

    /// <summary>A '-new' file beside the tmp file is a write that never finished: Leftover deletes it and never offers it as the leftover.</summary>
    [Fact]
    public void Leftover_deletes_an_unfinished_new_file_and_never_offers_it()
    {
        var files = Create();
        Put(Tmp + "-new", "half a wri");

        Expect.Equal(expected: null, files.Leftover(NotePath));
        Expect.Equal(expected: null, Text(Tmp + "-new"));
    }

    /// <summary>Keep renames a file to its own name plus '.keep-' and a time; the text goes with it and the old name is free.</summary>
    [Fact]
    public void Keep_moves_the_file_to_a_name_that_starts_with_the_old_one_and_keep()
    {
        var files = Create();
        Put(Tmp, "kept");

        var kept = files.Keep(Tmp, new DateTimeOffset(2026, 11, 8, 12, 30, 5, TimeSpan.Zero));

        Expect.True(kept.StartsWith(Tmp + ".keep-", StringComparison.Ordinal));
        Expect.Equal(("kept", null, null), (Text(kept), Text(Tmp), files.Leftover(NotePath)));
    }

    /// <summary>The intent file holds the last hash written until it is cleared.</summary>
    [Fact]
    public void The_intent_round_trips_and_clears()
    {
        var files = Create();
        Expect.Equal(expected: null, files.ReadIntent());

        files.WriteIntent("aaa");
        files.WriteIntent("bbb");
        Expect.Equal("bbb", files.ReadIntent());

        files.ClearIntent();
        Expect.Equal(expected: null, files.ReadIntent());
    }

    /// <summary>A saved base loads by the hash that names it; the same lines give the same hash, other lines another; an unknown hash loads nothing.</summary>
    [Fact]
    public void A_base_loads_by_its_hash_and_an_unknown_hash_loads_nothing()
    {
        var files = Create();

        var hash = files.SaveBase(new NoteBase(["- [ ] one", "- [x] two"]));

        Expect.Sequence(["- [ ] one", "- [x] two"], files.LoadBase(hash)!.Lines);
        Expect.Equal(hash, files.SaveBase(new NoteBase(["- [ ] one", "- [x] two"])));
        Expect.True(!string.Equals(hash, files.SaveBase(new NoteBase(["- [ ] other"])), StringComparison.Ordinal));
        Expect.Equal(expected: null, files.LoadBase(new string('0', 64)));
    }

    /// <summary>A base with no lines saves and loads back empty.</summary>
    [Fact]
    public void A_base_with_no_lines_loads_back_empty()
    {
        var files = Create();

        var hash = files.SaveBase(new NoteBase([]));

        Expect.Equal(0, files.LoadBase(hash)!.Lines.Count);
    }

    /// <summary>DeleteBases keeps the named base only, or none when it is given null.</summary>
    [Fact]
    public void Deleting_bases_keeps_only_the_named_one_or_none()
    {
        var files = Create();
        var one = files.SaveBase(new NoteBase(["- [ ] one"]));
        var two = files.SaveBase(new NoteBase(["- [ ] two"]));

        files.DeleteBases(two);
        Expect.Equal((false, true), (files.LoadBase(one) is not null, files.LoadBase(two) is not null));

        files.DeleteBases(keep: null);
        Expect.Equal(expected: null, files.LoadBase(two));
    }
}
