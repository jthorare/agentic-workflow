using Aw.Context.Core.Facade;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Mcp;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// Doctor's context checks. context:sources asks the engine in-process with the approved profile from the same home
/// serve reads (so it works with the process stopped); context:mcp-scope reads the three providers' user-wide MCP files.
/// </summary>
internal sealed class ContextDoctorChecks(ContextScopes scopes, IContextEngine engine, IProviderFiles files) : IDoctorChecks
{
    public async ValueTask<IReadOnlyList<DoctorCheck>> Run(CancellationToken cancellationToken)
    {
        var scope = await scopes.Current(cancellationToken).ConfigureAwait(false);
        var view = scope.IsError || scope.Value.Settings is null ? null : await engine.Sources(scope.Value, cancellationToken).ConfigureAwait(false);
        var sources = scope.IsError ? ContextChecks.NoProfile(scope.Error) : view is null ? ContextChecks.NoSection() : ContextChecks.Sources(view);
        var sync = view is null ? new DoctorCheck(ContextChecks.SyncName, CheckState.Ok, "no context sources, so nothing syncs", Fix: null) : ContextChecks.Sync(view, scope.Value.SyncConfigured);
        var findings = new List<McpScopeFinding>();
        foreach (var file in Enum.GetValues<ProviderFile>())
        {
            if (McpScope.Judge(await files.Read(file, cancellationToken).ConfigureAwait(false)) is { } finding)
            {
                findings.Add(finding);
            }
        }

        return [sources, sync, ContextChecks.McpScope(findings)];
    }
}
