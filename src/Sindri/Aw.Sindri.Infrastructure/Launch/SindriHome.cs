using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary><see cref="ISindriHome"/> over the settings' home.</summary>
/// <param name="path">The home.</param>
internal sealed class SindriHome(string path) : ISindriHome
{
    public string Path { get; } = path;
}
