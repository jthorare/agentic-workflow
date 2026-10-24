using Aw.Evidence.Core;
using Aw.Evidence.Core.Design;
using Aw.Evidence.Core.Files;
using Aw.Evidence.Core.Guard;
using Aw.Evidence.Core.Ids;
using Aw.Evidence.Core.Locks;
using Aw.Evidence.Core.Runs;
using Aw.Evidence.Core.Scripts;
using Aw.Evidence.Core.Settings;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Evidence.Features.Parity;

/// <summary>
/// Runs design parity: reads and checks the manifest, vets the host and the frame steps (read-only on every host
/// unless writes are allowed), reads sign-in credentials from the environment, then holds the QA-stack and heavy-job
/// locks for the browser run. parity.json is deleted first, so a failed run leaves none behind. A run failure's
/// message never carries a credential.
/// </summary>
internal sealed class ParityService(ParityFilesAccess files, IMachineLocks locks, ISecretsSource secrets, ParityRun run, IEvidenceSettings settings)
    : IService<ParityRequest, ParityResponse>
{
    public async ValueTask<Result<ParityResponse>> Handle(ParityRequest request, CancellationToken cancellationToken)
    {
        var manifest = await files.ReadManifestAsync(request.ManifestPath, cancellationToken).ConfigureAwait(false);
        var host = manifest.Bind(_ => HostGuard.Resolve(request.Host, request.AllowPreviewHost, settings.PreviewHostPattern));
        var vetted = host.Bind(_ => StepGuard.ForParity(StepsOf(manifest.Value), request.AllowWrites));
        var credentials = vetted.Bind(_ => ReadCredentials(manifest.Value));
        if (credentials.IsError)
        {
            return credentials.Error;
        }

        var lease = await StackLocks.AcquireAsync(locks, request.LockTimeout, cancellationToken).ConfigureAwait(false);
        if (lease.IsError)
        {
            return lease.Error;
        }

        await using (lease.Value.ConfigureAwait(false))
        {
            var context = new ParityContext(manifest.Value, DirectoryOf(request.ManifestPath), host.Value, request.RunDirectory, credentials.Value.Credentials);
            return await RunAsync(request, context, cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask<Result<ParityResponse>> RunAsync(ParityRequest request, ParityContext context, CancellationToken cancellationToken)
    {
        var prepared = await files.PrepareAsync(request.RunDirectory, cancellationToken).ConfigureAwait(false);
        if (prepared.IsError)
        {
            return prepared.Error;
        }

        var frames = await run.RunAsync(context, cancellationToken).ConfigureAwait(false);
        if (frames.IsError)
        {
            var message = context.Credentials?.Redact(frames.Error.Message) ?? frames.Error.Message;
            return EvidenceErrors.RunFailed.With(message);
        }

        var summary = new ParitySummary(
            ParitySummary.SchemaName, RunId.From(RunPath.NameOf(request.RunDirectory)), context.Host.Origin,
            ProvenanceRules.For(context.Host.Kind, request.DbProvenance), request.AppBuild, context.Manifest.DesignNode,
            context.Manifest.Viewport, frames.Value, request.At);
        var written = await files.WriteSummaryAsync(request.RunDirectory, summary, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<string> failures = [.. summary.FailedBoxChecks().Select(static x => $"{x.Frame} {x.Check.Target}: {x.Check.Failure}")];
        return written.Map(_ => new ParityResponse(summary, failures));
    }

    private Result<SignIn> ReadCredentials(DesignManifest manifest) =>
        manifest.Login is { } login
            ? Credentials.Read(secrets, login.EmailEnv, login.PasswordEnv).Map(static c => new SignIn(c))
            : new SignIn(Credentials: null);

    // The route is opened first, so it is vetted as the goto it is, ahead of every frame's steps.
    private static List<ScriptStep> StepsOf(DesignManifest manifest) =>
        [new ScriptStep(StepAction.Goto, manifest.Route, new UrlPath(manifest.Route)), .. manifest.Frames.SelectMany(static f => f.Steps)];

    private static string DirectoryOf(string path) => path[..Math.Max(1, path.LastIndexOf('/'))];

    private sealed record SignIn(Credentials? Credentials);
}
