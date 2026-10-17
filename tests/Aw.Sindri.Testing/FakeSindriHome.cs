using Aw.Sindri.Core.Control;

namespace Aw.Sindri.Testing;

/// <summary>Sindri's state directory, fixed.</summary>
/// <param name="path">The directory path.</param>
public sealed class FakeSindriHome(string path) : ISindriHome
{
    /// <inheritdoc />
    public string Path => path;
}
