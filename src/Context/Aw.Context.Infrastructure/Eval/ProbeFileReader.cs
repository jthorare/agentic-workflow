using System.Security.Cryptography;
using System.Text.Json;
using Aw.Context.Core;
using Aw.Context.Core.Eval;
using Aw.Context.Infrastructure.Map;
using Aw.Context.Infrastructure.Store;
using Aw.Kernel;
using Aw.Kernel.Json;

namespace Aw.Context.Infrastructure.Eval;

/// <summary>
/// Reads a probe file (JSON, spec §9.2 item 3): the named one, else $AW_SINDRI_HOME/context/eval/probes.json. Refuses one
/// that doesn't exist (036), one inside a git worktree (SND-CONTEXT-005) and one that doesn't parse or check (035), and
/// hashes its bytes so runs of the same file can be compared.
/// </summary>
internal sealed class ProbeFileReader(ContextStoreSettings settings) : IProbeFileReader
{
    private const long MaxBytes = 64L * 1024 * 1024;

    public async ValueTask<Result<ProbeFile>> Read(string? path, CancellationToken cancellationToken)
    {
        var located = Locate(path);
        if (located.IsError)
        {
            return located.Error;
        }

        var full = located.Value;
        var bytes = await Bytes(full, cancellationToken).ConfigureAwait(false);
        if (bytes.IsError)
        {
            return bytes.Error;
        }

        var set = AwJson.Deserialize<ProbeSet>(bytes.Value, EvalFileJson.Options);
        if (set.IsError)
        {
            return ContextErrors.ProbeFileInvalid.With($"{full}: {Reason(bytes.Value, set.Error.Message)}; nothing was run");
        }

        return ProbeRunner.Check(set.Value) is { } invalid ? invalid : new ProbeFile(full, Convert.ToHexStringLower(SHA256.HashData(bytes.Value)), set.Value);
    }

    /// <summary>
    /// Why the file didn't parse. An unknown key is named at its key path ('probes[0].expect.nodez: unknown key'), the way the
    /// shape checks name theirs; any other parse error keeps the parser's message.
    /// </summary>
    private static string Reason(byte[] bytes, string parserMessage)
    {
        try
        {
            _ = JsonSerializer.Deserialize(bytes, EvalFileJson.Options.GetTypeInfo(typeof(ProbeSet)));
        }
        catch (JsonException e) when (e.Message.Contains("could not be mapped", StringComparison.Ordinal) && e.Path is { Length: > 2 } path)
        {
            return $"{path[2..]}: unknown key";
        }
        catch (JsonException)
        {
            // another parse error: the parser's own message below
        }

        return parserMessage;
    }

    /// <summary>The file's real path: absolute and printable (035), existing (036), outside every worktree (005).</summary>
    private Result<string> Locate(string? path)
    {
        var named = path ?? settings.DefaultProbeFile;
        if (named.Any(char.IsControl) || !Path.IsPathRooted(named))
        {
            return ContextErrors.ProbeFileInvalid.With("the probe file's path isn't absolute or has control characters; nothing was run (fix: give its full path)");
        }

        if (RealPath.Of(named) is not { } full || !File.Exists(full))
        {
            return ContextErrors.NoProbeFile.With(path is null
                ? $"no probe file was named and {named} doesn't exist (fix: --file <probes.json>, or put one there)"
                : $"{named} doesn't exist (fix: give the probe file's full path)");
        }

        return Worktrees.Containing(full) is { } worktree
            ? ContextErrors.PathInWorktree.With($"{full} is inside the git worktree {worktree} (fix: keep probe files outside every repo)")
            : full;
    }

    private static async ValueTask<Result<byte[]>> Bytes(string full, CancellationToken cancellationToken)
    {
        try
        {
            return new FileInfo(full).Length > MaxBytes
                ? ContextErrors.ProbeFileInvalid.With($"{full}: over 64 MB; nothing was run")
                : await File.ReadAllBytesAsync(full, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return ContextErrors.NoProbeFile.With($"{full} can't be read: {e.Message}");
        }
    }
}
