using Aw.Judge.Core.Ports;

namespace Aw.Judge.Infrastructure.Processes;

/// <summary>IExecutableLocator over PATH: a regular file with an execute bit (any file on Windows) in a PATH directory.</summary>
/// <param name="environment">Reads PATH (each call, as the TS judge reads process.env).</param>
internal sealed class PathExecutableLocator(Func<string, string?> environment) : IExecutableLocator
{
    /// <inheritdoc />
    public bool IsOnPath(string name) => SearchPath.Find(environment("PATH"), name) is not null;
}
