using System.Text;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Infrastructure.Profile;

/// <summary>
/// <see cref="IProfileRepoWriter"/> over the live profile file (setup-once spec §5, §5.1). The versioned write: the file's text is read, the new text is built and checked (it must parse and hold the entry), and it replaces the
/// file only if the file is still the text that was read; on a conflict it re-reads and tries once more, and a second conflict is an error that says to try again. A missing file is created with the defaults.
/// </summary>
/// <param name="settings">Where the profile is.</param>
internal sealed class YamlProfileRepoWriter(SindriSettings settings) : IProfileRepoWriter
{
    private const string Conflict = "The profile changed while Sindri was writing it. Try again.";
    private const int Attempts = 2;

    private static readonly Lock _gate = new();   // one write at a time in this process; the version check below is for everyone else

    /// <summary>Called with the profile path and the attempt number just before the write is checked against what was read: the point a concurrent edit can land. Tests use it to make one.</summary>
    internal Action<string, int>? BeforeWrite { get; init; }

    public ValueTask<Result<RepoAppended>> Append(RepoEntry repo, IReadOnlyCollection<string> defaultEffects, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repo);
        ArgumentNullException.ThrowIfNull(defaultEffects);
        for (var attempt = 0; attempt < Attempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Result<RepoAppended>? done;
            lock (_gate)
            {
                done = Try(repo, defaultEffects, attempt);
            }

            if (done is { } finished)
            {
                return ValueTask.FromResult(finished);
            }
        }

        return ValueTask.FromResult<Result<RepoAppended>>(SindriErrors.ProfileInvalid.With(Conflict));
    }

    // null: the file changed under us, read again.
    private Result<RepoAppended>? Try(RepoEntry repo, IReadOnlyCollection<string> defaultEffects, int attempt)
    {
        var path = settings.ProfilePath;
        var read = Read(path);
        if (read.IsError)
        {
            return read.Error;
        }

        var planned = Plan(read.Value, repo, defaultEffects, path);
        if (planned.IsError)
        {
            return planned.Error;
        }

        if (planned.Value.After is not { } after)
        {
            return planned.Value.Outcome;
        }

        BeforeWrite?.Invoke(path, attempt);
        var (written, reason) = Write(path, read.Value, after);
        if (written == Written.Conflict)
        {
            return null;
        }

        return written == Written.Done ? planned.Value.Outcome : SindriErrors.ProfileInvalid.With($"{path} can't be written: {reason} (fix: make the file and its folder writable, then add the folder again)");
    }

    private static Result<string?> Read(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return SindriErrors.ProfileMissing.With($"{path} can't be read: {e.Message}");
        }
    }

    // What to do with the file as read: nothing (the entry is there), or the text to write.
    private static Result<Planned> Plan(string? before, RepoEntry repo, IReadOnlyCollection<string> defaultEffects, string path)
    {
        if (before is not null)
        {
            if (YamlToJson.TryConvert(before, out var json) is { } problem)
            {
                return SindriErrors.ProfileInvalid.With($"{path}: {problem} ({ProfileReading.Fix})");
            }

            var match = ProfileRepoText.Match(json, repo.Name, repo.Path);
            if (match == RepoMatch.Same)
            {
                return new Planned(After: null, RepoAppended.AlreadyThere);
            }

            if (match == RepoMatch.Other)
            {
                return SindriErrors.SetupRepoExists.With($"another repo in the profile has the name {repo.Name} or the folder {repo.Path} (fix: pick another folder, or give a different name)");
            }
        }

        var after = ProfileRepoText.Add(before ?? DefaultProfile.Text(Environment.UserName, defaultEffects), repo.Name, repo.Path);
        return after is not null && Holds(after, repo)
            ? new Planned(after, before is null ? RepoAppended.Created : RepoAppended.Appended)
            : SindriErrors.ProfileInvalid.With($"{path} has a layout Sindri won't edit (fix: add these lines under repos by hand, then add the folder again): {string.Join(" / ", ProfileRepoText.Entry(repo.Name, repo.Path, 2))}");
    }

    private static bool Holds(string yaml, RepoEntry repo) =>
        YamlToJson.TryConvert(yaml, out var json) is null && ProfileRepoText.Match(json, repo.Name, repo.Path) == RepoMatch.Same;

    private static (Written Result, string? Reason) Write(string path, string? expected, string text)
    {
        var temporary = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            if (Path.GetDirectoryName(path) is { Length: > 0 } directory)
            {
                OwnerOnly.EnsureDirectory(directory);
            }

            File.WriteAllText(temporary, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            OwnerOnly.RestrictFiles(temporary);
            var now = File.Exists(path) ? File.ReadAllText(path) : null;
            if (!string.Equals(now, expected, StringComparison.Ordinal))
            {
                return (Written.Conflict, null);   // the file changed since it was read
            }

            File.Move(temporary, path, overwrite: expected is not null);
            return (Written.Done, null);
        }
        catch (IOException) when (expected is null && File.Exists(path))
        {
            return (Written.Conflict, null);   // someone created it between the check and the move
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return (Written.Failed, e.Message);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    private sealed record Planned(string? After, RepoAppended Outcome);

    private enum Written
    {
        Done,
        Conflict,
        Failed,
    }
}
