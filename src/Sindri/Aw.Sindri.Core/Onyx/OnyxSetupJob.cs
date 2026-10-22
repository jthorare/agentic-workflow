using System.Globalization;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Onyx;

/// <summary>The fixed words of the Onyx setup job (setup-once spec §5.6): its kind and key, the signal it waits on, how long, and the page the owner signs up on.</summary>
public static class OnyxSetupJob
{
    /// <summary>The job's kind: the action id of 'SetupOnyx'.</summary>
    public static readonly JobKind Kind = JobKind.From("SetupOnyx");

    /// <summary>The job's key within its kind: there is one Onyx.</summary>
    public static readonly JobKey Key = JobKey.From("onyx");

    /// <summary>The named signal the job parks on until the owner's login arrives.</summary>
    public const string Signal = "onyx-account";

    /// <summary>How long the job waits for the login before it fails.</summary>
    public static readonly TimeSpan LoginWait = TimeSpan.FromMinutes(30);

    /// <summary>The sign-up page of the Onyx at <paramref name="port"/>.</summary>
    /// <param name="port">Onyx's loopback port.</param>
    /// <returns>For example 'http://127.0.0.1:3000/auth/signup'.</returns>
    public static string SignupUrl(int port) => string.Create(CultureInfo.InvariantCulture, $"http://127.0.0.1:{port}/auth/signup");
}
