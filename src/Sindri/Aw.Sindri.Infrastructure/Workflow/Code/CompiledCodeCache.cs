using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace Aw.Sindri.Infrastructure.Workflow.Code;

/// <summary>Compiled assemblies on disk, keyed by the sha256 of the body and the compiler's fingerprint, so the same code compiles once.</summary>
/// <param name="compiler">Compiles a body that is not cached.</param>
/// <param name="directory">The folder the assemblies live in.</param>
internal sealed class CompiledCodeCache(ICodeCompiler compiler, string directory)
{
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
        if (new FileInfo(path) is { Exists: true, Length: > 0 })   // why: a crash can leave an empty file; it is a miss, so the next compile overwrites it
        {
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

    private string Key(string body) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{compiler.Fingerprint}\n{body}"))).ToLowerInvariant();
}
