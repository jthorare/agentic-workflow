using System.Security.Cryptography;
using System.Text;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>
/// The per-launch secret: minted by the launcher and kept as a hash only, in a 0600 file the launcher owns under <c lang="text">launch/secrets/</c>
/// (not the ledger, not views.db, not Temporal history). It reaches the agent without ever sitting in a process command line: the launcher writes it
/// once to <c lang="text">&lt;launch&gt;.secret</c> (0600) beside the hash, the host is told only that file's path, and 'session wrap' reads the file once, deletes it and
/// sets the secret in the agent's environment. Both files are deleted on Stop and when the session ends, so a dead launch's secret can't validate a late step-report.
/// </summary>
internal static class LaunchSecrets
{
    internal static string Mint() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));

    internal static string Hash(string secret) => "sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));

    internal static string PathFor(string directory, LaunchId launch) => Path.Combine(directory, launch.Value + ".hash");

    /// <summary>The path of the launch's one-read secret file, beside its hash.</summary>
    internal static string SecretPathFor(string directory, LaunchId launch) => Path.Combine(directory, launch.Value + ".secret");

    internal static void Write(string directory, LaunchId launch, string secret)
    {
        OwnerOnly.EnsureDirectory(Path.GetDirectoryName(directory)!);   // why: launch/ beside secrets/
        OwnerOnly.EnsureDirectory(directory);
        OwnerOnly.WriteAtomically(PathFor(directory, launch), Encoding.UTF8.GetBytes(Hash(secret) + "\n"));
        OwnerOnly.WriteAtomically(SecretPathFor(directory, launch), Encoding.UTF8.GetBytes(secret));
    }

    /// <summary>Deletes the launch's hash and its secret file; a missing file is fine (Stop and the session's end both call it).</summary>
    /// <param name="directory">The secrets directory.</param>
    /// <param name="launch">The launch.</param>
    internal static void Delete(string directory, LaunchId launch)
    {
        if (Directory.Exists(directory))
        {
            File.Delete(PathFor(directory, launch));
            File.Delete(SecretPathFor(directory, launch));
        }
    }
}
