using Aw.Kernel;
using Aw.Kernel.Data;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Infrastructure.Ledger;

/// <summary>
/// <see cref="ICommandOrigins"/> over the ledger (engine spec rev 8 §5.4; engine P4 unit (d)): the causing event, found by
/// its envelope id, names its instance in its payload's 'instance' field, as every 'workflow.*' event the interpreter
/// mirrors does. A read only: it never migrates or creates the file. The payload read is the design; there is no 'workflow_commands' map.
/// </summary>
/// <param name="database">The ledger file.</param>
internal sealed class LedgerCommandOrigins(LedgerDatabase database) : ICommandOrigins
{
    public async ValueTask<Result<InstanceAddress?>> CauserOf(LedgerEvent reply, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reply);
        var schema = await database.Schema(cancellationToken).ConfigureAwait(false);
        if (schema.IsError || schema.Value.Applied == 0 || reply.Envelope.CausationId is not { } command)
        {
            return schema.IsError ? schema.Error : Results.Success<InstanceAddress?>(value: null);
        }

        var read = await database.Sessions.BeginRead(cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return LedgerDatabase.Translate(read.Error);
        }

        await using (read.Value.ConfigureAwait(false))
        {
            var named = await SqliteQueries.List(read.Value, new SqlStatement(LedgerSql.SelectInstanceOfEvent, c => c.Bind("$id", command)),
                TextMapper.Instance, cancellationToken).ConfigureAwait(false);
            if (named.IsError)
            {
                return LedgerDatabase.Translate(named.Error);
            }

            return named.Value is [{ } name] && InstanceAddress.TryFrom(name, out var instance) ? instance : Results.Success<InstanceAddress?>(value: null);
        }
    }
}
