using System.Security.Cryptography;
using System.Text;

namespace Aw.Sindri.Infrastructure.Workflow.Code;

/// <summary>Compiled assemblies on disk, keyed by the sha256 of the body and the compiler's fingerprint, so the same code compiles once.</summary>
/// <param name="compiler">Compiles a body that is not cached.</param>
/// <param name="directory">The folder the assemblies live in.</param>
internal sealed class CompiledCodeCache(ICodeCompiler compiler, string directory)
{
    /// <summary>Gives the assembly for the body, compiling it only when it is not cached.</summary>
    /// <param name="body">The method body.</param>
    /// <param name="cancellationToken">Stops the compile.</param>
    /// <returns>The assembly's path, or the compile errors.</returns>
    public CachedCode Resolve(string body, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{compiler.Fingerprint}\n{body}"))).ToLowerInvariant();
        var path = Path.Combine(directory, key + ".dll");
        if (File.Exists(path))
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

    /// <summary>The validator's compile check for the body: its compile errors, empty when it compiles.</summary>
    /// <param name="body">The method body.</param>
    /// <param name="cancellationToken">Stops the compile.</param>
    /// <returns>The compile errors with their line and column in the body.</returns>
    public IReadOnlyList<CodeDiagnostic> Check(string body, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        return compiler.Compile(body, cancellationToken).Diagnostics;
    }
}
