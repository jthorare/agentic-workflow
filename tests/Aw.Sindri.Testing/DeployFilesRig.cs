using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Testing;

/// <summary>An <see cref="IDeployFiles"/> under test with the remote and local files it was given, and a way to read what ended up on disk and what was asked of the remote.</summary>
public abstract class DeployFilesRig : IDisposable
{
    /// <summary>Gets the port under test.</summary>
    public abstract IDeployFiles Files { get; }

    /// <summary>Gets the directory the files are laid out under.</summary>
    public abstract string DeployDir { get; }

    /// <summary>Gets the paths the remote was asked for, in order.</summary>
    public abstract IReadOnlyList<string> Requested { get; }

    /// <summary>Reads a laid-out file.</summary>
    /// <param name="path">The path under the deploy directory.</param>
    /// <returns>Its bytes, or null when it isn't there.</returns>
    public abstract byte[]? ReadLocal(string path);

    /// <inheritdoc />
    public virtual void Dispose() => GC.SuppressFinalize(this);
}
