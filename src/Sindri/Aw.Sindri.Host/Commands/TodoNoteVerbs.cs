using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Features.SetTodoNote;
using Aw.Sindri.Features.StopTodoNote;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri todo note set &lt;vault&gt; &lt;note&gt; [--json]' and 'sindri todo note stop [--json]' (todo spec §4.4, §4.5): one validated edit of the live profile's 'todo.obsidian', in process like 'profile set' (no control plane, never started). The
/// todo writes are request class, so a launched session may run them, and the edit is live at once (no approval). Exit 0 set, removed or unchanged; 2 for SND-TODO-002, SND-PROFILE-001, SND-PROFILE-002 and a usage error.
/// </summary>
/// <param name="services">The scope the services are resolved in.</param>
/// <param name="output">Where the verbs write.</param>
internal sealed class TodoNoteVerbs(IServiceProvider services, Output output)
{
    internal async Task<int> Run(string[] args, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(args);
        var (flags, words) = (args.Skip(3).Where(static a => a.StartsWith("--", StringComparison.Ordinal)).ToList(), args.Skip(3).Where(static a => !a.StartsWith("--", StringComparison.Ordinal)).ToList());
        var json = flags is ["--json"];
        if (flags.Count > (json ? 1 : 0))
        {
            return await Usage(args[2]).ConfigureAwait(false);
        }

        return (args[2], words) switch
        {
            ("set", [var vault, var note]) => await Set(vault, note, json, ct).ConfigureAwait(false),
            ("stop", []) => await Stop(json, ct).ConfigureAwait(false),
            _ => await Usage(args[2]).ConfigureAwait(false),
        };
    }

    private async Task<int> Set(string vault, string note, bool json, CancellationToken ct)
    {
        var set = await services.GetRequiredService<IService<SetTodoNoteRequest, SetTodoNoteResponse>>().Handle(new SetTodoNoteRequest(vault, note), ct).ConfigureAwait(false);
        if (set.IsError)
        {
            return await output.Error(set.Error).ConfigureAwait(false);
        }

        var done = set.Value;
        return json
            ? await output.Json(done, 0).ConfigureAwait(false)
            : await output.Text(done.Changed ? $"todo.obsidian set to {done.Vault}/{done.Note}\n" : $"todo.obsidian is already {done.Vault}/{done.Note}: unchanged\n", 0).ConfigureAwait(false);
    }

    private async Task<int> Stop(bool json, CancellationToken ct)
    {
        var stop = await services.GetRequiredService<IService<StopTodoNoteRequest, StopTodoNoteResponse>>().Handle(new StopTodoNoteRequest(), ct).ConfigureAwait(false);
        if (stop.IsError)
        {
            return await output.Error(stop.Error).ConfigureAwait(false);
        }

        var done = stop.Value;
        return json
            ? await output.Json(done, 0).ConfigureAwait(false)
            : await output.Text(done.Changed ? string.Concat(done.Keys.Select(static k => $"removed {k}\n")) : "todo.obsidian isn't set: unchanged\n", 0).ConfigureAwait(false);
    }

    private async Task<int> Usage(string verb) =>
        await output.Error(SindriErrors.InvalidInput.With(verb == "set" ? "usage: sindri todo note set <vault> <note> [--json]" : "usage: sindri todo note stop [--json]")).ConfigureAwait(false);
}
