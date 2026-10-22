using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Core;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// <see cref="IPrWatchState"/> as one owner-only (0600) file under the Sindri home, written to a temporary name and moved into place, so a crash
/// never leaves half of one (the same care as <see cref="SequenceCursor"/>).
/// </summary>
/// <param name="home">Where the file is.</param>
internal sealed class HomePrWatchState(WorkflowsHome home) : IPrWatchState
{
    private const UnixFileMode OwnerFile = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    private const UnixFileMode OwnerDirectory = OwnerFile | UnixFileMode.UserExecute;
    private static readonly System.Text.Json.JsonSerializerOptions _json = AwJson.CreateOptions(WorkflowsJsonContext.Default);

    public async ValueTask<Result<PrWatchFile>> Read(CancellationToken cancellationToken)
    {
        byte[] bytes;
        try
        {
            if (!File.Exists(home.PrWatchFile))
            {
                return PrWatchFile.Empty;
            }

            bytes = await File.ReadAllBytesAsync(home.PrWatchFile, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) // boundary: an unreadable home is an expected failure
        {
            return Failed($"can't be read: {e.Message}");
        }

        var read = AwJson.Deserialize<PrWatchFile>(bytes, _json);
        return read.IsError ? Failed($"doesn't read: {read.Error.Message} (fix: delete it; the poller starts from what each pull request is now)") : read.Value;
    }

    public async ValueTask<Result<Unit>> Write(PrWatchFile file, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(file);
        var path = home.PrWatchFile;
        var temporary = path + "." + Path.GetRandomFileName();
        try
        {
            var directory = Path.GetDirectoryName(path)!; // why: the file is always under '<home>/workflow'
            _ = OperatingSystem.IsWindows() ? Directory.CreateDirectory(directory) : Directory.CreateDirectory(directory, OwnerDirectory);
            var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
            if (!OperatingSystem.IsWindows())
            {
                options.UnixCreateMode = OwnerFile;
            }

            var stream = new FileStream(temporary, options);
            await using (stream.ConfigureAwait(false))
            {
                await stream.WriteAsync(AwJson.Serialize(file, _json), cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);   // why: the move must not publish a name whose bytes a power cut could still lose
            }

            File.Move(temporary, path, overwrite: true);
            return Unit.Value;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) // boundary: a home that can't be written is an expected failure
        {
            return Failed($"can't be written: {e.Message} (fix: check the Sindri home's permissions and free space)");
        }
    }

    private Error Failed(string problem) => SindriErrors.ServiceFailed.With($"the pr-watch state {home.PrWatchFile} {problem}");
}
