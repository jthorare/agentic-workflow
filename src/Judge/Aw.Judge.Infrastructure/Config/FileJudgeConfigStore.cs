using System.Text.Json;
using Aw.Judge.Core.Config;
using Aw.Judge.Core.Errors;
using Aw.Judge.Core.Ports;
using Aw.Kernel;
using Aw.Kernel.Data;
using Aw.Kernel.Json;

namespace Aw.Judge.Infrastructure.Config;

/// <summary>
/// IJudgeConfigStore over $AW_STATE_DIR/judge/config.json: no file is the default config; a file that can't be read
/// or isn't a valid config (JudgeConfigParser) is AW-JUDGE-007 naming the file. Written as indented Kernel-profile JSON,
/// atomically (a temporary file renamed into place).
/// </summary>
/// <param name="stateDirectory">$AW_STATE_DIR/judge (JudgeSettings.StateDirectory).</param>
/// <param name="json">The host's Kernel JSON profile (its context knows JudgeConfig).</param>
internal sealed class FileJudgeConfigStore(string stateDirectory, JsonSerializerOptions json) : IJudgeConfigStore
{
    internal const string FileName = "config.json";

    private readonly JsonSerializerOptions _indented = AwJson.CreateOptions(
        json.TypeInfoResolver ?? throw new ArgumentException("The options need the host's JSON context.", nameof(json)), indented: true);

    private readonly string _file = Path.Combine(stateDirectory, FileName);

    public async ValueTask<Result<JudgeConfig>> Load(CancellationToken cancellationToken)
    {
        byte[] bytes;
        try
        {
            bytes = await File.ReadAllBytesAsync(_file, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return JudgeConfig.Default;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Invalid(exception.Message);   // a directory in its place, no permission
        }

        var parsed = JudgeConfigParser.Parse(bytes, json);
        return parsed.IsError ? Invalid(parsed.Error.Message) : parsed;
    }

    // Written to a temporary file in the same directory and renamed over config.json (atomic on one volume), so a
    // question never reads a half-written config and an interrupted save leaves the old one in place.
    public async ValueTask<Result<Unit>> Save(JudgeConfig config, CancellationToken cancellationToken)
    {
        var temporary = $"{_file}.{Guid.NewGuid():N}.tmp";
        try
        {
            _ = Directory.CreateDirectory(stateDirectory);
            await File.WriteAllBytesAsync(temporary, AwJson.Serialize(config, _indented), cancellationToken).ConfigureAwait(false);
            File.Move(temporary, _file, overwrite: true);
            return Results.Ok();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return DataErrors.Unavailable.With($"Can't write the judge config '{_file}': {exception.Message}");
        }
        finally
        {
            Discard(temporary);
        }
    }

    // A temporary file left by a save that failed or was cancelled; gone already after a successful one.
    private static void Discard(string temporary)
    {
        try
        {
            File.Delete(temporary);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // why: best effort; the save's own result already says what happened.
        }
    }

    private Result<JudgeConfig> Invalid(string reason) => JudgeErrors.ConfigMalformed.With($"invalid config {_file}: {reason}");
}
