using System.Text.Json;
using Aw.Sindri.Core.Sandbox;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// What 'sindri open' and 'sindri takeover' print first for a sandboxed session (sandbox spec §7): the takeover banner for a Step's run, the attended escape (the
/// status line's words) for a session the owner opened. Nothing for a launch that ran unconfined, so the text of those verbs doesn't change.
/// </summary>
internal static class SandboxFirstScreen
{
    private const string DefaultPrefix = "C-b";

    /// <summary>The first-screen text for the launch the API served as <paramref name="launchBody"/>.</summary>
    /// <param name="launchBody">A 'GET /sindri/launches/{id}' answer; its 'sandbox' member says whether the launch is confined and whether it is attended.</param>
    /// <param name="tmuxPrefix">The session's tmux prefix option ('C-b'), read from its own server by the caller.</param>
    /// <returns>The text, or null when the launch has no sandbox (or the body isn't a launch).</returns>
    internal static string? For(ReadOnlyMemory<byte> launchBody, string tmuxPrefix)
    {
        try
        {
            using var document = JsonDocument.Parse(launchBody);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("sandbox", out var sandbox) || sandbox.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var attended = sandbox.TryGetProperty("attended", out var flag) && flag.ValueKind == JsonValueKind.True;
            return attended ? SandboxStatusLine.Render(attended: true, tmuxPrefix) : SandboxStatusLine.TakeoverBanner;
        }
        catch (JsonException)
        {
            return null;   // not a launch we can read: say nothing rather than guess
        }
    }

    /// <summary>The tmux prefix option of the server a session's attach command names, or 'C-b' when it can't be read.</summary>
    /// <param name="attach">The attach command ('tmux -L sindri-launch attach -t =snd-…'), or null.</param>
    /// <param name="ct">Cancels the read.</param>
    /// <returns>The option's value.</returns>
    internal static async Task<string> PrefixOf(IReadOnlyList<string>? attach, CancellationToken ct)
    {
        if (attach is not { Count: > 0 } || attach.ToList().IndexOf("attach") is not (> 0 and var at))
        {
            return DefaultPrefix;
        }

        try
        {
            var info = Composition.StartChild(attach[0], [.. attach.Skip(1).Take(at - 1), "show-options", "-gv", "prefix"]);
            info.RedirectStandardOutput = true;
            info.RedirectStandardError = true;
            _ = info.Environment.Remove("TMUX");   // a pane's own server is never the launcher's
            using var process = System.Diagnostics.Process.Start(info);
            if (process is null)
            {
                return DefaultPrefix;
            }

            using var bound = CancellationTokenSource.CreateLinkedTokenSource(ct);
            bound.CancelAfter(TimeSpan.FromSeconds(3));
            var output = (await process.StandardOutput.ReadToEndAsync(bound.Token).ConfigureAwait(false)).Trim();
            await process.WaitForExitAsync(bound.Token).ConfigureAwait(false);
            return process.ExitCode == 0 && output.Length > 0 ? output : DefaultPrefix;
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or OperationCanceledException or IOException)
        {
            return DefaultPrefix;   // no tmux here, or it did not answer: the usual prefix
        }
    }
}
