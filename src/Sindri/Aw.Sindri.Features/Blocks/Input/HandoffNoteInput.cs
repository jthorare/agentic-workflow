using System.Text;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Features.Blocks.Input;

/// <summary>'input.handoff-note' (PB spec W24): the latest 'fact:handoffNote' as the section 'handoff-note'.</summary>
/// <param name="contents">Reads the stored 'fact:handoffNote'.</param>
internal sealed class HandoffNoteInput(IFactContents contents) : IStepInput, IDescribedBlock
{
    /// <summary>The block id.</summary>
    public const string Id = "input.handoff-note";

    /// <summary>The section the text is under.</summary>
    public const string Section = "handoff-note";

    /// <inheritdoc/>
    public static BlockDescriptor Descriptor => PrInputs.HandoffNote;

    /// <inheritdoc/>
    public async ValueTask<Result<InjectionSections>> Fill(StepInputContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        var note = await InputFacts.Read(contents, context, "fact:handoffNote", cancellationToken).ConfigureAwait(false);
        if (note.IsError)
        {
            return InputFacts.Fails(note.Error)
                ? note.Error
                : InputFacts.Say(Section, "The handoff note is unavailable for this Step (" + note.Error.Code + ": " + note.Error.Message + "); it runs without it.");
        }

        return note.Value is null
            ? InputFacts.Say(Section, "There is no handoff note yet: no agent has left one for this instance.")
            : InputFacts.Say(Section, Text(note.Value).TrimEnd());
    }

    private static string Text(JsonNode note)
    {
        if (note is not JsonObject fields)
        {
            return note is JsonValue value && value.TryGetValue<string>(out var text) ? text : note.ToJsonString();
        }

        var lines = new StringBuilder();
        foreach (var (name, label) in new[] { ("done", "Done"), ("next", "Next"), ("risks", "Risks"), ("files", "Files") })
        {
            if (fields[name] is { } field && Flat(field) is { Length: > 0 } flat)
            {
                _ = lines.Append(label).Append(": ").Append(flat).Append('\n');
            }
        }

        return lines.Length == 0 ? note.ToJsonString() : lines.ToString();
    }

    private static string Flat(JsonNode field) => field switch
    {
        JsonArray items => string.Join(", ", items.Select(static i => i is JsonValue v && v.TryGetValue<string>(out var s) ? s : i?.ToJsonString())),
        JsonValue value => value.TryGetValue<string>(out var text) ? text : value.ToJsonString(),
        _ => field.ToJsonString(),
    };
}
