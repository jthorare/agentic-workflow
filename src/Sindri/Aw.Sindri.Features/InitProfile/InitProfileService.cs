using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Features.InitProfile;

/// <summary>Writes the default profile (the schema version, the user and the effects the shipped recipes declare) when none exists. The profile then waits for a repo and for the owner's approval; this approves nothing.</summary>
/// <param name="initializer">Writes the file.</param>
/// <param name="shipped">The effect kinds a new profile allows.</param>
internal sealed class InitProfileService(IProfileInitializer initializer, IShippedEffects shipped) : IService<InitProfileRequest, InitProfileResponse>
{
    public async ValueTask<Result<InitProfileResponse>> Handle(InitProfileRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var created = await initializer.Create(shipped.Kinds(), cancellationToken).ConfigureAwait(false);
        return created.IsError ? created.Error : new InitProfileResponse(created.Value);
    }
}
