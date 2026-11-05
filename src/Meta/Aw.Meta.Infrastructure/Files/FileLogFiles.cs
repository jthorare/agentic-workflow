using System.Globalization;
using Aw.Kernel;
using Aw.Meta.Core;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Infrastructure.Files;

/// <summary>ILogFiles over the file system: the end of a log, so a multi-megabyte build log costs a few kilobytes to read.</summary>
internal sealed class FileLogFiles : ILogFiles
{
    private const int BytesPerCharacterBound = 4;
    private const int MaxClaimAttempts = 1000;
    private const string ClaimMarker = ".claimed";

    public async ValueTask<Result<string>> Tail(string path, int maxCharacters, CancellationToken cancellationToken)
    {
        try
        {
            var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            await using (stream.ConfigureAwait(false))
            {
                var keep = Math.Min(stream.Length, (long)maxCharacters * BytesPerCharacterBound);
                _ = stream.Seek(-keep, SeekOrigin.End);
                var bytes = new byte[keep];
                await stream.ReadExactlyAsync(bytes, cancellationToken).ConfigureAwait(false);
                var text = System.Text.Encoding.UTF8.GetString(bytes);
                return text.Length <= maxCharacters ? text : text[^maxCharacters..];
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return MetaErrors.LogUnreadable.With(exception.Message);
        }
    }

    public ValueTask<Result<int>> LatestRun(string gateDirectory, CancellationToken cancellationToken)
    {
        try
        {
            var latest = 0;
            if (Directory.Exists(gateDirectory))
            {
                foreach (var directory in Directory.EnumerateDirectories(gateDirectory))
                {
                    var name = Path.GetFileName(directory);
                    if (name.All(char.IsAsciiDigit) && int.TryParse(name, NumberStyles.None, CultureInfo.InvariantCulture, out var run))
                    {
                        latest = Math.Max(latest, run);
                    }
                }
            }

            return ValueTask.FromResult(Results.Success(latest));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return ValueTask.FromResult<Result<int>>(MetaErrors.LogUnreadable.With(exception.Message));
        }
    }

    public async ValueTask<Result<int>> ClaimRun(string gateDirectory, int recorded, CancellationToken cancellationToken)
    {
        var latest = await LatestRun(gateDirectory, cancellationToken).ConfigureAwait(false);
        if (latest.IsError)
        {
            return latest;
        }

        try
        {
            for (var run = Math.Max(recorded, latest.Value) + 1; run <= Math.Max(recorded, latest.Value) + MaxClaimAttempts; run++)
            {
                var directory = Path.Combine(gateDirectory, run.ToString(CultureInfo.InvariantCulture));
                _ = Directory.CreateDirectory(directory);

                // CreateDirectory succeeds on an existing directory, so the directory alone is not exclusive: the marker file is.
                try
                {
                    await new FileStream(Path.Combine(directory, ClaimMarker), FileMode.CreateNew, FileAccess.Write, FileShare.None).DisposeAsync().ConfigureAwait(false);
                    return run;
                }
                catch (IOException) when (File.Exists(Path.Combine(directory, ClaimMarker)))
                {
                    // Another pass claimed this run first: try the next one.
                }
            }

            return MetaErrors.LogUnreadable.With("no free run directory in " + gateDirectory);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return MetaErrors.LogUnreadable.With(exception.Message);
        }
    }
}
