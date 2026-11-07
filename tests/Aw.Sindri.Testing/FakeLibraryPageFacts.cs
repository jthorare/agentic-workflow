using Aw.Kernel;
using Aw.Sindri.Core.Reviews;

namespace Aw.Sindri.Testing;

/// <summary>The library page's facts, set by the test: what Context's stored docs and root states would give.</summary>
public sealed class FakeLibraryPageFacts : ILibraryPageFacts
{
    private Result<LibraryPageFacts> _facts = new LibraryPageFacts(Docs: 0, Broken: [], NearMisses: [], UnreadableRoots: [], Roots: []);

    /// <summary>Gets how many times the facts were read.</summary>
    public int Reads { get; private set; }

    /// <summary>Sets what the next reads answer.</summary>
    /// <param name="facts">The facts.</param>
    /// <returns>This fake.</returns>
    public FakeLibraryPageFacts With(LibraryPageFacts facts)
    {
        _facts = facts;
        return this;
    }

    /// <summary>Makes every read fail.</summary>
    /// <param name="error">The error.</param>
    /// <returns>This fake.</returns>
    public FakeLibraryPageFacts Failing(Error error)
    {
        _facts = error;
        return this;
    }

    public ValueTask<Result<LibraryPageFacts>> Read(CancellationToken cancellationToken)
    {
        Reads++;
        return ValueTask.FromResult(_facts);
    }
}
