using Aw.Sindri.Core.Onyx;
using Xunit;

namespace Aw.Sindri.Testing;

/// <summary>
/// The contract every <see cref="IDeployFiles"/> honours: each pinned file ends up laid out under the directory with its pinned
/// content, a file that is already right isn't requested, and a file that fails its pin or can't be fetched is SND-ONYX-004 with
/// nothing written for it (what was there stays). Run against the fake and the HTTP adapter over a loopback server.
/// </summary>
public abstract class DeployFilesContract
{
    private const string Compose = "docker_compose/docker-compose.yml";
    private const string Nginx = "data/nginx/run-nginx.sh";

    private static readonly byte[] _composeBytes = "services: {}\n"u8.ToArray();
    private static readonly byte[] _nginxBytes = "#!/bin/sh\nexec nginx\n"u8.ToArray();

    /// <summary>A rig whose remote serves <paramref name="remote"/> and whose directory holds <paramref name="local"/>.</summary>
    /// <param name="remote">The remote's files by path under deployment/.</param>
    /// <param name="local">The files already laid out, by path.</param>
    /// <returns>The rig.</returns>
    protected abstract DeployFilesRig Create(IReadOnlyDictionary<string, byte[]> remote, IReadOnlyDictionary<string, byte[]> local);

    private static OnyxPins Pins() => new(
        "v4.9.0",
        new string('a', 40),
        [new OnyxFilePin(Compose, OnyxFilePin.Sha256Of(_composeBytes)), new OnyxFilePin(Nginx, OnyxFilePin.Sha256Of(_nginxBytes))],
        [],
        DiskBytes: 1,
        DockerMemoryBytes: 1);

    private static Dictionary<string, byte[]> Files(byte[]? compose = null, byte[]? nginx = null)
    {
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        if (compose is not null)
        {
            files[Compose] = compose;
        }

        if (nginx is not null)
        {
            files[Nginx] = nginx;
        }

        return files;
    }

    private static async Task<Kernel.Result<DeployFetch>> Run(DeployFilesRig rig)
        => await rig.Files.Fetch(Pins(), rig.DeployDir, static _ => { }, TestContext.Current.CancellationToken);

    /// <summary>Every pinned file is written under its relative layout.</summary>
    [Fact]
    public async Task Every_pinned_file_is_written_with_its_relative_layout()
    {
        using var rig = Create(Files(_composeBytes, _nginxBytes), Files());

        var fetch = (await Run(rig)).Value;

        Expect.Equal(new DeployFetch(2, 0), fetch);
        Expect.Sequence(_composeBytes, rig.ReadLocal(Compose) ?? []);
        Expect.Sequence(_nginxBytes, rig.ReadLocal(Nginx) ?? []);
    }

    /// <summary>A file that is already right is neither requested nor rewritten.</summary>
    [Fact]
    public async Task A_present_file_with_the_right_hash_is_not_requested_again()
    {
        using var rig = Create(Files(_composeBytes, _nginxBytes), Files(_composeBytes, _nginxBytes));

        var fetch = (await Run(rig)).Value;

        Expect.Equal(new DeployFetch(0, 2), fetch);
        Expect.True(rig.Requested.Count == 0);
    }

    /// <summary>A stale local file is replaced by the verified remote content.</summary>
    [Fact]
    public async Task A_stale_local_file_is_replaced_by_verified_content()
    {
        using var rig = Create(Files(_composeBytes, _nginxBytes), Files("old\n"u8.ToArray(), _nginxBytes));

        var fetch = (await Run(rig)).Value;

        Expect.Equal(new DeployFetch(1, 1), fetch);
        Expect.Sequence(_composeBytes, rig.ReadLocal(Compose) ?? []);
    }

    /// <summary>A file that fails its pin is 004 naming it and both hashes; the other good file is still written and the bad one is not.</summary>
    [Fact]
    public async Task A_hash_mismatch_is_004_and_nothing_is_written_for_that_file()
    {
        using var rig = Create(Files("tampered\n"u8.ToArray(), _nginxBytes), Files());

        var error = (await Run(rig)).Error;

        Expect.Equal("SND-ONYX-004", error.Code);
        Expect.True(error.Message.Contains(Compose, StringComparison.Ordinal));
        Expect.True(error.Message.Contains(OnyxFilePin.Sha256Of(_composeBytes)[..12], StringComparison.Ordinal));
        Expect.True(error.Message.Contains(OnyxFilePin.Sha256Of("tampered\n"u8)[..12], StringComparison.Ordinal));
        Expect.True(rig.ReadLocal(Compose) is null);
        Expect.Sequence(_nginxBytes, rig.ReadLocal(Nginx) ?? []);
    }

    /// <summary>A mismatch leaves what was already on disk untouched, even when that is stale.</summary>
    [Fact]
    public async Task A_mismatch_keeps_an_existing_stale_file()
    {
        var stale = "stale\n"u8.ToArray();
        using var rig = Create(Files("tampered\n"u8.ToArray(), _nginxBytes), Files(stale));

        var error = (await Run(rig)).Error;

        Expect.Equal("SND-ONYX-004", error.Code);
        Expect.Sequence(stale, rig.ReadLocal(Compose) ?? []);
    }

    /// <summary>A file the remote doesn't have is 004 and nothing is written for it.</summary>
    [Fact]
    public async Task A_missing_remote_file_is_004()
    {
        using var rig = Create(Files(_composeBytes), Files());

        var error = (await Run(rig)).Error;

        Expect.Equal("SND-ONYX-004", error.Code);
        Expect.True(error.Message.Contains(Nginx, StringComparison.Ordinal));
        Expect.True(rig.ReadLocal(Nginx) is null);
        Expect.Sequence(_composeBytes, rig.ReadLocal(Compose) ?? []);
    }
}
