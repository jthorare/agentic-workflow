using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Toolkits;

namespace Aw.Sindri.Features.PauseToolkit;

/// <summary>Pauses a toolkit (see <see cref="PauseToolkitRequest"/>): the runtime flips the process's pause, then the row follows it.</summary>
/// <param name="store">The toolkit store.</param>
/// <param name="runtime">Owns the process.</param>
internal sealed class PauseToolkitService(IToolkitStore store, IToolkitRuntime runtime) : IService<PauseToolkitRequest, PauseToolkitResponse>
{
    public async ValueTask<Result<PauseToolkitResponse>> Handle(PauseToolkitRequest request, CancellationToken cancellationToken)
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

        var done = await runtime.Pause(toolkit.Name, cancellationToken).ConfigureAwait(false);
        if (done.IsError)
        {
            return done.Error;
        }

        var saved = await store.Save(toolkit with { State = ToolkitState.Paused }, cancellationToken).ConfigureAwait(false);
        return saved.IsError ? saved.Error : new PauseToolkitResponse(toolkit.Name, ToolkitState.Paused);
    }
}
