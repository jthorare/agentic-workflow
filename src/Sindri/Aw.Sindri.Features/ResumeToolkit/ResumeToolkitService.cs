using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Toolkits;

namespace Aw.Sindri.Features.ResumeToolkit;

/// <summary>Resumes a toolkit (see <see cref="ResumeToolkitRequest"/>): the runtime flips the process's pause, then the row follows it.</summary>
/// <param name="store">The toolkit store.</param>
/// <param name="runtime">Owns the process.</param>
internal sealed class ResumeToolkitService(IToolkitStore store, IToolkitRuntime runtime) : IService<ResumeToolkitRequest, ResumeToolkitResponse>
{
    public async ValueTask<Result<ResumeToolkitResponse>> Handle(ResumeToolkitRequest request, CancellationToken cancellationToken)
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

        var done = await runtime.Resume(toolkit.Name, cancellationToken).ConfigureAwait(false);
        if (done.IsError)
        {
            return done.Error;
        }

        var saved = await store.Save(toolkit with { State = ToolkitState.Running }, cancellationToken).ConfigureAwait(false);
        return saved.IsError ? saved.Error : new ResumeToolkitResponse(toolkit.Name, ToolkitState.Running);
    }
}
