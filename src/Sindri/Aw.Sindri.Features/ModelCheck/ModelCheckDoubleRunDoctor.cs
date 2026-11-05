using Aw.Sindri.Core.Checks;
using Aw.Sindri.Core.Doctor;

namespace Aw.Sindri.Features.ModelCheck;

/// <summary>
/// Doctor's 'model:double-run' line (model-check design §4.1): since workflow.build v7 the build runs the model check itself, on the specs a change touches, so a profile that also lists
/// scripts/tlc-check.sh as a build command runs every spec twice, on every change. A warning, never a failure; no row when the profile is not approved or lists no such command.
/// </summary>
/// <param name="commands">The approved profile's build commands.</param>
internal sealed class ModelCheckDoubleRunDoctor(IBuildCommandSource commands) : IDoctorChecks
{
    internal const string Name = "model:double-run";

    private const string Runner = "scripts/tlc-check.sh";

    public async ValueTask<IReadOnlyList<DoctorCheck>> Run(CancellationToken cancellationToken)
    {
        var read = await commands.Read(cancellationToken).ConfigureAwait(false);
        return !read.IsError && read.Value.Commands.Any(static c => c.Run.Any(static part => part.Contains(Runner, StringComparison.Ordinal)))
            ? [new DoctorCheck(Name, CheckState.Warn, $"workflow.build.commands lists {Runner}, and the build already runs the model check, so every spec runs twice", $"remove {Runner} from workflow.build.commands")]
            : [];
    }
}
