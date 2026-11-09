using System.Globalization;
using Aw.Sindri.Core.Todos;
using Aw.Sindri.Host.Endpoints;

namespace Aw.Sindri.Host.Commands;

/// <summary>'sindri todo keep &lt;id&gt; sindri|obsidian' (todo spec §4.4): picks a side of a todo changed in both places.</summary>
internal sealed partial class TodoVerbs
{
    private async Task<int> Keep(TodoVerbArguments.Parsed command, CancellationToken ct)
    {
        var id = await Resolve(command.Id, TodoCliVerb.Keep, ct).ConfigureAwait(false);
        if (id.IsError)
        {
            return await output.Error(id.Error).ConfigureAwait(false);
        }

        var fence = Fenced(command) ? "?fence=1" : string.Empty;
        var sent = await client.Post<KeepTodoBody, KeepTodoWire>(Root + "/" + id.Value.Value + "/keep" + fence, new KeepTodoBody(command.Side), ct).ConfigureAwait(false);
        if (sent.IsError)
        {
            return await output.Error(sent.Error).ConfigureAwait(false);
        }

        var side = string.Equals(command.Side, "obsidian", StringComparison.Ordinal) ? "Obsidian's" : "Sindri's";
        return command.Json
            ? await output.Raw(sent.Value.Body, 0).ConfigureAwait(false)
            : await output.Text($"{id.Value.Value} kept {side} version; written to the note in {TodoPickWindow.Span.TotalSeconds.ToString("0", CultureInfo.InvariantCulture)} s\n", 0).ConfigureAwait(false);
    }
}
