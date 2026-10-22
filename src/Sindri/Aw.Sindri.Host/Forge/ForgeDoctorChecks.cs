using Aw.Sindri.Core;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Host.Forge;

/// <summary>
/// The doctor's 'forge:credential' row (PR babysitter spec §10.2, W27): whether the approved profile's forge credential works. It names the credential's
/// source ('env:NAME', or 'command:' and the program) and never its value or the command's arguments.
/// </summary>
/// <param name="approved">The approved profile.</param>
/// <param name="pulls">The pull-request port; its Self is the check.</param>
/// <param name="environment">Reads an environment variable.</param>
/// <param name="limit">How long Self may take (5 s in the app).</param>
internal sealed class ForgeDoctorChecks(IApprovedProfile approved, IPullRequests pulls, Func<string, string?> environment, TimeSpan limit) : IDoctorChecks
{
    private const string Name = "forge:credential";
    private const string DefaultCredential = "env:AW_FORGE_TOKEN";

    public async ValueTask<IReadOnlyList<DoctorCheck>> Run(CancellationToken cancellationToken)
    {
        var latest = await approved.Latest(cancellationToken).ConfigureAwait(false);
        if (latest.IsError || latest.Value?.Profile.Workflow?.Forge is not { Adapter: not null } forge)
        {
            return [new DoctorCheck(Name, CheckState.Ok, "no forge configured", Fix: null)];
        }

        var github = forge.Adapter == "github";
        var credential = forge.Credential is { Length: > 0 } named ? named : DefaultCredential;
        if (github && credential.StartsWith("env:", StringComparison.Ordinal) && string.IsNullOrEmpty(environment(credential[4..])))
        {
            return [new DoctorCheck(Name, CheckState.Fail, $"the forge credential {Source(credential)} is not set", $"export {credential[4..]}=<token> where sindri runs, or set workflow.forge.credential")];
        }

        return [await Ask(forge, github, Source(credential), cancellationToken).ConfigureAwait(false)];
    }

    private async ValueTask<DoctorCheck> Ask(ForgeSettings forge, bool github, string source, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(limit);
        try
        {
            var self = await pulls.Self(timeout.Token).ConfigureAwait(false);
            var host = forge.Hosts is { Count: > 0 } hosts ? hosts[0] : github ? "github.com" : "forge.test";
            return self.IsSuccess ? new DoctorCheck(Name, CheckState.Ok, $"forge {host} as {self.Value}", Fix: null) : Refused(self.Error, source);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new DoctorCheck(Name, CheckState.Warn, $"the forge did not answer within {limit.TotalSeconds.ToString("0", System.Globalization.CultureInfo.InvariantCulture)} s", "check the network and workflow.forge.hosts");
        }
    }

    private static DoctorCheck Refused(Aw.Kernel.Error error, string source) => error.Code == SindriErrors.ForgeUnavailable.Code
        ? new DoctorCheck(Name, CheckState.Warn, $"the forge could not be reached with {source}: {error.Message}", "check the network and workflow.forge.hosts")
        : new DoctorCheck(Name, CheckState.Fail, $"the forge refused the credential {source}: {error.Message}", $"check that {source} holds a valid token for the forge");

    private static string Source(string credential)
    {
        if (!credential.StartsWith("command:", StringComparison.Ordinal))
        {
            return credential;
        }

        var program = credential[8..].Split(' ', StringSplitOptions.RemoveEmptyEntries)[..1];
        return "command:" + string.Concat(program);
    }
}
