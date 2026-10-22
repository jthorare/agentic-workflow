using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Mcp;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Features.RegisterMcp;

/// <summary>Asks the registration slice what it would change, on every provider, without writing. A tracked project file counts as a change: the job runs so its needs-you row can name the file.</summary>
/// <param name="registration">The slice, with the trust row off.</param>
internal sealed class McpNeed(AutomaticMcpRegistration registration) : IMcpNeed
{
    private static readonly ProviderFile[] _providers = [ProviderFile.Claude, ProviderFile.Cursor, ProviderFile.Codex];

    public async ValueTask<bool> Needed(RepoName repo, CancellationToken cancellationToken)
    {
        var would = await registration.Service.Handle(new RegisterMcpRequest(Directory: string.Empty, repo.Value, Apply: false, _providers), cancellationToken).ConfigureAwait(false);
        return would.IsSuccess && would.Value.Providers.Any(static p => p.Outcome is not (ProviderOutcome.Unchanged or ProviderOutcome.Skipped));
    }
}
