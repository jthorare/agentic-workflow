using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.LinearConnection;

/// <summary>Lists the teams a pasted read key can see. Owner only.</summary>
/// <param name="checks">Asks Linear.</param>
/// <param name="callers">The current caller.</param>
internal sealed class ListLinearTeamsService(ILinearKeyChecks checks, ICallerContextAccessor callers) : IService<ListLinearTeamsRequest, ListLinearTeamsResponse>
{
    public ValueTask<Result<ListLinearTeamsResponse>> Handle(ListLinearTeamsRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException($"{checks} {callers}");
}
