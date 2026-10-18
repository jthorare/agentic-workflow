using Aw.Kernel.Json;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Host.Commands.SessionWrap;

/// <summary>
/// Where the agent's session id comes from (launcher spec §4): the hook's <c lang="text">launches/&lt;id&gt;.session-id</c>, read once and deleted, and, once the hook has had
/// its chance, the single new file the CLI wrote. An id must match the generic bound and the entry's pattern from <c lang="text">launches/&lt;id&gt;.capture.json</c>.
/// </summary>
/// <param name="home">Sindri's home.</param>
internal sealed class SessionIdFiles(string home) : ISessionIdSource
{
    internal static readonly TimeSpan HookGrace = TimeSpan.FromSeconds(10);
    private static readonly System.Text.Json.JsonSerializerOptions _json = AwJson.CreateOptions(SindriJsonContext.Default);

    public ValueTask<SessionIdScan> Look(LaunchId launch, DateTimeOffset startedAt, TimeSpan elapsed, CancellationToken cancellationToken)
    {
        var directory = Path.Combine(home, LaunchFiles.Directory);
        var rule = ReadRule(Path.Combine(directory, LaunchFiles.CaptureName(launch)));
        if (ReadHook(Path.Combine(directory, LaunchFiles.SessionIdName(launch)), rule) is { } reported)
        {
            return ValueTask.FromResult(new SessionIdScan(reported, Ambiguous: false));
        }

        if (elapsed < HookGrace || rule?.NewestFile is not { } glob)
        {
            return ValueTask.FromResult(SessionIdScan.None);
        }

        var fresh = GlobFiles.Expand(glob).Where(file => File.GetCreationTimeUtc(file) >= startedAt.UtcDateTime.AddSeconds(-1)).ToList();
        return ValueTask.FromResult(SessionIdCapture.Choose(rule, fresh, ClaimedByOthers(directory, launch)));
    }

    private static CaptureRule? ReadRule(string path)
    {
        try
        {
            var read = File.Exists(path) ? AwJson.Deserialize<CaptureRule>(File.ReadAllBytes(path), _json) : null;
            return read is { IsSuccess: true } ? read.Value : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Reads the hook's file once and deletes it; an id that breaks the bound or the entry's pattern is dropped with it.</summary>
    private static SessionId? ReadHook(string path, CaptureRule? rule)
    {
        string text;
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            text = File.ReadAllText(path).Trim();
            File.Delete(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        return SessionId.TryFrom(text, out var id) && SessionIdCapture.Accepts(rule?.SessionIdPattern, id) ? id : null;
    }

    private static HashSet<string> ClaimedByOthers(string directory, LaunchId own)
    {
        var claimed = new HashSet<string>(StringComparer.Ordinal);
        if (!Directory.Exists(directory))
        {
            return claimed;
        }

        foreach (var file in Directory.EnumerateFiles(directory, "*.session-id").Where(f => !string.Equals(Path.GetFileName(f), LaunchFiles.SessionIdName(own), StringComparison.Ordinal)))
        {
            try
            {
                _ = claimed.Add(File.ReadAllText(file).Trim());
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // a file that vanished claims nothing
            }
        }

        return claimed;
    }
}
