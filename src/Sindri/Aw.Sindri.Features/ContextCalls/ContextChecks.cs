using Aw.Context.Core;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Aw.Kernel;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Mcp;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>The doctor rows for the context engine (context spec §3, §8.6). Details name codes, sources and files, never record or file content.</summary>
internal static class ContextChecks
{
    internal const string SourcesName = "context:sources";
    internal const string SyncName = "context:sync";
    internal const string McpScopeName = "context:mcp-scope";

    /// <summary>No approved profile: nothing to check yet.</summary>
    internal static DoctorCheck NoProfile(Error error) =>
        new(SourcesName, CheckState.Warn, $"{error.Code} {error.Message}", Fix: null);   // every error ContextScopes returns names its fix

    /// <summary>The profile has no context: section.</summary>
    internal static DoctorCheck NoSection() => new(SourcesName, CheckState.Ok, "no context: section in the profile, so no context sources", Fix: null);

    /// <summary>The sources' state: fail when the store can't open or every source is down, warn when one is degraded.</summary>
    internal static DoctorCheck Sources(Result<SourcesView> view)
    {
        if (view.IsError)
        {
            return new DoctorCheck(SourcesName, CheckState.Fail, $"{view.Error.Code} {view.Error.Message}", "sindri context sources status");
        }

        var rows = view.Value.Sources;
        var down = rows.Where(r => r.State != SourceState.Ok).Select(r => $"{r.Source.Value} {r.Repo?.Value ?? "-"} {r.Code ?? r.State.ToString().ToLowerInvariant()}").ToList();
        var counts = $"{rows.Count(r => r.State == SourceState.Ok)} of {rows.Count} sources ok";
        if (rows.Count > 0 && rows.All(r => r.State == SourceState.Unavailable))   // 011 is every source unavailable; degraded ones (e.g. never refreshed) only warn
        {
            return new DoctorCheck(SourcesName, CheckState.Fail, $"{ContextErrors.AllSourcesUnavailable.Code} every source is unavailable: {string.Join("; ", down)}", "sindri context sources status");
        }

        if (down.Count > 0)
        {
            return new DoctorCheck(SourcesName, CheckState.Warn, $"{ContextErrors.SourceUnavailable.Code} {counts}; degraded: {string.Join("; ", down)}", "sindri context sources status");
        }

        // Session reads off is the owner's choice (they are on by default since q93), not a fault: ok, saying what it means for MCP.
        return new DoctorCheck(SourcesName, CheckState.Ok, view.Value.SessionsEnabled
            ? $"{counts}; session reads on"
            : $"{counts}; session reads off (context.sessions.enabled), so MCP calls answer {ContextErrors.SessionsDisabled.Code}", Fix: null);
    }

    /// <summary>The intent sources' sync (S-5B): warn when one was never synced or its last run failed; nothing to check without an intent source or a profile. The fix fits the state and names only what runs today.</summary>
    internal static DoctorCheck Sync(Result<SourcesView> view, bool serviceNamed)
    {
        if (view.IsError)
        {
            return new DoctorCheck(SyncName, CheckState.Ok, "no sources to check yet", Fix: null);   // context:sources already reports why
        }

        var intent = view.Value.Sources.Where(r => r.Kind == SourceKind.Intent).ToList();
        if (intent.Count == 0)
        {
            return new DoctorCheck(SyncName, CheckState.Ok, "no intent source in the profile, so nothing syncs", Fix: null);
        }

        var problems = intent.Select(r => (r.Source.Value, r.Sync)).Where(p => p.Sync is null or { LastSync: null } or { State: SourceState.Unavailable })
            .Select(p => p.Sync?.Code is { } code ? $"{p.Value} last sync failed ({code})" : $"{p.Value} never synced").ToList();
        return problems.Count > 0
            ? new DoctorCheck(SyncName, CheckState.Warn, string.Join("; ", problems), SyncFix(serviceNamed, failed: problems.Exists(p => p.Contains("last sync failed", StringComparison.Ordinal))))
            : new DoctorCheck(SyncName, CheckState.Ok, $"{intent.Count} intent source(s) synced", Fix: null);
    }

    private static string SyncFix(bool serviceNamed, bool failed) => !serviceNamed
        ? "run sindri context sources sync (or name the context-sync service under services: in the profile to sync on a schedule), then sindri context sources status"
        : failed
            ? "sindri context sources status shows the code; run sindri context sources sync to retry now, or the next context-sync slot retries"
            : "run sindri context sources sync, or wait for the next context-sync slot, then sindri context sources status";

    /// <summary>Warn on any user-wide sindri entry (026) or a provider file that couldn't be checked.</summary>
    internal static DoctorCheck McpScope(IReadOnlyList<McpScopeFinding> findings)
    {
        var userWide = findings.Where(f => f.State == McpScopeState.UserWide).ToList();
        if (userWide.Count > 0)
        {
            return new DoctorCheck(McpScopeName, CheckState.Warn,
                $"{ContextErrors.McpUserWide.Code} a user-wide '{Core.Mcp.McpScope.ServerName}' MCP entry in {string.Join(", ", userWide.Select(f => $"{f.Path} ({Provider(f.File)})"))}: every session on this machine, personal repos included, would reach work context",
                $"remove it ({string.Join("; ", userWide.Select(f => Removal(f.File)))}), then run 'sindri mcp register' in the work repo");
        }

        var unreadable = findings.Where(f => f.State == McpScopeState.Unreadable).ToList();
        return unreadable.Count > 0
            ? new DoctorCheck(McpScopeName, CheckState.Warn, $"couldn't check {string.Join(", ", unreadable.Select(f => $"{f.Path} ({f.Problem})"))}", "fix the file, or check it by hand for a 'sindri' MCP entry")
            : new DoctorCheck(McpScopeName, CheckState.Ok, "no user-wide 'sindri' MCP entry for Claude Code, Codex or Cursor", Fix: null);
    }

    private static string Provider(ProviderFile file) => file switch
    {
        ProviderFile.Claude => "Claude Code, user scope",
        ProviderFile.Codex => "Codex",
        ProviderFile.Cursor => "Cursor",
        _ => file.ToString(),
    };

    private static string Removal(ProviderFile file) => file switch
    {
        ProviderFile.Claude => "claude mcp remove --scope user sindri",
        ProviderFile.Codex => "delete the 'sindri' server under mcp_servers from Codex's config.toml",
        ProviderFile.Cursor => "delete mcpServers.sindri from Cursor's mcp.json",
        _ => "delete the 'sindri' entry",
    };
}
