using Aw.Context.Core.Concepts;
using Aw.Context.Core.Map;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.GetPinMoves;

/// <summary>What each pin gained and lost between two map versions, each read the way the concepts read does.</summary>
internal sealed class GetPinMovesService(ConceptReader reader) : IService<GetPinMovesRequest, IReadOnlyList<PinMove>>
{
    public async ValueTask<Result<IReadOnlyList<PinMove>>> Handle(GetPinMovesRequest request, CancellationToken cancellationToken)
    {
        var before = await reader.Read(request.Scope, At(request.From), repo: null, cancellationToken).ConfigureAwait(false);
        if (before.IsError)
        {
            return before.Error;
        }

        var after = await reader.Read(request.Scope, At(request.To), repo: null, cancellationToken).ConfigureAwait(false);
        if (after.IsError)
        {
            return after.Error;
        }

        return Results.Success(PinMembership.Moves(request.From.Number, request.To.Number, before.Value, after.Value));
    }

    private static MapRead At(MapVersion version) => new(Draft: false, new MapVersionRef(version.Number, version.Commit));
}
