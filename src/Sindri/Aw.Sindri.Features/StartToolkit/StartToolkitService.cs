using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Toolkits;

namespace Aw.Sindri.Features.StartToolkit;

/// <summary>Starts a toolkit that is built for its current source (see <see cref="StartToolkitRequest"/>).</summary>
/// <param name="store">The toolkit store.</param>
/// <param name="workspace">The toolkit folders.</param>
/// <param name="runtime">Starts the process.</param>
internal sealed class StartToolkitService(IToolkitStore store, IToolkitWorkspace workspace, IToolkitRuntime runtime) : IService<StartToolkitRequest, StartToolkitResponse>
{
    public async ValueTask<Result<StartToolkitResponse>> Handle(StartToolkitRequest request, CancellationToken cancellationToken)
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

        var hash = await workspace.SourceHash(toolkit.Dir, cancellationToken).ConfigureAwait(false);
        if (hash.IsError)
        {
            return hash.Error;
        }

        if (!toolkit.IsBuiltFor(hash.Value))
        {
            return SindriErrors.ToolkitNotBuilt.With(request.Name);
        }

        var started = await runtime.Start(toolkit, cancellationToken).ConfigureAwait(false);
        if (started.IsError)
        {
            return started.Error;
        }

        var saved = await store.Save(toolkit with { State = ToolkitState.Running }, cancellationToken).ConfigureAwait(false);
        if (saved.IsError)
        {
            return saved.Error;
        }

        return new StartToolkitResponse(toolkit.Name, ToolkitState.Running);
    }
}
