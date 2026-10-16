using System.Security.Cryptography;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// <see cref="IFactStore"/> as files under '&lt;home&gt;/workflow/facts' (engine spec rev 8.1 §5.3, §6.1): one file per
/// content, named by its SHA-256, owner-only (0600) in an owner-only (0700) directory. A file is written to a temporary
/// name and moved into place, so a reader never sees half of one. A stored fact reads back by its SHA-256
/// (<see cref="IFactContents"/>, engine P6 unit (d)).
/// </summary>
/// <param name="home">Where the facts directory is.</param>
internal sealed class HomeFactStore(WorkflowsHome home) : IFactStore, IFactContents
{
    private const UnixFileMode OwnerFile = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    private const UnixFileMode OwnerDirectory = OwnerFile | UnixFileMode.UserExecute;

    public async ValueTask<Result<FactValue>> Put(ReadOnlyMemory<byte> content, CancellationToken cancellationToken)
    {
        var sha = Convert.ToHexStringLower(SHA256.HashData(content.Span));
        var fact = new FactValue("fact:sha256:" + sha, sha, HeadSha: null);
        var file = Path.Combine(home.FactsDirectory, sha);
        try
        {
            if (File.Exists(file) && new FileInfo(file).Length == content.Length)   // why: a file of another size (an empty one a crash left) is rewritten
            {
                return fact;
            }

            _ = OperatingSystem.IsWindows() ? Directory.CreateDirectory(home.FactsDirectory) : Directory.CreateDirectory(home.FactsDirectory, OwnerDirectory);
            var temporary = file + "." + Path.GetRandomFileName();
            var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
            if (!OperatingSystem.IsWindows())
            {
                options.UnixCreateMode = OwnerFile;
            }

            var stream = new FileStream(temporary, options);
            await using (stream.ConfigureAwait(false))
            {
                await stream.WriteAsync(content, cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);   // why: the move must not publish a name whose bytes a power cut could still lose
            }

            File.Move(temporary, file, overwrite: true);
            return fact;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) // boundary: a home that can't be written is an expected failure
        {
            return SindriErrors.ApiInternal.With($"the fact {sha} can't be stored under {home.FactsDirectory}: {e.Message} (fix: check the Sindri home's permissions and free space)");
        }
    }

    public async ValueTask<Result<ReadOnlyMemory<byte>>> Read(FactValue fact, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fact);
        var file = Path.Combine(home.FactsDirectory, Path.GetFileName(fact.Sha256));
        try
        {
            return (ReadOnlyMemory<byte>)await File.ReadAllBytesAsync(file, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) // boundary: a fact that isn't stored, or a home that can't be read
        {
            return SindriErrors.ApiInternal.With($"the fact {fact.Ref} can't be read under {home.FactsDirectory}: {e.Message} (fix: check the Sindri home's permissions)");
        }
    }
}
