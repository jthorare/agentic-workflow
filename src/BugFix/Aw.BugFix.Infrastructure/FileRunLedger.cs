using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aw.BugFix.Core.Errors;
using Aw.BugFix.Core.Ids;
using Aw.BugFix.Core.Model;
using Aw.BugFix.Core.Ports;
using Aw.Kernel;
using Aw.Kernel.Json;

namespace Aw.BugFix.Infrastructure;

/// <summary>Run records under &lt;state&gt;/runs/ and judge inputs beside state.json, as JSON files.</summary>
internal sealed class FileRunLedger(JsonSerializerOptions json) : IRunLedger
{
    private const string KeyFile = "digest.key";

    public Result<RunSlot> Reserve(string directory, DateTimeOffset at, CheckKind kind)
    {
        if (ReadKey(directory) is not { } key)
        {
            return BugFixErrors.DigestKeyMissing.With($"{Path.Combine(directory, KeyFile)} is missing or malformed; it is made by init and holds the key the environment digests in state.json were made with, so restore it, or start over in a new state directory");
        }

        var runs = Directory.CreateDirectory(Path.Combine(directory, "runs")).FullName;
        var stamp = at.UtcDateTime.ToString("yyyyMMdd'T'HHmmssfff'Z'", CultureInfo.InvariantCulture);
        var name = kind == CheckKind.Test ? "test" : "ui";
        for (var n = 1; ; n++)
        {
            var stem = Path.Combine(runs, string.Create(CultureInfo.InvariantCulture, $"{stamp}-{name}-{n}"));
            try
            {
                // CreateNew claims the slot, so two runs in the same millisecond never share files.
                using (new FileStream($"{stem}.json", FileMode.CreateNew))
                {
                }
            }
            catch (IOException) when (File.Exists($"{stem}.json"))
            {
                continue;
            }

            return new RunSlot(kind == CheckKind.Test ? $"{stem}.log" : stem, $"{stem}.log", $"{stem}.json", key);
        }
    }

    // Written to a temporary file and moved into place, so a reader never sees a half-written key; a valid key is never
    // replaced, a malformed one is (init runs only when no unfinished bug fix uses it).
    public void CreateDigestKey(string directory)
    {
        if (ReadKey(directory) is not null)
        {
            return;
        }

        var path = Path.Combine(directory, KeyFile);
        var temporary = string.Create(CultureInfo.InvariantCulture, $"{path}.{Environment.ProcessId}.tmp");
        var options = new FileStreamOptions { Mode = FileMode.Create, Access = FileAccess.Write };
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }

        using (var stream = new FileStream(temporary, options))
        {
            stream.Write(Encoding.ASCII.GetBytes(Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32))));
        }

        File.Move(temporary, path, overwrite: true);   // only a missing or malformed key gets here
    }

    // The key, or null when the file is missing or isn't 32 bytes of hex.
    private static string? ReadKey(string directory)
    {
        var path = Path.Combine(directory, KeyFile);
        var key = File.Exists(path) ? File.ReadAllText(path).Trim() : string.Empty;
        return key.Length == 64 && key.All(char.IsAsciiHexDigitLower) ? key : null;
    }

    public string Write(RunSlot slot, RunRecord record) => WriteFile(slot.Record, AwJson.Serialize(record, json));

    public Result<StoredRun> Read(string path)
    {
        var full = Path.GetFullPath(path);
        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(full);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return BugFixErrors.EvidenceRefused.With($"cannot read evidence: {full}");
        }

        var record = AwJson.Deserialize<RunRecord>(bytes, json);
        return record.IsError
            ? BugFixErrors.EvidenceRefused.With($"{full} is not a bugfix-state run record (write evidence only with run-test or run-ui)")
            : new StoredRun(full, Convert.ToHexStringLower(SHA256.HashData(bytes)), record.Value);
    }

    public void Release(RunSlot slot) => File.Delete(slot.Record);

    public JudgeQuestion WriteJudgeInput(string directory, CandidateId candidate, ResolutionCheckInput input)
    {
        var file = Path.Combine(directory, $"judge-{candidate.Value}.json");
        var canonical = JudgeInputForm.Canonical(input, json);
        File.WriteAllBytes(file, canonical);
        return new JudgeQuestion(input, file, JudgeInputForm.Digest(canonical));
    }

    private static string WriteFile(string path, byte[] bytes)
    {
        File.WriteAllBytes(path, bytes);
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }
}
