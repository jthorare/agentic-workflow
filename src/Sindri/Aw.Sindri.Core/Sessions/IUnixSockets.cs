namespace Aw.Sindri.Core.Sessions;

/// <summary>The bound unix sockets of the system (/proc/net/unix; on macOS lsof, keyed by the socket's kernel address): the way a tmux server's socket path is found without a tmux call.</summary>
public interface IUnixSockets
{
    /// <summary>Inode (on macOS the key of a socket's kernel address) to path for every socket that has a path.</summary>
    /// <returns>The map; empty when the table can't be read.</returns>
    public IReadOnlyDictionary<long, string> Read();
}
