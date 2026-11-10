using Aw.Kernel;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Toolkits;

namespace Aw.Sindri.Infrastructure.Toolkits;

/// <summary>The toolkits root on disk, and the generator run that fills it.</summary>
/// <param name="home">The Sindri home the root lives under.</param>
internal sealed class FileToolkitFolders(ISindriHome home) : IToolkitFolders
{
    public IReadOnlyList<ToolkitFolder> Folders() => throw new NotSupportedException(home.Path);

    public ValueTask<Result<ToolkitFolder>> Generate(string name, string tool, string? needs, CancellationToken cancellationToken) => throw new NotSupportedException();
}
