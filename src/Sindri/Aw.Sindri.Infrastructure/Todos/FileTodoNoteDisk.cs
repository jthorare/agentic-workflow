using System.Globalization;
using Aw.Sindri.Core.Todos.Note;
using Aw.Sindri.Infrastructure.Locks;
using Aw.Sindri.Infrastructure.Sandbox;

namespace Aw.Sindri.Infrastructure.Todos;

/// <summary>The note check's disk side (todo spec §4.5): reads the note without following a symlink, and probes the nearest existing folder with two temporary files and the platform's swap call (<see cref="Libc.SwapFiles"/>).</summary>
internal sealed class FileTodoNoteDisk : ITodoNoteDisk
{
    private const long MaxBytes = 8L * 1024 * 1024;

    public bool VaultExists(string vaultRoot) => Directory.Exists(vaultRoot);

    public NoteFileFacts Inspect(string vaultRoot, string note)
    {
        ArgumentNullException.ThrowIfNull(vaultRoot);
        ArgumentNullException.ThrowIfNull(note);
        var path = Path.Combine(vaultRoot, note);
        if (AnySymlink(vaultRoot, note))
        {
            return new NoteFileFacts(Exists: File.Exists(path), IsSymlink: true, LinkCount: 0, Text: null, Unreadable: null);
        }

        if (!File.Exists(path))
        {
            return new NoteFileFacts(Exists: false, IsSymlink: false, LinkCount: 0, Text: null, Unreadable: null);
        }

        if (Links(path) is not { } links)
        {
            return new NoteFileFacts(Exists: true, IsSymlink: false, LinkCount: 0, Text: null, Unreadable: "its hard links can't be counted");
        }

        try
        {
            var info = new FileInfo(path);
            return info.Length > MaxBytes
                ? new NoteFileFacts(Exists: true, IsSymlink: false, LinkCount: links, Text: null, Unreadable: "it is over 8 MB")
                : new NoteFileFacts(Exists: true, IsSymlink: false, LinkCount: links, Text: File.ReadAllText(path), Unreadable: null);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return new NoteFileFacts(Exists: true, IsSymlink: false, LinkCount: links, Text: null, Unreadable: e.Message);
        }
    }

    public NoteFolderFacts Probe(string vaultRoot, string note)
    {
        ArgumentNullException.ThrowIfNull(vaultRoot);
        ArgumentNullException.ThrowIfNull(note);
        var folder = NearestFolder(vaultRoot, note);
        if (!Directory.Exists(folder))
        {
            return new NoteFolderFacts(Writable: false, CanSwap: false);
        }

        var stem = ".sindri-probe-" + Guid.NewGuid().ToString("N");
        var first = Path.Combine(folder, stem + "-a");
        var second = Path.Combine(folder, stem + "-b");
        try
        {
            File.WriteAllText(first, "a");
            File.WriteAllText(second, "b");
            var swapped = Libc.SwapFiles(first, second) == 0 && File.ReadAllText(first) == "b" && File.ReadAllText(second) == "a";
            return new NoteFolderFacts(Writable: true, CanSwap: swapped);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return new NoteFolderFacts(Writable: false, CanSwap: false);
        }
        finally
        {
            DeleteQuietly(first);
            DeleteQuietly(second);
        }
    }

    // the folder the note would live in, or the nearest one above it that exists, never above the vault
    private static string NearestFolder(string vaultRoot, string note)
    {
        var folder = Path.GetDirectoryName(Path.Combine(vaultRoot, note)) ?? vaultRoot;
        while (!Directory.Exists(folder) && folder.Length > vaultRoot.Length)
        {
            folder = Path.GetDirectoryName(folder) ?? vaultRoot;
        }

        return folder;
    }

    // the note itself or any folder on the way to it inside the vault; a dangling link counts, so there is no exists gate
    internal static bool AnySymlink(string vaultRoot, string note)
    {
        var at = vaultRoot;
        foreach (var segment in note.Split('/'))
        {
            at = Path.Combine(at, segment);
            if (new FileInfo(at).LinkTarget is not null)
            {
                return true;
            }
        }

        return false;
    }

    // .NET has no link count; stat prints it (GNU: -c %h, BSD and macOS: -f %l). Null when stat can't answer: the note can't be checked, so it is refused.
    internal static int? Links(string path)
    {
        var run = new SandboxProcesses().Run("/usr/bin/stat", OperatingSystem.IsMacOS() ? ["-f", "%l", path] : ["-c", "%h", path], TimeSpan.FromSeconds(5));
        return run.Succeeded && int.TryParse(run.Output.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var links) ? links : null;
    }

    private static void DeleteQuietly(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // a probe file that can't be removed is left for the next sweep of the vault's owner; the probe's answer stands
        }
    }
}
