using Aw.Kernel;

namespace Aw.Sindri.Core.Packets;

/// <summary>
/// The files of a packet directory, for a Step that derives one packet from another (an amended phase packet, line spec §7.2): read a packet's top-level files, and write a new
/// packet's files into the instance's run area. aw-packets still hashes, checks and freezes the result (<see cref="IPackets"/>).
/// </summary>
public interface IPacketDirectory
{
    /// <summary>Reads the files directly under a packet directory.</summary>
    /// <param name="directory">The packet directory.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>File name to text; an error when the directory is missing or a file cannot be read.</returns>
    public ValueTask<Result<IReadOnlyDictionary<string, string>>> Read(string directory, CancellationToken cancellationToken);

    /// <summary>Writes a packet's files into a directory of the instance's run area, replacing what a retry wrote before.</summary>
    /// <param name="address">The instance's address ('T-1/build/1').</param>
    /// <param name="name">The packet's directory name under the run area, one to three '/'-separated segments of letters, digits, '.', '_' and '-'.</param>
    /// <param name="files">File name to text; each name is a plain file name.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The directory written; an error when the address, a name or a file name is invalid or the run area cannot be written.</returns>
    public ValueTask<Result<string>> Create(string address, string name, IReadOnlyDictionary<string, string> files, CancellationToken cancellationToken);
}
