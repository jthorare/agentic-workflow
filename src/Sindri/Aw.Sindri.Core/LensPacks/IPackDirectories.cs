using Aw.Kernel;

namespace Aw.Sindri.Core.LensPacks;

/// <summary>Reads a pack directory for 'sindri pack check' (line spec §5.3).</summary>
public interface IPackDirectories
{
    /// <summary>Every file under <paramref name="directory"/> as text, and the entries that resolve outside it.</summary>
    /// <param name="directory">The pack's directory.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The files by path inside the pack ('/' separated) and the entries (a link, say) that point outside the pack; an error when the directory can't be read.</returns>
    public ValueTask<Result<PackDirectory>> Read(string directory, CancellationToken cancellationToken);
}
