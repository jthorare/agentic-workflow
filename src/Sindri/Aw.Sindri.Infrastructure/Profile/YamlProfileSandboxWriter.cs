using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Infrastructure.Profile;

/// <summary>
/// <see cref="IProfileSandboxWriter"/> over the live profile file (sandbox spec §3.6). The text is edited line by line, so every other key and comment stays: the repo's 'hosts' or 'homeAllow' list under 'sandbox.repos.&lt;repo&gt;' is
/// found or created, and the entry is appended once. The result must parse and hold the entry, and replaces the file only if the file is still the text that was read (one retry, as <see cref="YamlProfileRepoWriter"/>).
/// </summary>
/// <param name="settings">Where the profile is.</param>
internal sealed class YamlProfileSandboxWriter(SindriSettings settings) : IProfileSandboxWriter
{
    private const int Attempts = 2;

    private static readonly Lock _gate = new();   // one write at a time in this process; the version check is for everyone else

    /// <summary>The owner's home directory, which a path is made relative to.</summary>
    internal string Home { get; init; } = Environment.GetEnvironmentVariable("HOME") is { Length: > 0 } configured ? configured : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    public ValueTask<Result<SandboxAllowed>> Allow(string repo, DenialKind kind, string target, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repo);
        ArgumentNullException.ThrowIfNull(target);
        var entry = EntryOf(kind, target);
        if (entry.IsError)
        {
            return ValueTask.FromResult<Result<SandboxAllowed>>(entry.Error);
        }

        for (var attempt = 0; attempt < Attempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Result<SandboxAllowed>? done;
            lock (_gate)
            {
                done = Try(repo, kind, entry.Value);
            }

            if (done is { } finished)
            {
                return ValueTask.FromResult(finished);
            }
        }

        return ValueTask.FromResult<Result<SandboxAllowed>>(SindriErrors.ProfileInvalid.With("The profile changed while Sindri was writing it. Try again."));
    }

    // What the profile holds for the target: the host, or the path under the home; a refusal in the owner's words when the profile couldn't hold it.
    private Result<string> EntryOf(DenialKind kind, string target)
    {
        if (kind == DenialKind.Policy)
        {
            return SindriErrors.SandboxAllowRefused.With("A blocked tool can't be allowed here. Push from your own terminal, or let Sindri send it.");
        }

        var entry = kind == DenialKind.Path ? SandboxAllowances.HomeRelative(target, Home) : target;
        if (entry is null)
        {
            return SindriErrors.SandboxAllowRefused.With($"{target} isn't under your home folder, so it can't be allowed for a repo.");
        }

        return SandboxAllowances.Problem(kind, entry) is { } problem
            ? SindriErrors.SandboxAllowRefused.With($"{target} can't be put in the profile: {problem}.")
            : entry;
    }

    // null: the file changed under us, read again.
    private Result<SandboxAllowed>? Try(string repo, DenialKind kind, string entry)
    {
        var path = settings.ProfilePath;
        string before;
        try
        {
            before = File.ReadAllText(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return SindriErrors.ProfileMissing.With($"{path} can't be read: {e.Message} (fix: sindri profile init)");
        }

        if (YamlToJson.TryConvert(before, out var json) is { } problem)
        {
            return SindriErrors.ProfileInvalid.With($"{path}: {problem} ({ProfileReading.Fix})");
        }

        if (Holds(json, repo, kind, entry))
        {
            return new SandboxAllowed(entry, Added: false);
        }

        var list = kind == DenialKind.Path ? "homeAllow" : "hosts";
        var after = ProfileSandboxText.Add(before, repo, list, Lines(kind, entry));
        if (after is null || !Holds(after, repo, kind, entry))
        {
            return SindriErrors.ProfileInvalid.With($"{path} has a layout Sindri won't edit (fix: add {string.Join(" / ", Lines(kind, entry))} to sandbox.repos.{repo}.{list} by hand)");
        }

        var (written, reason) = YamlProfileRepoWriter.Write(path, before, after);
        return written switch
        {
            YamlProfileRepoWriter.Written.Conflict => null,
            YamlProfileRepoWriter.Written.Done => new SandboxAllowed(entry, Added: true),
            YamlProfileRepoWriter.Written.Failed => SindriErrors.ProfileInvalid.With($"{path} can't be written: {reason} (fix: make the file and its folder writable, then press Allow again)"),
            _ => throw new InvalidOperationException("unknown write outcome"),
        };
    }

    private static string[] Lines(DenialKind kind, string entry) =>
        kind == DenialKind.Path ? [$"- path: {YamlOnyxProfileWriter.Quote(entry)}", "  mode: ro"] : [$"- {YamlOnyxProfileWriter.Quote(entry)}"];

    private static bool Holds(string yaml, string repo, DenialKind kind, string entry) =>
        YamlToJson.TryConvert(yaml, out var json) is null && Holds(json, repo, kind, entry);

    private static bool Holds(byte[] json, string repo, DenialKind kind, string entry)
    {
        using var document = JsonDocument.Parse(json);
        var list = YamlOnyxProfileWriter.Descend(document.RootElement, "sandbox", "repos", repo, kind == DenialKind.Path ? "homeAllow" : "hosts");
        return list is { ValueKind: JsonValueKind.Array } items && items.EnumerateArray().Any(item =>
            kind == DenialKind.Path
                ? item.ValueKind == JsonValueKind.Object && item.TryGetProperty("path", out var p) && p.ValueKind == JsonValueKind.String && string.Equals(p.GetString(), entry, StringComparison.Ordinal)
                : item.ValueKind == JsonValueKind.String && string.Equals(item.GetString(), entry, StringComparison.Ordinal));
    }
}
