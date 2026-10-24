using System.Security.Cryptography;
using System.Text;
using Aw.Kernel.Json;
using Aw.UiDriver.Core;
using Aw.UiDriver.Core.Protocol;

namespace Aw.UiDriver.Avalonia;

/// <summary>The driver directory: driver.json (where it answers) and driver.token (the per-run secret, 0600), both removed when the process ends.</summary>
internal sealed class DriverFiles
{
    private const UnixFileMode OwnerOnly = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    private DriverFiles(string directory, string secret)
    {
        Folder = directory;
        Secret = secret;
    }

    internal string Folder { get; }

    internal string Secret { get; }

    /// <summary>Makes the directory (0700 on Unix) and a new random secret and writes the secret's file.</summary>
    /// <param name="directory">The driver directory.</param>
    /// <returns>The files.</returns>
    internal static DriverFiles Create(string directory)
    {
        _ = OperatingSystem.IsWindows()
            ? Directory.CreateDirectory(directory)
            : Directory.CreateDirectory(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var secret = RandomNumberGenerator.GetHexString(64, lowercase: true);
        Write(Path.Combine(directory, DriverEndpoint.TokenFile), secret);
        return new DriverFiles(directory, secret);
    }

    /// <summary>Writes driver.json once the server listens.</summary>
    /// <param name="url">The origin it answers on.</param>
    internal void PublishEndpoint(string url) =>
        Write(Path.Combine(Folder, DriverEndpoint.EndpointFile), Encoding.UTF8.GetString(AwJson.Serialize(new DriverEndpoint(DriverEndpoint.SchemaName, url, Environment.ProcessId), UiDriverJson.Options)));

    /// <summary>Removes both files (the process is ending).</summary>
    internal void Remove()
    {
        foreach (var name in new[] { DriverEndpoint.EndpointFile, DriverEndpoint.TokenFile })
        {
            try
            {
                File.Delete(Path.Combine(Folder, name));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // Left behind; the next run replaces them.
            }
        }
    }

    // Created with the owner-only mode from the start, so the secret is never readable by another user, not even for a moment.
    private static void Write(string path, string text)
    {
        var options = new FileStreamOptions { Mode = FileMode.Create, Access = FileAccess.Write, Share = FileShare.None };
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = OwnerOnly;
        }

        using var stream = new FileStream(path, options);
        stream.Write(Encoding.UTF8.GetBytes(text));
    }
}
