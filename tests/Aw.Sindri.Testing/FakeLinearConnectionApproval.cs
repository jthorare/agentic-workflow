using System.Diagnostics.CodeAnalysis;
using Aw.Kernel;
using Aw.Sindri.Features.LinearConnection;

namespace Aw.Sindri.Testing;

/// <summary>The in-memory <see cref="ILinearConnectionApproval"/>: settles to a fixed answer, or fails. Counts the settles.</summary>
[SuppressMessage("Design", "MA0182:Avoid unused internal types", Justification = "Internal because the port is; Aw.Sindri.Tests uses it through InternalsVisibleTo, which MA0182 can not see.")]
internal sealed class FakeLinearConnectionApproval : ILinearConnectionApproval
{
    private Result<bool> _answer = true;

    /// <summary>Gets how many times the approval was settled.</summary>
    public int Settles { get; private set; }

    /// <summary>Gets the change the last settle was asked about.</summary>
    public LinearConnectionChange? LastChange { get; private set; }

    /// <summary>Settles to <paramref name="approved"/>: false stands for other changes waiting.</summary>
    /// <param name="approved">Whether the profile is approved.</param>
    /// <returns>The same instance.</returns>
    public FakeLinearConnectionApproval Settling(bool approved)
    {
        _answer = approved;
        return this;
    }

    /// <summary>Fails the settle with <paramref name="error"/>.</summary>
    /// <param name="error">The error.</param>
    /// <returns>The same instance.</returns>
    public FakeLinearConnectionApproval Failing(Error error)
    {
        _answer = error;
        return this;
    }

    /// <inheritdoc/>
    public ValueTask<Result<bool>> Settle(LinearConnectionChange change, CancellationToken cancellationToken)
    {
        Settles++;
        LastChange = change;
        return ValueTask.FromResult(_answer);
    }
}
