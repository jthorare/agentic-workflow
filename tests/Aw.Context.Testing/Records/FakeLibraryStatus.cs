using Aw.Context.Core.Library;

namespace Aw.Context.Testing.Records;

/// <summary>An in-memory <see cref="ILibraryStatus"/>.</summary>
public sealed class FakeLibraryStatus : ILibraryStatus
{
    private IReadOnlyList<LibraryRootReport> _reports = [];

    /// <inheritdoc />
    public IReadOnlyList<LibraryRootReport> Read() => _reports;

    /// <inheritdoc />
    public void Write(IReadOnlyList<LibraryRootReport> reports) => _reports = [.. reports];
}
