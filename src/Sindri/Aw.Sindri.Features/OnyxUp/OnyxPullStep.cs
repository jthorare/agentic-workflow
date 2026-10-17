using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Features.OnyxUp;

// Step 4: pull the images by their pinned digests, then check each active image holds the pinned digest locally.
internal sealed class OnyxPullStep(IContainerRuntime runtime, IOnyxWorkspace workspace, IComposeServices services, IOnyxProgress progress) : IOnyxUpStep
{
    public string Name => OnyxSteps.Pull;

    public string Stamp(OnyxUpContext context, string? upstream) => OnyxSteps.Stamp(OnyxUpPlan.Upstream(Name, upstream));

    public ValueTask<Result<OnyxStepOutcome>> Execute(OnyxUpContext context, string stamp, string? recorded, CancellationToken cancellationToken)
    {
        if (string.Equals(recorded, stamp, StringComparison.Ordinal))
        {
            return ValueTask.FromResult<Result<OnyxStepOutcome>>(OnyxStepOutcome.Skipped);
        }

        progress.Line($"{Name}: running");
        return ValueTask.FromResult(Pull(context, cancellationToken).Map(static _ => OnyxStepOutcome.Ran));
    }

    private Result<Unit> Pull(OnyxUpContext context, CancellationToken cancellationToken)
    {
        var pulled = runtime.Pull(context.Project, _ => progress.Tick(), cancellationToken);
        if (pulled.IsError)
        {
            return pulled.Error;
        }

        var (compose, env) = (workspace.Read(context.Paths.Compose), workspace.Read(context.Paths.Env));
        if (compose.IsError || env.IsError)
        {
            return compose.IsError ? compose.Error : env.Error;
        }

        var images = services.Read(compose.Value ?? "")
            .Bind(list => OnyxConfig.ActiveImages(list, context.Settings(""), context.Pins, context.Platform, OnyxConfig.EnabledProfiles(env.Value)));
        var digests = images.Bind(list => runtime.RepoDigests(list, cancellationToken));
        if (digests.IsError)
        {
            return digests.Error;
        }

        var wrong = images.Value.FirstOrDefault(i => !digests.Value[i].Contains(ImageNames.WithoutDefaults(i), StringComparer.Ordinal));
        if (wrong is null)
        {
            return Results.Ok();
        }

        var at = wrong.IndexOf('@', StringComparison.Ordinal);
        return SindriErrors.OnyxPinsMalformed.With($"the pulled image {wrong[..at]} doesn't hold the pinned digest {wrong[(at + 1)..]}; remove it (`docker image rm {wrong[..at]}`) and run `sindri onyx up` again, and if it still differs, the registry no longer serves the pinned build");
    }
}
