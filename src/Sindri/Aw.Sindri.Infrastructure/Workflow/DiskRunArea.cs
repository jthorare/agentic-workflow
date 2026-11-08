using Aw.Kernel;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Blocks;

namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary><see cref="IRunArea"/> on disk, in the directory <see cref="RunAreaDirectory"/> names.</summary>
/// <param name="settings">Sindri's home.</param>
internal sealed class DiskRunArea(SindriSettings settings) : IRunArea
{
    /// <inheritdoc/>
    public async ValueTask<Result<bool>> Write(string address, string name, string content, bool append, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        var path = PathOf(address, name);
        if (path.IsError)
        {
            return path.Error;
        }

        try
        {
            _ = Directory.CreateDirectory(Path.GetDirectoryName(path.Value)!); // why: the path always has a directory
            if (append)
            {
                await File.AppendAllTextAsync(path.Value, content, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await File.WriteAllTextAsync(path.Value, content, cancellationToken).ConfigureAwait(false);
            }

            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return WorkflowErrors.BlockRunAreaUnavailable.With($"the run area file {name} of {address} cannot be written: {e.Message} (fix: make $AW_SINDRI_HOME/workflow/runs writable)");   // boundary: disk
        }
    }

    /// <inheritdoc/>
    public async ValueTask<Result<string>> Read(string address, string name, CancellationToken cancellationToken)
    {
        var path = PathOf(address, name);
        if (path.IsError)
        {
            return path.Error;
        }

        try
        {
            return File.Exists(path.Value) ? await File.ReadAllTextAsync(path.Value, cancellationToken).ConfigureAwait(false) : string.Empty;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return WorkflowErrors.BlockRunAreaUnavailable.With($"the run area file {name} of {address} cannot be read: {e.Message}");   // boundary: disk
        }
    }

    /// <inheritdoc/>
    public Result<string> PathFor(string address, string name) => PathOf(address, name);

    private Result<string> PathOf(string address, string name)
    {
        if (!RunAreaNames.IsValid(name))
        {
            return SindriErrors.InvalidInput.With($"'{name}' is not a run area file name (fix: use one to three '/'-separated segments of letters, digits, '.', '_' and '-')");
        }

        return InstanceAddress.TryFrom(address, out var parsed)
            ? Path.Combine(RunAreaDirectory.Of(settings.Home, WorkflowInstanceId.Of(parsed)), name)
            : SindriErrors.InvalidInput.With($"'{address}' is not an instance address");
    }
}
