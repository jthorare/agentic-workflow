using Aw.Evidence.Core;
using Aw.Evidence.Core.Files;
using Aw.Evidence.Core.Ids;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Evidence.Features.DbRun;

internal sealed class DbRunValidator : IValidator<DbRunRequest>
{
    public Result<Unit> Validate(DbRunRequest value)
    {
        string?[] optional = [value.ConfigPath];
        if (!value.ScriptPath.StartsWith('/') || !value.RunDirectory.StartsWith('/') || !value.Repo.StartsWith('/') || optional.Any(static p => p is not null && !p.StartsWith('/')))
        {
            return EvidenceErrors.Invalid.With("script, run directory, repo and config paths must be absolute");
        }

        if (!RunId.TryFrom(RunPath.NameOf(value.RunDirectory), out _))
        {
            return EvidenceErrors.Invalid.With("the run directory needs a name (it is the run id)");
        }

        return value.LockTimeout < TimeSpan.Zero ? EvidenceErrors.Invalid.With("the lock timeout must not be negative") : Unit.Value;
    }
}
