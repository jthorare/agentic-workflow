namespace Aw.Context.Core.Library;

/// <summary>Where the library feed leaves what its last pass over each root found, for <see cref="Facade.IContextLibrary.Roots"/> and the doctor row.</summary>
public interface ILibraryStatus
{
    /// <summary>The last report of each root; none before a first pass. An unreadable store reads as none.</summary>
    /// <returns>The reports.</returns>
    public IReadOnlyList<LibraryRootReport> Read();

    /// <summary>Replaces the reports with these (a root that is no longer configured drops out).</summary>
    /// <param name="reports">The reports.</param>
    public void Write(IReadOnlyList<LibraryRootReport> reports);
}
