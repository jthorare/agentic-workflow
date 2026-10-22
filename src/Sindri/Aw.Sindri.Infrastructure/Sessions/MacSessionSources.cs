namespace Aw.Sindri.Infrastructure.Sessions;

/// <summary>The macOS sources wired to the real tools.</summary>
internal static class MacSessionSources
{
    private static readonly MacToolRunner _tools = new(MacToolRunner.CallBudget);

    /// <summary>The process table over the real ps, lsof and argument block.</summary>
    /// <returns>The table.</returns>
    internal static MacProcessTable Table() => new(_tools, new SysctlProcessArguments(), OwnUid.Read());

    /// <summary>The socket table over the real lsof.</summary>
    /// <returns>The table.</returns>
    internal static MacUnixSockets Sockets() => new(_tools, OwnUid.Read());
}
