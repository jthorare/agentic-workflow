using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Locks;
using Aw.Sindri.Core.Toolkits;

namespace Aw.Sindri.Features.BuildToolkit;

/// <summary>Builds and tests a toolkit (see <see cref="BuildToolkitRequest"/>).</summary>
/// <param name="store">The toolkit store.</param>
/// <param name="workspace">The toolkit folders.</param>
/// <param name="commands">Runs the build and the tests.</param>
/// <param name="slots">The heavy-job slots.</param>
/// <param name="jobs">The running job.</param>
/// <param name="clock">The clock.</param>
internal sealed class BuildToolkitService(IToolkitStore store, IToolkitWorkspace workspace, IToolkitCommands commands, IHeavySlots slots, IJobContextAccessor jobs, TimeProvider clock)
    : IService<BuildToolkitRequest, BuildToolkitResult>
{
    private const int OutputCap = 600;

    /// <summary>How long a build queues for a heavy-job slot: hours, like the map build, so a busy box waits with progress instead of failing.</summary>
    private static readonly TimeSpan _slotWait = TimeSpan.FromHours(12);

    private static readonly string[] _build = ["dotnet", "build", "-warnaserror"];

    private static readonly string[] _test = ["dotnet", "test"];

    public async ValueTask<Result<BuildToolkitResult>> Handle(BuildToolkitRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var found = await store.Find(request.Name, cancellationToken).ConfigureAwait(false);
        if (found.IsError)
        {
            return found.Error;
        }

        if (found.Value is not { } toolkit)
        {
            return SindriErrors.ToolkitNotFound.With($"'{request.Name}' (fix: sindri toolkit list)");
        }

        var progress = jobs.Current?.Progress;
        progress?.Report("waiting for a heavy-job slot");
        var taken = await slots.Take("sindri toolkit-build", _slotWait, progress, cancellationToken).ConfigureAwait(false);
        if (taken.IsError)
        {
            return taken.Error;
        }

        if (taken.Value.Lease is not { } lease)
        {
            return SindriErrors.ToolkitTestsFailed.With($"{toolkit.Name}: no heavy-job slot came free in {_slotWait.TotalHours.ToString(System.Globalization.CultureInfo.InvariantCulture)} h: {taken.Value.Busy} (fix: run the build again later)");
        }

        await using (lease.ConfigureAwait(false))
        {
            return await Run(toolkit, progress, cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask<Result<BuildToolkitResult>> Run(Toolkit toolkit, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        var hash = await workspace.SourceHash(toolkit.Dir, cancellationToken).ConfigureAwait(false);
        if (hash.IsError)
        {
            return await Failed(toolkit, hash.Error.Message, cancellationToken).ConfigureAwait(false);
        }

        progress?.Report("building (warnings are errors)");
        var built = await commands.Run(toolkit.Dir, _build, cancellationToken).ConfigureAwait(false);
        if (built.ExitCode != 0)
        {
            return await Failed(toolkit, "the build failed: " + Tail(built.Output), cancellationToken).ConfigureAwait(false);
        }

        progress?.Report("running the toolkit's tests");
        var tested = await commands.Run(toolkit.Dir, _test, cancellationToken).ConfigureAwait(false);
        if (tested.ExitCode != 0)
        {
            return await Failed(toolkit, "the tests failed: " + Tail(tested.Output), cancellationToken).ConfigureAwait(false);
        }

        var text = await workspace.ReadManifest(toolkit.Dir, cancellationToken).ConfigureAwait(false);
        var manifest = text.IsError ? text.Error : ToolkitManifest.Parse(text.Value);
        if (manifest.IsError)
        {
            _ = await Failed(toolkit, manifest.Error.Message, cancellationToken).ConfigureAwait(false);
            return manifest.Error;
        }

        var saved = await store.Save(toolkit with { State = ToolkitState.Built, SourceHash = hash.Value, BuiltAt = clock.GetUtcNow(), Manifest = manifest.Value }, cancellationToken).ConfigureAwait(false);
        if (saved.IsError)
        {
            return saved.Error;
        }

        progress?.Report("built");
        return new BuildToolkitResult(toolkit.Name, hash.Value);
    }

    private async ValueTask<Result<BuildToolkitResult>> Failed(Toolkit toolkit, string why, CancellationToken cancellationToken)
    {
        var saved = await store.Save(toolkit with { State = ToolkitState.Failed, SourceHash = null, BuiltAt = null, Manifest = null }, cancellationToken).ConfigureAwait(false);
        if (saved.IsError)
        {
            return saved.Error;
        }

        return SindriErrors.ToolkitTestsFailed.With($"{toolkit.Name}: {why}");
    }

    private static string Tail(string output) => output.Length <= OutputCap ? output : "..." + output[^OutputCap..];
}
