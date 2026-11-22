using Aw.Kernel;
using Aw.Meta.Core.Delegations;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Core.Coordination;

/// <summary>Reads, adds and removes the delegations in the key-value store (DelegationBook's text under KvKeys.Delegations). A write is a compare and set, retried when another call wrote in between; lines it can't read are kept.</summary>
/// <param name="values">The key-value store.</param>
public sealed class DelegationLedger(IKeyValueStore values)
{
    private const int Tries = 5;

    /// <summary>Every recorded delegation, oldest first.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The delegations.</returns>
    public async ValueTask<Result<IReadOnlyList<Delegation>>> All(CancellationToken cancellationToken)
    {
        var stored = await values.Get(KvKeys.Delegations, cancellationToken).ConfigureAwait(false);
        return stored.IsError ? stored.Error : Results.Success(DelegationBook.Parse(stored.Value));
    }

    /// <summary>Records a delegation; an earlier one of the same issue is replaced.</summary>
    /// <param name="delegation">The delegation.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Success, or AW-META-012 when other writers kept winning the compare and set.</returns>
    public async ValueTask<Result<Unit>> Add(Delegation delegation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(delegation);
        var written = await Write(stored => DelegationBook.Add(stored, delegation), cancellationToken).ConfigureAwait(false);
        return written.IsError ? written.Error : Results.Ok();
    }

    /// <summary>Removes the delegation of one issue, owed or settled.</summary>
    /// <param name="issue">The issue number.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The removed delegation, null when the issue had none (nothing is written), or AW-META-012 when other writers kept winning.</returns>
    public async ValueTask<Result<Delegation?>> Remove(int issue, CancellationToken cancellationToken)
    {
        Delegation? removed = null;
        var written = await Write(
            stored =>
            {
                removed = DelegationBook.Find(stored, issue);
                return removed is null ? null : DelegationBook.Without(stored, issue);
            },
            cancellationToken).ConfigureAwait(false);
        return written.IsError ? written.Error : Results.Success<Delegation?>(removed);
    }

    // Reads the stored text, computes the new one (null: write nothing) and sets it if nobody wrote in between; retried on a lost race.
    private async ValueTask<Result<Unit>> Write(Func<string?, string?> change, CancellationToken cancellationToken)
    {
        for (var i = 0; i < Tries; i++)
        {
            var stored = await values.Get(KvKeys.Delegations, cancellationToken).ConfigureAwait(false);
            if (stored.IsError)
            {
                return stored.Error;
            }

            if (change(stored.Value) is not { } text)
            {
                return Results.Ok();
            }

            var written = await values.SetIf(KvKeys.Delegations, stored.Value, text, cancellationToken).ConfigureAwait(false);
            if (written.IsError || written.Value)
            {
                return written.IsError ? written.Error : Results.Ok();
            }
        }

        return MetaErrors.CommandFailed.With("the delegations changed under every write");
    }
}
