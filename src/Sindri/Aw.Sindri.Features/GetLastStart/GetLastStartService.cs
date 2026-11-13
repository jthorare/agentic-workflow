using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Features.GetLastStart;

/// <summary>Answers the choice <see cref="IStartChoices"/> holds; a missing or unreadable file is an answer with both members null, never an error.</summary>
/// <param name="choices">The last-start file.</param>
internal sealed class GetLastStartService(IStartChoices choices) : IService<GetLastStartRequest, LastStartView>
{
    public async ValueTask<Result<LastStartView>> Handle(GetLastStartRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var last = await choices.Last(cancellationToken).ConfigureAwait(false);
        return new LastStartView(last.Repo, last.Agent);
    }
}
