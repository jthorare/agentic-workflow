using Aw.Evidence.Core;
using Aw.Evidence.Core.Files;
using Aw.Evidence.Core.Ids;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Evidence.Features.DesktopRun;

internal sealed class DesktopRunValidator : IValidator<DesktopRunRequest>
{
    public Result<Unit> Validate(DesktopRunRequest value)
    {
        if (!IsAbsolute(value.ManifestPath) || !IsAbsolute(value.RunDirectory) || !IsAbsolute(value.Repo) || (value.ConfigPath is not null && !IsAbsolute(value.ConfigPath)))
        {
            return EvidenceErrors.Invalid.With("manifest, run directory, repo and config paths must be absolute");
        }

        if (!RunId.TryFrom(RunPath.NameOf(value.RunDirectory), out _))
        {
            return EvidenceErrors.Invalid.With("the run directory needs a name (it is the run id)");
        }

        return value.LockTimeout < TimeSpan.Zero ? EvidenceErrors.Invalid.With("the lock timeout must not be negative") : Unit.Value;
    }

    private static bool IsAbsolute(string path) => path.StartsWith('/');
}
