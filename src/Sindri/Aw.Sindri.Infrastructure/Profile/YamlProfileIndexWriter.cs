using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Infrastructure.Profile;

/// <summary>
/// <see cref="IProfileIndexWriter"/> over the live profile file (concept pins spec §3.6): the same versioned write as <see cref="YamlProfileRepoWriter"/>. The file is read and hashed, the caller's hash is checked, the new
/// text is built and checked (it must parse, pass the profile's rules and hold every set entry and none of the removed ones) and it replaces the file only if the file is still the text that was read; on a conflict it
/// reads again and tries once more. Nothing is written when any check fails.
/// </summary>
/// <param name="settings">Where the profile is.</param>
/// <param name="options">The package's AwJson options.</param>
/// <param name="box">The box's limits the profile must stay inside, read each time; null checks the profile alone.</param>
internal sealed class YamlProfileIndexWriter(SindriSettings settings, JsonSerializerOptions options, Func<BoxLimits>? box = null) : IProfileIndexWriter
{
    private const string Conflict = "The profile changed while Sindri was writing it. Try again.";
    private const int Attempts = 2;

    private static readonly Lock _gate = new();   // one write at a time in this process; the version check is for everyone else

    /// <summary>Called with the profile path and the attempt number just before the write is checked against what was read: the point a concurrent edit can land. Tests use it to make one.</summary>
    internal Action<string, int>? BeforeWrite { get; init; }

    public ValueTask<Result<ProfileHash>> Write(IReadOnlyList<ConceptEdit> edits, ProfileHash? ifHash, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(edits);
        for (var attempt = 0; attempt < Attempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Result<ProfileHash>? done;
            lock (_gate)
            {
                done = Try(edits, ifHash, attempt);
            }

            if (done is { } finished)
            {
                return ValueTask.FromResult(finished);
            }
        }

        return ValueTask.FromResult<Result<ProfileHash>>(SindriErrors.ProfileInvalid.With(Conflict));
    }

    // null: the file changed under us, read again.
    private Result<ProfileHash>? Try(IReadOnlyList<ConceptEdit> edits, ProfileHash? ifHash, int attempt)
    {
        var path = settings.ProfilePath;
        var planned = Plan(path, edits, ifHash);
        if (planned.IsError)
        {
            return planned.Error;
        }

        var (before, after, hash) = planned.Value;
        if (after is null)
        {
            return hash;
        }

        BeforeWrite?.Invoke(path, attempt);
        var (result, reason) = YamlProfileRepoWriter.Write(path, before, after);
        return result switch
        {
            YamlProfileRepoWriter.Written.Conflict => null,
            YamlProfileRepoWriter.Written.Done => hash,
            YamlProfileRepoWriter.Written.Failed => SindriErrors.ProfileInvalid.With($"{path} can't be written: {reason} (fix: make the file and its folder writable, then try again)"),
            _ => throw new InvalidOperationException("unknown write result"),
        };
    }

    // The text read, the text to write (null: nothing to change) and the hash the file has after the write.
    private Result<(string Before, string? After, ProfileHash Hash)> Plan(string path, IReadOnlyList<ConceptEdit> edits, ProfileHash? ifHash)
    {
        var read = Read(path);
        if (read.IsError)
        {
            return read.Error;
        }

        var before = read.Value;
        var current = Parse(before, path);
        if (current.IsError)
        {
            return current.Error;
        }

        if (ifHash is { } expected && expected != current.Value.Hash)
        {
            return SindriErrors.ProfileHashMismatch.With("the profile changed since you read it (fix: read it again; the sheet keeps your changes)");
        }

        var after = ProfileIndexText.Apply(before, edits);
        return after is null ? Refused(path, edits) : Check(path, edits, before, after, current.Value.Hash);
    }

    // The new text must be YAML, keep the profile's rules and hold the edits; when it equals the old text there is nothing to write.
    private Result<(string Before, string? After, ProfileHash Hash)> Check(string path, IReadOnlyList<ConceptEdit> edits, string before, string after, ProfileHash current)
    {
        if (YamlToJson.TryConvert(after, out var json) is not null)
        {
            return Refused(path, edits);   // the edit broke the layout
        }

        if (string.Equals(after, before, StringComparison.Ordinal))
        {
            return (before, null, current);
        }

        var written = ProfileReading.FromJson(json, path, options, box?.Invoke());   // the entry itself may break the profile's rules
        if (written.IsError)
        {
            return written.Error;
        }

        return Holds(written.Value.Profile, edits) ? (before, after, written.Value.Hash) : Refused(path, edits);
    }

    private static Result<string> Read(string path)
    {
        try
        {
            return File.Exists(path)
                ? File.ReadAllText(path)
                : SindriErrors.ProfileMissing.With($"{path} doesn't exist (fix: run 'sindri profile init', then 'sindri repo add <path>' and 'sindri profile approve')");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return SindriErrors.ProfileMissing.With($"{path} can't be read: {e.Message}");
        }
    }

    private Result<LoadedProfile> Parse(string yaml, string path) =>
        YamlToJson.TryConvert(yaml, out var json) is { } problem
            ? SindriErrors.ProfileInvalid.With($"{path}: {problem} ({ProfileReading.Fix})")
            : ProfileReading.FromJson(json, path, options, box?.Invoke());

    private static bool Holds(SindriProfile profile, IReadOnlyList<ConceptEdit> edits)
    {
        var concepts = profile.Index?.Concepts ?? [];
        var last = new Dictionary<string, ConceptEdit>(StringComparer.Ordinal);
        foreach (var edit in edits)
        {
            last[edit.Id.Value] = edit;   // the last edit of an id is the one that stands
        }

        return last.Values.All(edit => edit.Entry is null
            ? !concepts.Any(c => c.Id is { } id && id.Value == edit.Id.Value)
            : concepts.Any(c => c.Id is { } id && id.Value == edit.Id.Value));
    }

    private static Error Refused(string path, IReadOnlyList<ConceptEdit> edits)
    {
        var lines = edits.Select(static e => e.Entry is { } entry
            ? string.Join(" / ", ProfileIndexText.Entry(e.Id.Value, entry, 4))
            : $"remove the entry with id {e.Id.Value}");
        return SindriErrors.ProfileInvalid.With($"{path} has a layout Sindri won't edit (fix: change these under index.concepts by hand, then try again): {string.Join(" ; ", lines)}");
    }
}
