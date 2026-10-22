using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Packets;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Infrastructure.Workflow;

namespace Aw.Sindri.Infrastructure.Packets;

/// <summary><see cref="IPacketDirectory"/> on disk: packets are read where they are, and written under the directory <see cref="RunAreaDirectory"/> names for the instance.</summary>
/// <param name="settings">Sindri's home.</param>
internal sealed class DiskPacketDirectory(SindriSettings settings) : IPacketDirectory
{
    public async ValueTask<Result<IReadOnlyDictionary<string, string>>> Read(string directory, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(directory))
        {
            return SindriErrors.InvalidInput.With($"the packet directory {directory} does not exist");
        }

        try
        {
            var files = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var path in Directory.EnumerateFiles(directory))
            {
                files[Path.GetFileName(path)] = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            }

            return files;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return SindriErrors.InvalidInput.With($"the packet in {directory} cannot be read: {e.Message}");   // boundary: disk
        }
    }

    public async ValueTask<Result<string>> Create(string address, string name, IReadOnlyDictionary<string, string> files, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(files);
        if (!RunAreaNames.IsValid(name) || files.Keys.Any(static f => !RunAreaNames.IsValid(f) || f.Contains('/', StringComparison.Ordinal)))
        {
            return SindriErrors.InvalidInput.With($"'{name}' or one of its file names is not valid (fix: use letters, digits, '.', '_' and '-'; a file name has no '/')");
        }

        if (!InstanceAddress.TryFrom(address, out var parsed))
        {
            return SindriErrors.InvalidInput.With($"'{address}' is not an instance address");
        }

        var directory = Path.Combine(RunAreaDirectory.Of(settings.Home, WorkflowInstanceId.Of(parsed)), name);
        try
        {
            _ = Directory.CreateDirectory(directory);
            foreach (var (file, text) in files)
            {
                await File.WriteAllTextAsync(Path.Combine(directory, file), text, cancellationToken).ConfigureAwait(false);
            }

            return directory;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return WorkflowErrors.BlockRunAreaUnavailable.With($"the packet {name} of {address} cannot be written: {e.Message} (fix: make $AW_SINDRI_HOME/workflow/runs writable)");   // boundary: disk
        }
    }
}
