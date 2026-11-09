using Aw.Kernel;
using Aw.Sindri.Core.Resources;

namespace Aw.Sindri.Infrastructure.Resources;

/// <summary>The resources on disk (stub until the real adapter lands in the next commit).</summary>
/// <param name="root">The resources root, <c language="text">$AW_SINDRI_HOME/resources</c>.</param>
internal sealed class ResourceFiles(string root) : IResourceFiles
{
    internal string Root { get; } = root;

    public Result<IReadOnlyList<ResourceEntry>> List(ResourceOwner? owner) => throw new NotSupportedException();

    public Result<int> Purge(ResourceOwner owner) => throw new NotSupportedException();
}
