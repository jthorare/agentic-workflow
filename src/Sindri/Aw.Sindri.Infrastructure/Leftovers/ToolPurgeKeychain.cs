using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Leftovers;
using Aw.Sindri.Infrastructure.Processes;

namespace Aw.Sindri.Infrastructure.Leftovers;

/// <summary>IPurgeKeychain over `security` (macOS) and `secret-tool` (Linux), the tools that stored the entry.</summary>
/// <param name="runner">Runs the tool.</param>
internal sealed class ToolPurgeKeychain(IToolRunner runner) : IPurgeKeychain
{
    private const int MacNotFound = 44;   // `security` exit code for "item not found": already gone

    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(15);

    public Result<Unit> Remove(string service, string account)
    {
        var run = OperatingSystem.IsMacOS()
            ? runner.Run("security", ["delete-generic-password", "-s", service, "-a", account], _timeout)
            : OperatingSystem.IsLinux() ? runner.Run("secret-tool", ["clear", "service", service, "account", account], _timeout) : ToolRun.Missing;
        return run is { Outcome: ToolOutcome.Missing } or { Outcome: ToolOutcome.Exited, ExitCode: 0 or MacNotFound }
            ? Aw.Kernel.Results.Ok()
            : SindriErrors.IndexOnyxLeftover.With($"the keychain entry {service}/{account} could not be deleted (fix: unlock the keychain, or delete that entry in your keychain app, then run `sindri onyx down --purge` again)");
    }
}
