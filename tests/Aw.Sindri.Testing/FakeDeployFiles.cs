using Aw.Kernel;
using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Testing;

/// <summary>Deploy files over an in-memory remote and an in-memory directory. Held to <see cref="DeployFilesContract"/>.</summary>
/// <param name="remote">The remote's files by path under deployment/.</param>
/// <param name="local">The files already laid out, by path.</param>
public sealed class FakeDeployFiles(IReadOnlyDictionary<string, byte[]> remote, IReadOnlyDictionary<string, byte[]> local) : IDeployFiles
{
    private readonly List<string> _requested = [];

    /// <summary>Gets the remote's files by path under deployment/; change it to serve something else.</summary>
    public IDictionary<string, byte[]> Remote { get; } = new Dictionary<string, byte[]>(remote, StringComparer.Ordinal);

    /// <summary>Gets or sets a callback told each file written (path under deployment/, bytes), so a rig can mirror the files into another store.</summary>
    public Action<string, byte[]>? Written { get; set; }

    /// <summary>Gets the laid-out files by path.</summary>
    public IDictionary<string, byte[]> Local { get; } = new Dictionary<string, byte[]>(local, StringComparer.Ordinal);

    /// <summary>Gets the paths the remote was asked for, in order.</summary>
    public IReadOnlyList<string> Requested => _requested;

    /// <summary>Gets the number of times <see cref="Fetch"/> ran.</summary>
    public int Fetches { get; private set; }

    /// <summary>Gets or sets an error every fetch returns instead of fetching.</summary>
    public Error? FailWith { get; set; }

    /// <inheritdoc />
    public ValueTask<Result<DeployFetch>> Fetch(OnyxPins pins, string deployDir, Action<string> progress, CancellationToken cancellationToken)
    {
        Fetches++;
        if (FailWith is { } failure)
        {
            return ValueTask.FromResult<Result<DeployFetch>>(failure);
        }

        Error? first = null;
        int downloaded = 0, present = 0;
        foreach (var pin in pins.Files)
        {
            if (Local.TryGetValue(pin.Path, out var have) && pin.Matches(have))
            {
                present++;
                continue;
            }

            _requested.Add(pin.Path);
            if (!Remote.TryGetValue(pin.Path, out var served))
            {
                first ??= pin.Unavailable("HTTP 404");
            }
            else if (!pin.Matches(served))
            {
                first ??= pin.Mismatch(served);
            }
            else
            {
                Local[pin.Path] = served;
                Written?.Invoke(pin.Path, served);
                downloaded++;
                progress($"fetched {pin.Path}");
            }
        }

        return ValueTask.FromResult(first is { } error ? Results.Failure<DeployFetch>(error) : Results.Success(new DeployFetch(downloaded, present)));
    }
}
