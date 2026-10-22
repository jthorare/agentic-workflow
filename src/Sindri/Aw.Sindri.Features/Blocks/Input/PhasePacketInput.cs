using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Features.Blocks.Input;

/// <summary>'input.phase-packet' (PB spec §10.1): the phase packet's phase.json and design.md as the section 'phase-packet'.</summary>
/// <param name="files">Reads the packet's files.</param>
/// <param name="checkouts">Where the repo is checked out (a relative packet directory is read from it).</param>
/// <param name="contents">Reads the stored 'fact:phasePacket'.</param>
internal sealed class PhasePacketInput(IBriefFiles files, IRepoCheckouts checkouts, IFactContents contents) : IStepInput, IDescribedBlock
{
    /// <summary>The block id.</summary>
    public const string Id = "input.phase-packet";

    /// <summary>The section the text is under.</summary>
    public const string Section = "phase-packet";

    /// <inheritdoc/>
    public static BlockDescriptor Descriptor => PrInputs.PhasePacket;

    /// <inheritdoc/>
    public async ValueTask<Result<InjectionSections>> Fill(StepInputContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        var packet = await InputFacts.Read(contents, context, "fact:phasePacket", cancellationToken).ConfigureAwait(false);
        if (packet.IsError)
        {
            return InputFacts.Fails(packet.Error) ? packet.Error : Unavailable(packet.Error.Code + ": " + packet.Error.Message);
        }

        if (DirOf(packet.Value) is not { } named)
        {
            return Unavailable(packet.Value is null ? "the instance has no fact:phasePacket" : "fact:phasePacket names no dir");
        }

        var dir = await Resolve(context, named, cancellationToken).ConfigureAwait(false);
        return dir.IsError ? Unavailable(dir.Error.Message) : await Serve(dir.Value, cancellationToken).ConfigureAwait(false);
    }

    private static string? DirOf(JsonNode? packet) =>
        (packet is JsonObject obj ? obj["dir"] : packet) is JsonValue value && value.TryGetValue<string>(out var dir) && !string.IsNullOrWhiteSpace(dir) ? dir : null;

    private async ValueTask<InjectionSections> Serve(string dir, CancellationToken cancellationToken)
    {
        var phase = await files.Read("phase.json", [dir], cancellationToken).ConfigureAwait(false);
        if (phase.IsError)
        {
            return Unavailable("phase.json: " + phase.Error.Message);
        }

        var design = await files.Read("design.md", [dir], cancellationToken).ConfigureAwait(false);
        return design.IsError
            ? Unavailable("design.md: " + design.Error.Message)
            : new InjectionSections(new Dictionary<string, string>(StringComparer.Ordinal) { [Section] = "## phase.json\n" + phase.Value.Text.TrimEnd() + "\n\n## design.md\n" + design.Value.Text.TrimEnd() + "\n" });
    }

    private static InjectionSections Unavailable(string why) => InputFacts.Say(Section, "The phase packet is unavailable for this Step (" + why + "); it runs without it.");

    // A relative directory is read under the repo's checkout, never under serve's working directory; an absolute one is read as it is.
    private async ValueTask<Result<string>> Resolve(StepInputContext context, string dir, CancellationToken cancellationToken)
    {
        if (Path.IsPathRooted(dir))
        {
            return dir;
        }

        if (context.Repo is not { } repo)
        {
            return SindriErrors.InvalidInput.With($"the instance has no repo to read the relative phase packet {dir} under");
        }

        var checkout = await checkouts.Of(repo, cancellationToken).ConfigureAwait(false);
        if (checkout.IsError)
        {
            return checkout.Error;
        }

        return Path.Combine(checkout.Value, dir);
    }
}
