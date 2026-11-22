using Aw.Kernel;
using Aw.Meta.Core.Delegations;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Core.Coordination;

/// <summary>Reads and adds the delegations in the key-value store (DelegationBook's text under KvKeys.Delegations). An add is a compare and set, retried when another call wrote in between.</summary>
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
        for (var i = 0; i < Tries; i++)
        {
            var stored = await values.Get(KvKeys.Delegations, cancellationToken).ConfigureAwait(false);
            if (stored.IsError)
            {
                return stored.Error;
            }

            var text = DelegationBook.Format(DelegationBook.With(DelegationBook.Parse(stored.Value), delegation));
            var written = await values.SetIf(KvKeys.Delegations, stored.Value, text, cancellationToken).ConfigureAwait(false);
            if (written.IsError || written.Value)
            {
                return written.IsError ? written.Error : Results.Ok();
            }
        }

        return MetaErrors.CommandFailed.With("the delegations changed under every write");
    }
}
