using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Packets;
using Aw.Sindri.Core.Workflow.Blocks;

namespace Aw.Sindri.Testing.Packets;

/// <summary>The in-memory <see cref="IPacketDirectory"/>: directories are names the test seeds with <see cref="Put"/>; <see cref="Create"/> keeps its files under '/runs/&lt;address&gt;/&lt;name&gt;'. Held to <see cref="PacketDirectoryContract"/>.</summary>
public sealed class InMemoryPacketDirectory : IPacketDirectory
{
    private readonly Dictionary<string, IReadOnlyDictionary<string, string>> _directories = new(StringComparer.Ordinal);

    /// <summary>Makes <paramref name="directory"/> hold <paramref name="files"/>.</summary>
    /// <param name="directory">The packet directory.</param>
    /// <param name="files">File name to text.</param>
    /// <returns>The same instance.</returns>
    public InMemoryPacketDirectory Put(string directory, IReadOnlyDictionary<string, string> files)
    {
        _directories[directory] = files;
        return this;
    }

    /// <summary>The directory a <see cref="Create"/> of this address and name returns.</summary>
    /// <param name="address">The instance's address.</param>
    /// <param name="name">The packet's name.</param>
    /// <returns>The path.</returns>
    public static string DirectoryOf(string address, string name) => $"/runs/{address}/{name}";

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyDictionary<string, string>>> Read(string directory, CancellationToken cancellationToken) =>
        ValueTask.FromResult(_directories.TryGetValue(directory, out var files) ? Results.Success(files) : SindriErrors.InvalidInput.With($"the packet directory {directory} does not exist"));

    /// <inheritdoc/>
    public ValueTask<Result<string>> Create(string address, string name, IReadOnlyDictionary<string, string> files, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(files);
        if (!RunAreaNames.IsValid(name) || files.Keys.Any(static f => !RunAreaNames.IsValid(f) || f.Contains('/', StringComparison.Ordinal)))
        {
            return ValueTask.FromResult<Result<string>>(SindriErrors.InvalidInput.With($"'{name}' or one of its file names is not valid"));
        }

        var directory = DirectoryOf(address, name);
        _directories[directory] = new Dictionary<string, string>(files, StringComparer.Ordinal);
        return ValueTask.FromResult(Results.Success(directory));
    }
}
