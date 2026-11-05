using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Infrastructure.Views;

/// <summary>The services a human paused (API P6 unit D), read from the 'services' view in views.db: the scheduler asks once per tick, so a pause survives a restart with the views.</summary>
/// <param name="reader">The views.db read side.</param>
/// <param name="status">The projector's status: before its first pass, while it rebuilds or is stalled the rows aren't trustworthy, so a read is refused (the scheduler keeps the pauses it last knew).</param>
internal sealed class ViewServicePauses(IViewReader reader, IProjectorStatus status) : IServicePauses
{
    public async ValueTask<Result<IReadOnlySet<ServiceName>>> Paused(CancellationToken cancellationToken)
    {
        if (status.Current is { Stalled: true } or { Rebuilding: true } or { Starting: true })
        {
            return status.Current.Stalled ? SindriErrors.ViewsStalled.With(status.Current.StalledPhrase())
                : SindriErrors.ViewsRebuilding.With(status.Current.Starting ? "the services view is still loading" : "the services view is being rebuilt");
        }

        var rows = await reader.List<ServiceRow>(ViewNames.Services, cancellationToken).ConfigureAwait(false);
        return rows.IsError ? rows.Error : Results.Success<IReadOnlySet<ServiceName>>(rows.Value.Where(static r => r.Paused).Select(static r => r.Name).ToHashSet());
    }
}
