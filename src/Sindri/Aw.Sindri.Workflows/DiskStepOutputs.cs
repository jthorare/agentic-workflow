using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>The output folder of a run directory on disk.</summary>
internal sealed class DiskStepOutputs : IStepOutputs
{
    public ValueTask<Result<IReadOnlyList<string>>> Names(string runDirectory, DateTimeOffset? since, CancellationToken cancellationToken)
    {
        var folder = Path.Combine(runDirectory, AgentRecovery.OutputFolder);
        try
        {
            IReadOnlyList<string> names = Directory.Exists(folder) ? [.. Directory.EnumerateFiles(folder).Where(f => since is not { } from || new DateTimeOffset(File.GetLastWriteTimeUtc(f)) >= from).Select(static f => Path.GetFileName(f)).Order(StringComparer.Ordinal)] : [];
            return ValueTask.FromResult(Results.Success(names));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return ValueTask.FromResult<Result<IReadOnlyList<string>>>(KernelErrors.Unavailable.With($"{folder} can't be listed: {e.Message}"));
        }
    }

    public async ValueTask<Result<ReadOnlyMemory<byte>>> Read(string runDirectory, string name, CancellationToken cancellationToken)
    {
        if (!string.Equals(Path.GetFileName(name), name, StringComparison.Ordinal))
        {
            return SindriErrors.InvalidInput.With($"'{name}' is not one file in the output folder");
        }

        var file = Path.Combine(runDirectory, AgentRecovery.OutputFolder, name);
        try
        {
            if (new FileInfo(file).Length > IFactStore.MaxBytes)
            {
                return SindriErrors.InvalidInput.With($"{file} is larger than 1 MiB (fix: report a smaller file; outputs travel as content, at most 1 MiB each)");
            }

            var bytes = await File.ReadAllBytesAsync(file, cancellationToken).ConfigureAwait(false);
            return new ReadOnlyMemory<byte>(bytes);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return KernelErrors.Unavailable.With($"{file} can't be read: {e.Message}");
        }
    }
}
