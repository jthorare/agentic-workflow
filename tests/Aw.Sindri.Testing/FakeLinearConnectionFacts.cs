using System.Diagnostics.CodeAnalysis;
using Aw.Kernel;
using Aw.Sindri.Features.LinearConnection;

namespace Aw.Sindri.Testing;

/// <summary>The in-memory <see cref="ILinearConnectionFacts"/>: a fixed view, or a failure. Counts the reads.</summary>
[SuppressMessage("Design", "MA0182:Avoid unused internal types", Justification = "Internal because the port is; Aw.Sindri.Tests uses it through InternalsVisibleTo, which MA0182 can not see.")]
internal sealed class FakeLinearConnectionFacts : ILinearConnectionFacts
{
    private Result<LinearConnectionView> _answer = new LinearConnectionView(new LinearKeyStateView("notSet", CheckedAt: null), new LinearKeyStateView("notSet", CheckedAt: null), [], Pending: false);

    /// <summary>Gets how many times the connection was read.</summary>
    public int Reads { get; private set; }

    /// <summary>Answers every read with <paramref name="view"/>.</summary>
    /// <param name="view">The view.</param>
    /// <returns>The same instance.</returns>
    public FakeLinearConnectionFacts Answering(LinearConnectionView view)
    {
        _answer = view;
        return this;
    }

    /// <summary>Fails every read with <paramref name="error"/>.</summary>
    /// <param name="error">The error.</param>
    /// <returns>The same instance.</returns>
    public FakeLinearConnectionFacts Failing(Error error)
    {
        _answer = error;
        return this;
    }

    /// <inheritdoc/>
    public ValueTask<Result<LinearConnectionView>> Read(CancellationToken cancellationToken)
    {
        Reads++;
        return ValueTask.FromResult(_answer);
    }
}
