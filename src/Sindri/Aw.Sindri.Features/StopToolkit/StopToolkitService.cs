using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Toolkits;

namespace Aw.Sindri.Features.StopToolkit;

/// <summary>Stops a toolkit (see <see cref="StopToolkitRequest"/>).</summary>
/// <param name="store">The toolkit store.</param>
/// <param name="runtime">Stops the process.</param>
internal sealed class StopToolkitService(IToolkitStore store, IToolkitRuntime runtime) : IService<StopToolkitRequest, StopToolkitResponse>
{
    public async ValueTask<Result<StopToolkitResponse>> Handle(StopToolkitRequest request, CancellationToken cancellationToken)
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

        var stopped = await runtime.Stop(toolkit.Name, cancellationToken).ConfigureAwait(false);
        if (stopped.IsError)
        {
            return stopped.Error;
        }

        var state = toolkit.SourceHash is not null && toolkit.Manifest is not null ? ToolkitState.Built : ToolkitState.Off;
        var saved = await store.Save(toolkit with { State = state }, cancellationToken).ConfigureAwait(false);
        if (saved.IsError)
        {
            return saved.Error;
        }

        return new StopToolkitResponse(toolkit.Name, state);
    }
}
