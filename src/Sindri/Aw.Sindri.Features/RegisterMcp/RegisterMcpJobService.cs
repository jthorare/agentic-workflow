using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Mcp;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Features.RegisterMcp;

/// <summary>
/// The work of a RegisterMcp job (setup-once spec §5.4): the registration slice for the repo the admission resolved, on every provider (the ones not installed are skipped), written for real, and without Codex's
/// project-trust row (Codex asks the owner once). It names the repo, so it never reads the current directory. It records 'mcp.registered' with each provider's outcome; a provider that failed fails the job, with
/// that provider's sentence and fix, which the 'setup:mcp' needs-you row shows.
/// </summary>
internal sealed class RegisterMcpJobService(ResolvedMcpRepos resolved, AutomaticMcpRegistration registration, IApprovedProfile approvals, IJobContextAccessor jobs, McpEvents recorder)
    : IService<RegisterMcpJobRequest, RegisterMcpResponse>
{
    public async ValueTask<Result<RegisterMcpResponse>> Handle(RegisterMcpJobRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (resolved.Take(request) is not { } repo)
        {
            return SindriErrors.ApiInternal.With("the RegisterMcp job has no resolved repo");
        }

        if (await approvals.Latest(cancellationToken).ConfigureAwait(false) is { IsSuccess: true, Value: { } approved })
        {
            _ = await recorder.ConsentOnHumanPress(approved.Hash, cancellationToken).ConfigureAwait(false);   // pressing "Turn on for your repos" is the consent
        }

        jobs.Current?.Progress.Report("registering");
        var done = await registration.Service.Handle(new RegisterMcpRequest(Directory: string.Empty, repo.Value, Apply: true, [ProviderFile.Claude, ProviderFile.Cursor, ProviderFile.Codex]), cancellationToken).ConfigureAwait(false);
        if (done.IsError)
        {
            return done.Error;
        }

        var outcomes = done.Value.Providers.Select(static p => new McpProviderOutcome(Name(p.Provider.ToString()), Name(p.Outcome.ToString()))).ToList();
        var recorded = await recorder.Registered(repo, PortOf(done.Value.Arguments), outcomes, cancellationToken).ConfigureAwait(false);
        if (recorded.IsError)
        {
            return recorded.Error;
        }

        return done.Value.Providers.FirstOrDefault(static p => p.Outcome == ProviderOutcome.Failed) is { } failed
            ? SindriErrors.ServiceFailed.With($"Couldn't register with {failed.Provider}: {failed.Detail}{(failed.Fix is { } fix ? " (fix: " + fix + ")" : string.Empty)}")
            : done;
    }

    private static string Name(string value) => JsonNamingPolicy.CamelCase.ConvertName(value);

    /// <summary>The port of the registered address, which is the one http argument of the claude command line.</summary>
    private static int PortOf(IReadOnlyList<string> arguments)
    {
        foreach (var argument in arguments)
        {
            if (Uri.TryCreate(argument, UriKind.Absolute, out var uri) && string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal))
            {
                return uri.Port;
            }
        }

        return 0;
    }
}
