using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Aw.Kernel.Data;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Todos.Note;
using Aw.Sindri.Infrastructure.Locks;

namespace Aw.Sindri.Infrastructure.Todos;

/// <summary>
/// The note sync's files on disk (todo spec §4.5 Writing steps 1-5, "The base", Limits): the note read with its limits, the quiet wait, the tmp-file swap,
/// and Sindri's own folder '$AW_SINDRI_HOME/todo/' (mode 0700) holding the intent file and the base files (0600). This part is the note side; the other is in
/// FileTodoNoteFiles.Own.cs.
/// </summary>
internal sealed partial class FileTodoNoteFiles(ISindriHome home, TimeProvider clock, NoteLinkCounter linkCounter) : ITodoNoteFiles
{
    private const long MaxBytes = 1024 * 1024;
    private const string NewSuffix = "-new";
    private static readonly UTF8Encoding _utf8 = new(encoderShouldEmitUTF8Identifier: false);
    private static readonly TimeSpan _poll = TimeSpan.FromMilliseconds(100);

    private readonly string _folder = Path.Combine(home.Path, "todo");

    public FileTodoNoteFiles(ISindriHome home, TimeProvider clock)
        : this(home, clock, new NoteLinkCounter(new Sandbox.SandboxProcesses()))
    {
    }

    public NoteRead Read(string note)
    {
        ArgumentNullException.ThrowIfNull(note);
        try
        {
            if (new FileInfo(note).LinkTarget is not null)
            {
                return Problem("the note is a symlink");
            }

            if (!File.Exists(note))
            {
                return new NoteRead(Text: null, Hash: null, Bytes: 0, Missing: true, Problem: null);
            }

            var links = linkCounter.Count(note);
            if (links is null)
            {
                return Problem("its hard links can't be counted");
            }

            if (links > 1)
            {
                return Problem("the note has more than one hard link");
            }

            var size = new FileInfo(note).Length;
            if (size > MaxBytes)
            {
                return new NoteRead(Text: null, Hash: null, Bytes: size, Missing: false, Problem: "the note is over 1 MB");
            }

            var bytes = File.ReadAllBytes(note);
            return bytes.LongLength > MaxBytes
                ? new NoteRead(Text: null, Hash: null, Bytes: bytes.LongLength, Missing: false, Problem: "the note is over 1 MB")
                : new NoteRead(Text: _utf8.GetString(bytes), Hash: Hash(bytes), Bytes: bytes.LongLength, Missing: false, Problem: null);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return Problem(e.Message);
        }
    }

    public async ValueTask<(NoteRead Read, bool Settled)> ReadQuiet(string path, TimeSpan quiet, TimeSpan max, CancellationToken cancellationToken)
    {
        var started = clock.GetTimestamp();
        var stableSince = started;
        (long Bytes, DateTime Written, string? Hash, string? Problem, bool Missing)? last = null;
        while (true)
        {
            var read = Read(path);
            var seen = (read.Bytes, Written(path), read.Hash, read.Problem, read.Missing);
            var now = clock.GetTimestamp();
            if (last != seen)
            {
                last = seen;
                stableSince = now;
            }

            if (clock.GetElapsedTime(stableSince, now) >= quiet)
            {
                return (read, true);
            }

            if (clock.GetElapsedTime(started, now) >= max)
            {
                return (read, false);
            }

            await Task.Delay(Min(_poll, Min(quiet, max) / 2), clock, cancellationToken).ConfigureAwait(false);
        }
    }

    public NoteSwap Swap(string note, string text, string vaultRoot)
    {
        ArgumentNullException.ThrowIfNull(note);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(vaultRoot);
        var refused = PathProblem(note, vaultRoot);
        if (refused is not null)
        {
            return new NoteSwap(CameOut: null, Problem: refused);
        }

        var tmp = TmpPath(note);
        var exists = File.Exists(note) || new FileInfo(note).LinkTarget is not null;
        var problem = WriteTmp(note, tmp, text, exists);
        if (problem is not null)
        {
            return new NoteSwap(CameOut: null, Problem: problem);
        }

        return exists ? SwapIn(tmp, note) : MoveIn(tmp, note);
    }

    public string? Leftover(string note)
    {
        ArgumentNullException.ThrowIfNull(note);
        var tmp = TmpPath(note);
        File.Delete(tmp + NewSuffix);   // an unfinished write (never flushed and renamed): never read, never merged
        return File.Exists(tmp) ? tmp : null;
    }

    public void Delete(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        File.Delete(path);   // a missing file is not an error
    }

    public string Keep(string path, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(path);
        var kept = path + ".keep-" + at.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
        File.Move(path, kept);
        return kept;
    }

    private static string? PathProblem(string note, string vaultRoot)
    {
        var relative = Path.GetRelativePath(vaultRoot, note).Replace('\\', '/');
        if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
        {
            return "the note is outside the vault";
        }

        // every write re-checks the way to the note, not only its own link: a folder swapped for a symlink since the last check would send the write elsewhere
        return FileTodoNoteDisk.AnySymlink(vaultRoot, relative) ? "the note or a folder on the way to it is a symlink" : null;
    }

    private static string? WriteTmp(string note, string tmp, string text, bool exists)
    {
        var fresh = tmp + NewSuffix;
        try
        {
            if (File.Exists(tmp) || new FileInfo(tmp).LinkTarget is not null)
            {
                return "a tmp file is already there";
            }

            var mode = (exists ? FileModes.TryRead(note) : null) ?? (UnixFileMode.UserRead | UnixFileMode.UserWrite);
            using (var stream = CreateNew(fresh, mode))
            {
                stream.Write(_utf8.GetBytes(text));
                stream.Flush(flushToDisk: true);   // why: a kill after the rename must not leave a truncated tmp that Leftover merges as an edit
            }

            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(fresh, mode);   // the create mode went through the umask
            }

            File.Move(fresh, tmp, overwrite: false);
            return null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            File.Delete(fresh);   // ours: the exclusive create made it, or the rename left it
            return File.Exists(tmp) ? "a tmp file is already there" : e.Message;
        }
    }

    private static NoteSwap SwapIn(string tmp, string note)
    {
        var errno = Libc.SwapFiles(tmp, note);
        if (errno == 0)
        {
            return new NoteSwap(CameOut: tmp, Problem: null);
        }

        File.Delete(tmp);
        return new NoteSwap(CameOut: null, Problem: errno == Libc.NotImplemented ? "can't write to this disk" : "can't write to this disk (error " + errno.ToString(CultureInfo.InvariantCulture) + ")");
    }

    private static NoteSwap MoveIn(string tmp, string note)
    {
        try
        {
            File.Move(tmp, note, overwrite: false);
            return new NoteSwap(CameOut: null, Problem: null);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            File.Delete(tmp);
            return new NoteSwap(CameOut: null, Problem: File.Exists(note) ? "the note appeared meanwhile" : e.Message);
        }
    }

    private static string TmpPath(string note) =>
        Path.Combine(Path.GetDirectoryName(note) ?? ".", "." + Path.GetFileName(note) + ".sindri-tmp");

    private static DateTime Written(string path) => File.GetLastWriteTimeUtc(path);

    private static TimeSpan Min(TimeSpan a, TimeSpan b) => a < b ? a : b;

    private static NoteRead Problem(string why) => new(Text: null, Hash: null, Bytes: 0, Missing: false, Problem: why);

    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}

