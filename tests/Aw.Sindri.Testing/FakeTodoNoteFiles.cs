using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Aw.Sindri.Core.Todos.Note;

namespace Aw.Sindri.Testing;

/// <summary>
/// The note sync's files in memory (<see cref="ITodoNoteFiles"/>): notes, tmp files, the intent and the bases by hash. A test injects the races of todo spec §4.5 through the hooks:
/// <see cref="BeforeSwap"/> (an edit lands between the pass's read and its swap), <see cref="AfterSwap"/> (an editor writes into what came out), and the empty came-out file.
/// </summary>
public sealed class FakeTodoNoteFiles : ITodoNoteFiles
{
    private readonly Dictionary<string, NoteBase> _bases = new(StringComparer.Ordinal);

    /// <summary>Gets every file by path: notes, tmp files and kept files.</summary>
    public IDictionary<string, string> Files { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Gets the paths a read of which answers a problem instead of the text.</summary>
    public IDictionary<string, string> Problems { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Gets or sets a problem every Swap answers instead of writing.</summary>
    public string? SwapProblem { get; set; }

    /// <summary>Gets or sets an exception WriteIntent throws, as a failing disk does.</summary>
    public Exception? WriteIntentThrows { get; set; }

    /// <summary>Gets or sets what runs just before a swap, with the note's path.</summary>
    public Action<string>? BeforeSwap { get; set; }

    /// <summary>Gets or sets what runs just after a swap, with the tmp path that now holds what was at the note.</summary>
    public Action<string>? AfterSwap { get; set; }

    /// <summary>Gets how many swaps happened.</summary>
    public int Swaps { get; private set; }

    /// <summary>Gets the text each swap wrote, in order.</summary>
    public IList<string> Written { get; } = [];

    /// <summary>Gets each quiet read as (path, quiet, max).</summary>
    public IList<(string Path, TimeSpan Quiet, TimeSpan Max)> QuietReads { get; } = [];

    /// <summary>Gets the 'keep' to delete bases was called with, per call.</summary>
    public IList<string?> BasesDeleted { get; } = [];

    /// <summary>Gets or sets the intent file's hash, or null for none.</summary>
    public string? Intent { get; set; }

    /// <summary>Gets how many base files are stored.</summary>
    public int BaseCount => _bases.Count;

    /// <summary>The lower-case hex sha256 of a text, as a read reports it.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The hash.</returns>
    public static string HashOf(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    /// <summary>The tmp path beside a note.</summary>
    /// <param name="note">The note's path.</param>
    /// <returns>The path.</returns>
    public static string TmpOf(string note) => Path.Combine(Path.GetDirectoryName(note) ?? string.Empty, "." + Path.GetFileName(note) + ".sindri-tmp");

    /// <inheritdoc/>
    public NoteRead Read(string note)
    {
        ArgumentNullException.ThrowIfNull(note);
        if (Problems.TryGetValue(note, out var problem))
        {
            return new NoteRead(Text: null, Hash: null, Bytes: 0, Missing: false, problem);
        }

        return Files.TryGetValue(note, out var text)
            ? new NoteRead(text, HashOf(text), Encoding.UTF8.GetByteCount(text), Missing: false, Problem: null)
            : new NoteRead(Text: null, Hash: null, Bytes: 0, Missing: true, Problem: null);
    }

    /// <inheritdoc/>
    public ValueTask<(NoteRead Read, bool Settled)> ReadQuiet(string path, TimeSpan quiet, TimeSpan max, CancellationToken cancellationToken)
    {
        QuietReads.Add((path, quiet, max));
        return ValueTask.FromResult((Read(path), true));
    }

    /// <inheritdoc/>
    public void WriteIntent(string hash)
    {
        if (WriteIntentThrows is { } thrown)
        {
            throw thrown;
        }

        Intent = hash;
    }

    /// <inheritdoc/>
    public string? ReadIntent() => Intent;

    /// <inheritdoc/>
    public void ClearIntent() => Intent = null;

    /// <inheritdoc/>
    public NoteSwap Swap(string note, string text)
    {
        ArgumentNullException.ThrowIfNull(note);
        ArgumentNullException.ThrowIfNull(text);
        if (SwapProblem is { } problem)
        {
            return new NoteSwap(CameOut: null, problem);
        }

        var tmp = TmpOf(note);
        if (Files.ContainsKey(tmp))
        {
            return new NoteSwap(CameOut: null, "a tmp file is already there");
        }

        BeforeSwap?.Invoke(note);
        Swaps++;
        Written.Add(text);
        if (!Files.TryGetValue(note, out var before))
        {
            Files[note] = text;
            return new NoteSwap(CameOut: null, Problem: null);
        }

        Files[tmp] = before;
        Files[note] = text;
        AfterSwap?.Invoke(tmp);
        return new NoteSwap(tmp, Problem: null);
    }

    /// <inheritdoc/>
    public string? Leftover(string note) => Files.ContainsKey(TmpOf(note)) ? TmpOf(note) : null;

    /// <inheritdoc/>
    public void Delete(string path) => _ = Files.Remove(path);

    /// <inheritdoc/>
    public string Keep(string path, DateTimeOffset at)
    {
        var kept = path + ".keep-" + at.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        Files[kept] = Files.TryGetValue(path, out var kept0) ? kept0 : string.Empty;
        _ = Files.Remove(path);
        return kept;
    }

    /// <inheritdoc/>
    public string SaveBase(NoteBase noteBase)
    {
        ArgumentNullException.ThrowIfNull(noteBase);
        var hash = HashOf(string.Join('\n', noteBase.Lines));
        _bases[hash] = noteBase;
        return hash;
    }

    /// <inheritdoc/>
    public NoteBase? LoadBase(string hash) => _bases.GetValueOrDefault(hash);

    /// <inheritdoc/>
    public void DeleteBases(string? keep)
    {
        BasesDeleted.Add(keep);
        foreach (var hash in _bases.Keys.Where(h => keep is null || !string.Equals(h, keep, StringComparison.Ordinal)).ToList())
        {
            _ = _bases.Remove(hash);
        }
    }
}
