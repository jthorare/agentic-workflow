using System.Security.Cryptography;
using System.Text;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Leftovers;
using Aw.Sindri.Core.Secrets;

namespace Aw.Sindri.Features.Leftovers;

/// <summary>
/// What an old `sindri onyx up` left on this box: the containers and volumes carrying its compose project label, the `keychain:` entry its state.json recorded and the onyx/ directory. Read-only (it lists, never deletes), so `onyx down --purge` and a later doctor row (`index:onyx-leftover`) share it.
/// Reads state.json with its own record and uses nothing from the Onyx setup code.
/// </summary>
public static class OnyxLeftovers
{
    private const string Prefix = "sindri-onyx-";
    private const int HashHexDigits = 8;

    private static readonly System.Text.Json.JsonSerializerOptions _json = AwJson.CreateOptions(PurgeJsonContext.Default);

    /// <summary>Lists the leftovers.</summary>
    /// <param name="home">The Sindri home (AW_SINDRI_HOME).</param>
    /// <param name="docker">Lists by compose label.</param>
    /// <returns>The plan (empty when nothing is left), or SND-INDEX-014 when docker is there but did not answer.</returns>
    public static Result<PurgePlan> Find(string home, IPurgeDocker docker)
    {
        var directory = Path.Combine(home, "onyx");
        var state = ReadState(Path.Combine(directory, "state.json"));
        var project = state?.Project is { } recorded && IsOurs(recorded) ? recorded : ProjectNameFor(home);
        var containers = docker.ContainersOf(project);
        if (containers.IsError)
        {
            return containers.Error;
        }

        var volumes = docker.VolumesOf(project);
        if (volumes.IsError)
        {
            return volumes.Error;
        }

        return new PurgePlan(containers.Value, volumes.Value, Directory.Exists(directory) ? directory : null, KeychainEntry(state?.KeyPointer));
    }

    /// <summary>Names the compose project of a Sindri home the way an old `onyx up` did: `sindri-onyx-` and the first 8 hex digits of the sha256 of the home's full path.</summary>
    /// <param name="sindriHome">The Sindri home as a full path; a trailing separator doesn't change the name.</param>
    /// <returns>For example sindri-onyx-0a1b2c3d.</returns>
    public static string ProjectNameFor(string sindriHome)
    {
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(sindriHome.TrimEnd('/', '\\'))));
        return Prefix + hash[..HashHexDigits];
    }

    private static PurgeState? ReadState(string path)
    {
        try
        {
            return File.Exists(path) && AwJson.Deserialize<PurgeState>(File.ReadAllBytes(path), _json) is { IsSuccess: true } read ? read.Value : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    // A recorded name that isn't one the old `onyx up` could have made is ignored, so a hand-edited file can't point the purge at another project.
    private static bool IsOurs(string? name)
        => name is not null && name.Length == Prefix.Length + HashHexDigits && name.StartsWith(Prefix, StringComparison.Ordinal) && name[Prefix.Length..].All(static c => char.IsAsciiHexDigitLower(c));

    // Only a keychain: pointer names an entry Sindri put there; a file: pointer's file lives under onyx/ or secrets/ and goes with the directory or stays.
    private static string? KeychainEntry(string? pointer)
        => pointer is not null && SecretPointer.Parse(pointer) is { IsSuccess: true } parsed && parsed.Value.Scheme == SecretScheme.Keychain ? parsed.Value.Target : null;
}
