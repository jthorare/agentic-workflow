using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace Aw.Sindri.Infrastructure.Workflow.Code;

/// <summary>Compiled assemblies on disk, keyed by the sha256 of the body and the compiler's fingerprint, so the same code compiles once.</summary>
/// <param name="compiler">Compiles a body that is not cached.</param>
/// <param name="directory">The folder the assemblies live in.</param>
/// <param name="time">The clock that ages the cached assemblies; the system clock when null.</param>
/// <param name="maxFiles">How many assemblies the folder keeps, the ones touched in the last hour aside.</param>
internal sealed class CompiledCodeCache(ICodeCompiler compiler, string directory, TimeProvider? time = null, int maxFiles = CompiledCodeCache.DefaultMaxFiles)
{
    /// <summary>How many assemblies the folder keeps by default.</summary>
    internal const int DefaultMaxFiles = 1000;

    /// <summary>The clock that ages the cached assemblies.</summary>
    internal TimeProvider Time { get; } = time ?? TimeProvider.System;

    /// <summary>How many assemblies the folder keeps.</summary>
    internal int MaxFiles { get; } = maxFiles;

    /// <summary>How many failing bodies the check remembers before it forgets them all.</summary>
    internal const int FailedLimit = 256;

    // why: a failing body writes no assembly, so without this the editor's check recompiles the same typo on every keystroke
    private readonly ConcurrentDictionary<string, IReadOnlyList<CodeDiagnostic>> _failed = new(StringComparer.Ordinal);

    /// <summary>Gives the assembly for the body, compiling it only when it is not cached.</summary>
    /// <param name="body">The method body.</param>
    /// <param name="cancellationToken">Stops the compile.</param>
    /// <returns>The assembly's path, or the compile errors.</returns>
    public CachedCode Resolve(string body, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        cancellationToken.ThrowIfCancellationRequested();
        var path = Path.Combine(directory, Key(body) + ".dll");
        if (File.Exists(path))
        {
            Touch(path);
            return new CachedCode(path, []);
        }

        var compiled = compiler.Compile(body, cancellationToken);
        if (compiled.Assembly is not { } bytes)
        {
            return new CachedCode(AssemblyPath: null, compiled.Diagnostics);
        }

        _ = Directory.CreateDirectory(directory);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        File.WriteAllBytes(temporary, bytes);
        File.Move(temporary, path, overwrite: true);   // a second writer of the same key writes the same bytes
        Touch(path);
        Prune();
        return new CachedCode(path, []);
    }

    /// <summary>The validator's compile check for the body, under the run's key: a body that compiles is cached on disk for the run, and a failing one's errors are remembered here.</summary>
    /// <param name="body">The method body.</param>
    /// <param name="cancellationToken">Stops the compile.</param>
    /// <returns>The compile errors with their line and column in the body.</returns>
    public IReadOnlyList<CodeDiagnostic> Check(string body, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        cancellationToken.ThrowIfCancellationRequested();
        var key = Key(body);
        if (_failed.TryGetValue(key, out var known))
        {
            return known;
        }

        var resolved = Resolve(body, cancellationToken);
        if (resolved.AssemblyPath is null)
        {
            if (_failed.Count >= FailedLimit)
            {
                _failed.Clear();
            }

            _failed[key] = resolved.Diagnostics;
        }

        return resolved.Diagnostics;
    }

    /// <summary>How long an assembly stays after its last use.</summary>
    internal static readonly TimeSpan MaxAge = TimeSpan.FromDays(30);

    /// <summary>How long a file is safe from removal after its last write: it may be mid-write by another process or about to be copied by the runner.</summary>
    internal static readonly TimeSpan Grace = TimeSpan.FromHours(1);

    // why: the last-write time is the last use, so the age and the cap both drop the least recently used body first
    private void Touch(string path)
    {
        try
        {
            File.SetLastWriteTimeUtc(path, Time.GetUtcNow().UtcDateTime);
        }
        catch (IOException)
        {
            // another writer holds the file; its own write makes it young
        }
    }

    // why: only a new write grows the folder, so a hit never pays for the folder scan
    private void Prune()
    {
        var now = Time.GetUtcNow().UtcDateTime;
        var folder = new DirectoryInfo(directory);
        foreach (var leftover in folder.EnumerateFiles("*.tmp"))
        {
            if (now - leftover.LastWriteTimeUtc > Grace)
            {
                Delete(leftover);
            }
        }

        var kept = new List<FileInfo>();
        foreach (var assembly in folder.EnumerateFiles("*.dll"))
        {
            var age = now - assembly.LastWriteTimeUtc;
            if (age > MaxAge)
            {
                Delete(assembly);
            }
            else
            {
                kept.Add(assembly);
            }
        }

        var over = kept.Count - MaxFiles;
        foreach (var oldest in kept.OrderBy(file => file.LastWriteTimeUtc).Take(Math.Max(over, 0)))
        {
            if (now - oldest.LastWriteTimeUtc > Grace)
            {
                Delete(oldest);
            }
        }
    }

    private static void Delete(FileInfo file)
    {
        try
        {
            file.Delete();
        }
        catch (IOException)
        {
            // another writer or the runner holds it; the next write tries again
        }
    }

    private string Key(string body) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{compiler.Fingerprint}\n{body}"))).ToLowerInvariant();
}
